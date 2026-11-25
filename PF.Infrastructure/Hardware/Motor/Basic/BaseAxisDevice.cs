using NPOI.SS.Formula.Functions;
using PF.Core.Entities.Hardware;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Card;
using PF.Core.Interfaces.Device.Hardware.Encoder.Basic;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Logging;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;

namespace PF.Infrastructure.Hardware.Motor.Basic
{
    /// <summary>
    /// 轴设备通用代理基类（Proxy Wrapper）
    ///
    /// 继承链：ConcreteAxis（可选）→ BaseAxisDevice → BaseDevice → IHardwareDevice
    ///                                                           → IAxis
    ///                                                           → IAttachedDevice
    ///
    /// 重构说明（代理/委托模式）：
    ///   · 本类不再包含任何抽象运动方法，不依赖厂商 SDK。
    ///   · 所有运动控制指令和轴状态读取均委托给 ParentCard（IMotionCard）对应方法执行。
    ///   · AxisIndex 属性由子类（或直接实例化时通过配置）提供，标识本轴在板卡内的物理索引。
    ///   · 新增硬件品牌时，只需实现一个 XXXMotionCard 类，无需再修改本类或轴设备代码。
    ///
    /// 用法示例：
    ///   直接实例化（无需子类）：
    ///     var axis = new ConcreteAxis(deviceId, deviceName, axisIndex, isSimulated, logger, dataDir);
    ///     axis.AttachToCard(gogoolCard);
    ///   扩展自定义行为时仍可继承并 override virtual 方法。
    ///
    /// 单位约定（减速比 AxisParam.GearRatio，默认 1）：
    ///   · 对外（业务指令 / 点表 / CurrentPosition）的位置、速度、加减速一律是负载侧工程单位；
    ///   · 收发板卡时按减速比换算：下发的乘、读回的除；时间量（S 段时间）与无量纲量（回零低速倍率）不换算。
    ///     减速比为 1 时数值原样进出板卡。
    ///
    /// 点表存储路径：{dataDirectory}/AxisPoints/{DeviceId}.json
    /// </summary>
    public abstract class BaseAxisDevice : AttachedDeviceBase<IMotionCard>, IAxis
    {
        private readonly List<AxisPoint> _pointTable = new();
        private readonly string _pointTableFilePath;

        // 点表会被调试页（UI 线程）增删改、被工站线程同时读取：所有访问经此锁，
        // 对外只给快照，避免"集合已修改"异常和读到改了一半的点位
        private readonly object _pointLock = new();

        /// <summary>锁内按名称取点位</summary>
        private AxisPoint? FindPoint(Func<AxisPoint, bool> predicate)
        {
            lock (_pointLock) return _pointTable.FirstOrDefault(predicate);
        }

        // 模拟模式下的虚拟轴位置：由运动指令更新，替代真实板卡的位置反馈，
        // 保证 IsAtPoint / IsInSafePosition 等位置判断在模拟模式下结果确定
        private double _simulatedPosition;

        #region 减速比换算

        // 最近一次告警过的非法减速比及是否已有记录：位置会被高频轮询，同一取值不能反复刷警告
        private bool _invalidGearRatioWarned;
        private double _warnedInvalidRatio;

        /// <summary>
        /// 本次调用使用的减速比换算器：创建时取一次有效减速比，同一条指令里的各个量用同一个值。
        /// </summary>
        private GearScale Gear => new(EffectiveGearRatio);

        /// <summary>
        /// 有效减速比：取正的有限数；否则按 1 处理（不换算），同一取值只告警一次。
        /// </summary>
        private double EffectiveGearRatio
        {
            get
            {
                var ratio = Param?.GearRatio ?? 1;
                if (ratio > 0 && !double.IsInfinity(ratio))
                {
                    return ratio;
                }

            if (!_invalidGearRatioWarned || !double.Equals(Volatile.Read(ref _warnedInvalidRatio), ratio))
            {
                Volatile.Write(ref _warnedInvalidRatio, ratio);
                _invalidGearRatioWarned = true;
                _logger?.Warn($"[{DeviceName}] 轴参数减速比 {ratio} 无效（需为大于 0 的有限数），按 1 处理");
            }
            return 1;
            }
        }

        /// <summary>负载侧与板卡之间的线性换算：板卡值 = 负载侧值 × 减速比</summary>
        private readonly struct GearScale
        {
            private readonly double _ratio;

            public GearScale(double ratio) => _ratio = ratio;

            /// <summary>负载侧（业务指令 / 点表 / 配方）的位置、速度、加减速 → 下发板卡的值</summary>
            public double ToCard(double value) => value * _ratio;

            /// <summary>板卡位置 → 负载侧</summary>
            public double FromCard(double value) => value / _ratio;

            /// <summary>位置表整表换算</summary>
            public double[] ToCard(double[] values) => Array.ConvertAll(values, ToCard);
        }

        #endregion

        #region 板卡挂载

        /// <summary>
        /// 归属的父运动控制卡（<see cref="AttachedDeviceBase{TParent}.Parent"/> 的领域别名，
        /// 保持既有调用点与日志语义不变）。
        /// </summary>
        public IMotionCard? ParentCard => Parent;

        /// <summary>将本轴挂载到运动控制卡。等价于 <see cref="AttachedDeviceBase{TParent}.AttachTo"/>。</summary>
        public void AttachToCard(IMotionCard card) => AttachTo(card);

        /// <inheritdoc/>
        protected override void OnAttached(IMotionCard parent)
        {
            _logger?.Info($"[{DeviceName}] 已挂载到板卡: '{parent.DeviceName}' (CardIndex={parent.CardIndex})");
        }

        /// <summary>
        /// 实现 <see cref="IEncoderCarrier"/>：转发到本轴已挂载的 <see cref="ParentCard"/>。
        /// 使挂在本轴下的辅助编码器（IAuxEncoder）能穿透本轴落到同一块物理板卡。
        /// 轴尚未挂载板卡时访问会抛异常，语义与 <see cref="EnsureCardAttached"/> 一致。
        /// </summary>
        IMotionCard IEncoderCarrier.EncoderCard =>
            ParentCard ?? throw new InvalidOperationException($"[{DeviceName}] 轴尚未挂载到板卡，无法作为辅助编码器的宿主。");

        #endregion

        #region 辅助编码器绑定（内部实现细节，不进入 IAxis 契约）

        // 挂在本轴下的辅助编码器（若有）。由 BaseAuxEncoder.OnAttached 在其挂载到本轴时回调注册，
        // 业务代码与 IAxis 接口均不感知 IAuxEncoder 的存在——位置锁存等高级功能内部调用它即可。
        private IAuxEncoder? _boundAuxEncoder;

        /// <summary>
        /// 供 <see cref="Encoder.Basic.BaseAuxEncoder"/> 挂载到本轴时回调注册自己（框架内部使用，
        /// 业务代码不应直接调用）。同一根轴重复挂载新编码器时以最后一次为准。
        /// </summary>
        internal void AttachAuxEncoder(IAuxEncoder encoder)
        {
            _boundAuxEncoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _logger?.Info($"[{DeviceName}] 已绑定辅助编码器 '{encoder.DeviceName}' (Channel={encoder.Channel})，硬件位置锁存将通过它执行。");
        }

        /// <summary>取本轴已绑定的辅助编码器；未绑定则记录错误日志并抛出 InvalidOperationException。</summary>
        private IAuxEncoder RequireBoundAuxEncoder([CallerMemberName] string caller = "")
        {
            if (_boundAuxEncoder is null)
            {
                var msg = $"[{DeviceName}] '{caller}'：硬件位置锁存需要本轴挂载一个辅助编码器" +
                          "（硬件配置中新增 IAuxEncoder，ParentDeviceId 指向本轴），当前未绑定。";
                _logger?.Error(msg);
                throw new InvalidOperationException(msg);
            }
            return _boundAuxEncoder;
        }

        #endregion

        /// <summary>
        /// 构造轴设备
        /// </summary>
        protected BaseAxisDevice(
            string deviceId,
            string deviceName,
            bool isSimulated,
            ILogService logger,
            string dataDirectory)
            : base(deviceId, deviceName, isSimulated, logger)
        {
            var dir = Path.Combine(dataDirectory, "AxisPoints");
            Directory.CreateDirectory(dir);
            _pointTableFilePath = Path.Combine(dir, $"{deviceId}.json");
            LoadPointTable();
        }

        // ── IAxis 点表管理 ──────────────────────────────────────────────────────

        /// <summary>
        /// 点位表（只读快照）。
        /// <para>返回的是调用时刻的副本：之后的增删不会反映到已取得的列表上，需要最新数据请重新读取本属性。
        /// 列表中的 <see cref="AxisPoint"/> 对象与点表共享，修改请走 <see cref="AddOrUpdatePoint"/>。</para>
        /// </summary>
        public IReadOnlyList<AxisPoint> PointTable
        {
            get { lock (_pointLock) return _pointTable.ToList().AsReadOnly(); }
        }

        /// <summary>
        /// 新增或更新点位
        /// </summary>
        public void AddOrUpdatePoint(AxisPoint point)
        {
            bool updated;
            lock (_pointLock)
            {
                var existing = _pointTable.FirstOrDefault(p => p.Name == point.Name);
                updated = existing != null;
                if (existing != null)
                {
                    existing.TargetPosition = point.TargetPosition;
                    existing.Speed = point.Speed;
                    existing.Acc = point.Acc;
                    existing.Dec = point.Dec;
                    existing.STime = point.STime;
                    existing.Description = point.Description;
                    existing.SortOrder = point.SortOrder;
                }
                else
                {
                    _pointTable.Add(point);
                }
            }
            _logger?.Info($"[{DeviceName}] {(updated ? "更新" : "新增")}点表 '{point.Name}' → {point.TargetPosition:F2} mm @ {point.Speed} mm/s");
        }

        /// <summary>
        /// 删除点位
        /// </summary>
        public bool DeletePoint(string pointName)
        {
            lock (_pointLock)
            {
                var target = _pointTable.FirstOrDefault(p => p.Name == pointName);
                if (target == null) return false;
                _pointTable.Remove(target);
            }
            _logger?.Info($"[{DeviceName}] 删除点表 '{pointName}'");
            return true;
        }

        /// <summary>
        /// 保存点位表到文件（原子写入：断电/崩溃时磁盘上要么是完整旧表、要么是完整新表）
        /// </summary>
        public void SavePointTable()
        {
            try
            {
                List<AxisPoint> sorted;
                lock (_pointLock) sorted = _pointTable.OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToList();

                var json = JsonSerializer.Serialize(sorted, new JsonSerializerOptions { WriteIndented = true });
                Persistence.AtomicFile.WriteAllText(_pointTableFilePath, json);
                _logger?.Success($"[{DeviceName}] 点表已保存（{sorted.Count} 条）→ {_pointTableFilePath}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{DeviceName}] 点表保存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 移动到指定点位
        /// </summary>
        public async Task<bool> MoveToPointAsync(string pointName, CancellationToken token = default)
        {
            var point = FindPoint(p => p.Name == pointName)
                ?? throw new KeyNotFoundException($"[{DeviceName}] 点表中未找到点位 '{pointName}'，请先在点表中添加。");

            _logger?.Info($"[{DeviceName}] MoveToPoint '{pointName}' → {point.TargetPosition:F2} mm @ {point.Speed} mm/s");

            if (IsSimulated) { await Task.Delay(1000, token); _simulatedPosition = point.TargetPosition; return true; }


            return await MoveAbsoluteAsync(point.TargetPosition, point.Speed, point.Acc, point.Dec, point.STime, token).ConfigureAwait(false);
        }

        // ── IAxis 轴标识（由子类/配置提供，标识本轴在父板卡中的物理索引）─────────

        /// <summary>
        /// 本轴在父板卡中的物理索引（0-based）。
        /// 由子类通过配置或构造参数提供，供委托调用时作为 axisIndex 参数传入板卡方法。
        /// </summary>
        public abstract int AxisIndex { get; }


        /// <summary>
        /// 轴参数配置
        /// </summary>
        public abstract AxisParam Param { get; set; }

        // ── IAxis 轴状态属性（委托给 ParentCard 读取，替代原来的抽象属性）──────────

        /// <summary>当前实时物理位置（负载侧工程单位，如 mm）</summary>
        public virtual double? CurrentPosition
        {
            get
            {
                EnsureCardAttached();
                if (IsSimulated) { return _simulatedPosition; }
                var pos = ParentCard!.GetAxisCurrentPosition(AxisIndex);
                return pos.HasValue ? Gear.FromCard(pos.Value) : null;
            }
        }


        /// <summary>
        /// 轴IO状态
        /// </summary>
        public virtual MotionIOStatus? AxisIOStatus
        {
            get
            {
                EnsureCardAttached();
                if (IsSimulated) { return new MotionIOStatus(); }


                return ParentCard?.GetMotionIOStatus(AxisIndex);
            }

        }





        // ── IAxis 运动控制方法（委托给 ParentCard，替代原来的抽象方法）────────────

        /// <summary>伺服使能</summary>
        public virtual async Task<bool> EnableAsync(CancellationToken token = default)
        {
            EnsureCardAttached();
            if (IsSimulated) { await Task.Delay(1000, token); return true; }

            return await ParentCard!.EnableAxisAsync(AxisIndex).ConfigureAwait(false);
        }

        /// <summary>伺服断使能</summary>
        public virtual async Task<bool> DisableAsync(CancellationToken token = default)
        {
            EnsureCardAttached();
            if (IsSimulated) { await Task.Delay(1000, token); return true; }

            return await ParentCard!.DisableAxisAsync(AxisIndex).ConfigureAwait(false);
        }

        /// <summary>停止运动</summary>
        public virtual async Task<bool> StopAsync(CancellationToken token = default)
        {
            EnsureCardAttached();
            if (IsSimulated) { await Task.Delay(1000, token); return true; }

            return await ParentCard!.StopAxisAsync(AxisIndex).ConfigureAwait(false);
        }

        /// <summary>回原点（Home）</summary>
        public virtual async Task<bool> HomeAsync(CancellationToken token = default)
        {
            EnsureCardAttached();
            if (IsSimulated) { await Task.Delay(1000, token); _simulatedPosition = 0; return true; }

            // 回零速度 / 加减速 / 偏移是负载侧单位，按减速比换算到板卡；低速倍率是无量纲量，不换算
            var gear = Gear;
            return await ParentCard!.HomeAxisAsync(AxisIndex, Param.HomeModel, (int)gear.ToCard(Param.HomeVel), (int)gear.ToCard(Param.HomeAcc), (int)gear.ToCard(Param.HomeDec), (int)gear.ToCard(Param.HomeOffset), Param.HomeLowVelRatio, token).ConfigureAwait(false);
        }

        /// <summary>绝对位置定位</summary>
        public virtual async Task<bool> MoveAbsoluteAsync(double targetPosition, double velocity, double Acc, double Dec, double STime, CancellationToken token = default)
        {
            EnsureCardAttached();

            if (IsSimulated) { await Task.Delay(1000, token); _simulatedPosition = targetPosition; return true; }

            // 位置 / 速度 / 加减速按减速比换算到板卡；S 段时间是时间量，不换算
            var gear = Gear;
            return await ParentCard!.MoveAbsoluteAsync(AxisIndex, gear.ToCard(targetPosition), gear.ToCard(velocity), gear.ToCard(Acc), gear.ToCard(Dec), STime, token).ConfigureAwait(false);
        }

        /// <summary>相对位置定位</summary>
        public virtual async Task<bool> MoveRelativeAsync(double distance, double velocity, double Acc, double Dec, double STime, CancellationToken token = default)
        {
            EnsureCardAttached();
            if (IsSimulated) { await Task.Delay(1000, token); _simulatedPosition += distance; return true; }

            var gear = Gear;
            return await ParentCard!.MoveRelativeAsync(AxisIndex, gear.ToCard(distance), gear.ToCard(velocity), gear.ToCard(Acc), gear.ToCard(Dec), STime, token).ConfigureAwait(false);
        }

        /// <summary>持续点动（Jog）</summary>
        public virtual async Task<bool> JogAsync(double velocity, bool isPositive, double Acc, double Dec)
        {
            EnsureCardAttached();

            if (IsSimulated) { await Task.Delay(1000); return true; }
            var gear = Gear;
            return await ParentCard!.JogAsync(AxisIndex, gear.ToCard(velocity), gear.ToCard(Acc), gear.ToCard(Dec), isPositive).ConfigureAwait(false);
        }



        #region 高级功能

        #region 位置锁存




        /// <summary>
        /// 设置锁存模式。硬件锁存（LatchType=1）委托给本轴挂载的辅助编码器（<see cref="_boundAuxEncoder"/>）
        /// 执行，不再直接持有/透传编码器通道号——通道号、倍率均由该编码器自己的 Channel/Multiplier 决定。
        /// <paramref name="encoder"/> 缺省（null）时自动取本轴已绑定的辅助编码器（未绑定则抛
        /// <see cref="InvalidOperationException"/>）；显式传入时以调用方指定的为准，用于需要临时
        /// 越过绑定关系操作另一路编码器的场景。
        /// </summary>
        public virtual async Task<bool> SetLatchMode(int LatchNo, int InPutPort, int LtcMode = 1, int LtcLogic = 0, double Filter = 0, double LatchSource = 0, int LatchType = 0, IAuxEncoder encoder = null, CancellationToken token = default)
        {
            EnsureCardAttached();
            if (IsSimulated) { await Task.Delay(1000, token); return true; }
            if (LatchType == 0)
                return await ParentCard!.SetSoftWareLatchMode(LatchNo, this.AxisIndex, InPutPort, LtcMode, LtcLogic, Filter, LatchSource, token).ConfigureAwait(false);
            encoder ??= RequireBoundAuxEncoder();
            return await ParentCard!.SetLtcLatchMode(LatchNo, encoder.Channel, LtcMode, LtcLogic, Filter, LatchSource).ConfigureAwait(false);
        }




        /// <summary>
        /// 获取锁存编号。硬件锁存分支同样委托给本轴挂载的辅助编码器，<paramref name="encoder"/>
        /// 缺省时的解析规则见 <see cref="SetLatchMode"/>。
        /// </summary>
        public virtual async Task<int> GetLatchNumber(int LatchNo, int LatchType = 0, IAuxEncoder encoder = null, CancellationToken token = default)
        {
            EnsureCardAttached();
            if (IsSimulated) { await Task.Delay(1000, token); return 1; }
            if (LatchType == 0)
                return await ParentCard!.GetSoftWareLatchNumber(LatchNo, this.AxisIndex, token).ConfigureAwait(false);

            encoder ??= RequireBoundAuxEncoder();
            return await ParentCard!.GetLtcLatchNumber(LatchNo, encoder.Channel, token).ConfigureAwait(false);
        }



        /// <summary>
        /// 获取锁存位置。硬件锁存分支同样委托给本轴挂载的辅助编码器，<paramref name="encoder"/>
        /// 缺省时的解析规则见 <see cref="SetLatchMode"/>。
        /// </summary>
        public virtual async Task<double?> GetLatchPos(int LatchNo, int LatchType = 0, IAuxEncoder encoder = null, CancellationToken token = default)
        {
            EnsureCardAttached();
            // 模拟模式返回当前虚拟位置（锁存语义即"捕获触发瞬间的轴位置"），不再返回 0 哨兵值
            if (IsSimulated) { await Task.Delay(1000, token); return _simulatedPosition; }
            if (LatchType == 0)
            {
                // 软件锁存存的是触发瞬间的板卡位置，换算回负载侧；
                // 硬件锁存走辅助编码器自己的倍率，与本轴减速比无关，不换算
                var latched = await ParentCard!.GetSoftWareLatchPos(LatchNo, this.AxisIndex, token).ConfigureAwait(false);
                return latched.HasValue ? Gear.FromCard(latched.Value) : null;
            }

            encoder ??= RequireBoundAuxEncoder();
            return await ParentCard.GetLtcLatchPos(LatchNo, encoder.Channel, encoder.Multiplier, token).ConfigureAwait(false);
        }


        #endregion 位置锁存


        #region 任意速度规划 (PVT)

        /// <summary>
        /// 按"位移分段占比 + 速度分段占比"自动规划 PTT（梯形速度）单轴任意速度运动，并启动执行
        /// </summary>
        public virtual async Task<bool> MoveVelocityProfileAsync(double targetPosition, double standardVelocity, double[] segmentPercents, double[] segmentVelocityPercents, CancellationToken token = default)
        {
            EnsureCardAttached();
            if (IsSimulated) { await Task.Delay(1000); return true; }

            // 规划表是负载侧单位；速度只用来推算各段耗时，位置与速度同乘减速比时耗时不变，所以只换算位置表
            var (times, positions) = BuildVelocityProfileTable(targetPosition, standardVelocity, segmentPercents, segmentVelocityPercents);
            positions = Gear.ToCard(positions);

            if (!await ParentCard!.SetPttTableAsync(AxisIndex, times, positions, token).ConfigureAwait(false))
            {
                return false;
            }
            return await ParentCard!.StartPvtMoveAsync(new[] { AxisIndex }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// 按"位移分段占比 + 速度分段占比"自动规划 PTS（平滑速度）单轴任意速度运动，并启动执行
        /// </summary>
        public virtual async Task<bool> MoveVelocityProfileSmoothAsync(double targetPosition, double standardVelocity, double[] segmentPercents, double[] segmentVelocityPercents, double smoothPercent = 30, CancellationToken token = default)
        {
            EnsureCardAttached();
            if (IsSimulated) { await Task.Delay(1000); return true; }

            // 规划表是负载侧单位，只换算位置表（原因见 MoveVelocityProfileAsync）
            var (times, positions) = BuildVelocityProfileTable(targetPosition, standardVelocity, segmentPercents, segmentVelocityPercents);
            positions = Gear.ToCard(positions);

            var percents = new double[times.Length];
            for (int i = 1; i < percents.Length; i++)
            {
                percents[i] = smoothPercent;
            }

            if (!await ParentCard!.SetPtsTableAsync(AxisIndex, times, positions, percents, token).ConfigureAwait(false))
            {
                return false;
            }
            return await ParentCard!.StartPvtMoveAsync(new[] { AxisIndex }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// 将"目标位置 + 位移分段占比 + 速度分段占比"换算为 PTT/PTS 所需的 时间/位置 表。
        /// 各段位移 = 总位移(targetPosition - 当前实际位置) * segmentPercents[i] / 100；
        /// 各段速度 = standardVelocity * segmentVelocityPercents[i] / 100；
        /// 各段耗时 = |段位移| / 段速度；位置表以当前实际位置为 0 点累加，供板卡侧作为相对位移下发。
        /// </summary>
        private (double[] times, double[] positions) BuildVelocityProfileTable(double targetPosition, double standardVelocity, double[] segmentPercents, double[] segmentVelocityPercents)
        {
            if (segmentPercents == null || segmentVelocityPercents == null
                || segmentPercents.Length == 0 || segmentPercents.Length != segmentVelocityPercents.Length)
            {
                throw new ArgumentException($"[{DeviceName}] segmentPercents 与 segmentVelocityPercents 长度须一致且不为空");
            }
            if (standardVelocity <= 0)
            {
                throw new ArgumentException($"[{DeviceName}] standardVelocity 必须大于 0");
            }
            if (Math.Abs(segmentPercents.Sum() - 100.0) > 0.01)
            {
                throw new ArgumentException($"[{DeviceName}] segmentPercents 总和须为 100，当前为 {segmentPercents.Sum():F2}");
            }

            double? start = CurrentPosition;
            if (!start.HasValue)
            {
                throw new InvalidOperationException($"[{DeviceName}] 无法读取轴当前位置，无法规划速度曲线");
            }

            double totalDisp = targetPosition - start.Value;
            int n = segmentPercents.Length;
            var times = new double[n + 1];
            var positions = new double[n + 1];

            for (int i = 0; i < n; i++)
            {
                if (segmentVelocityPercents[i] <= 0)
                {
                    throw new ArgumentException($"[{DeviceName}] segmentVelocityPercents[{i}] 必须大于 0");
                }

                double segDisp = totalDisp * segmentPercents[i] / 100.0;
                double segVel = standardVelocity * segmentVelocityPercents[i] / 100.0;
                double segTime = Math.Abs(segDisp) / segVel;

                positions[i + 1] = positions[i] + segDisp;
                times[i + 1] = times[i] + segTime;
            }

            return (times, positions);
        }

        #endregion 任意速度规划 (PVT)


        #region 辅助编码器功能
        /// <summary>
        /// 设置辅助编码器的位置
        /// </summary>
        [Obsolete("辅助编码器已抽取为独立的 IAuxEncoder 设备（挂在本轴或运动控制卡下均可）。" +
            "请改为在硬件配置中新增一个 IAuxEncoder（ParentDeviceId 指向本轴或所在运动控制卡），" +
            "并调用其 SetPositionAsync。本方法保留仅为兼容旧调用点，行为不变，将在后续版本移除。")]
        public virtual async Task<bool> SetExtraPos(int Channel, int Pos, double Mulit = 2, CancellationToken token = default)
        {
            EnsureCardAttached();
            if (IsSimulated) { await Task.Delay(1000, token); return true; }
            return await ParentCard!.SetExtraPos(Channel, Pos, Mulit, token);
        }



     


        #endregion 辅助编码器功能

        #endregion 高级功能






        /// <summary>检查当前位置是否在指定点位（含容差）</summary>
        public bool IsAtPoint(string pointName, double tolerance = 0.5)
        {
            if (CurrentPosition == null) return false;
            var point = FindPoint(p => p.Name == pointName);
            return point != null && Math.Abs(CurrentPosition.Value - point.TargetPosition) <= tolerance;
        }

        /// <summary>轴是否在安全位置；默认查找点表中名称包含"待机"的点位，子类可覆写</summary>
        public virtual bool IsInSafePosition
        {
            get
            {
                if (CurrentPosition == null) return true;
                var safePoint = FindPoint(p => p.Name.Contains("待机"));
                if (safePoint == null) return true;
                return Math.Abs(CurrentPosition.Value - safePoint.TargetPosition) <= 0.5;
            }
        }

        // ── 私有工具 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 检查父板卡是否已挂载，未挂载则记录错误日志并抛出 InvalidOperationException。
        /// </summary>
        private void EnsureCardAttached([CallerMemberName] string caller = "")
        {
            if (ParentCard is null)
            {
                var msg = $"[{DeviceName}] '{caller}'：设备尚未挂载到板卡，请先调用 AttachToCard()。";
                _logger?.Error(msg);
                throw new InvalidOperationException(msg);
            }
        }

        private void LoadPointTable()
        {
            if (!File.Exists(_pointTableFilePath)) return;

            try
            {
                var json = File.ReadAllText(_pointTableFilePath);
                var loaded = JsonSerializer.Deserialize<List<AxisPoint>>(json);
                if (loaded != null)
                {
                    lock (_pointLock) _pointTable.AddRange(loaded);
                    _logger?.Info($"[{DeviceName}] 加载点表成功（{loaded.Count} 条）");
                }
            }
            catch (Exception ex)
            {
                // 读不出来时随后的 EnsurePointsExist/SavePointTable 会用空表覆盖原文件——
                // 先把坏文件另存一份，示教数据至少还能人工恢复
                var backup = Persistence.AtomicFile.BackupCorrupt(_pointTableFilePath);
                _logger?.Error($"[{DeviceName}] 点表加载失败，将使用空表（原文件已备份为 {backup ?? "（备份失败）"}，" +
                               $"请核对后恢复，否则现场示教点位需重新示教）: {ex.Message}");
            }
        }

      
    }
}
