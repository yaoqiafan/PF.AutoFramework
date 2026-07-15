using PF.Core.Entities.Hardware;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Card;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Logging;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

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
    /// 点表存储路径：{dataDirectory}/AxisPoints/{DeviceId}.json
    /// </summary>
    public abstract class BaseAxisDevice : BaseDevice, IAxis, IAttachedDevice
    {
        private readonly List<AxisPoint> _pointTable = new();
        private readonly string _pointTableFilePath;

        // 模拟模式下的虚拟轴位置：由运动指令更新，替代真实板卡的位置反馈，
        // 保证 IsAtPoint / IsInSafePosition 等位置判断在模拟模式下结果确定
        private double _simulatedPosition;

        #region IAttachedDevice 实现

        /// <inheritdoc/>
        public IMotionCard? ParentCard { get; private set; }

        /// <inheritdoc/>
        public void AttachToCard(IMotionCard card)
        {
            ParentCard = card;
            _logger?.Info($"[{DeviceName}] 已挂载到板卡: '{card.DeviceName}' (CardIndex={card.CardIndex})");
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
        /// 点位表（只读）
        /// </summary>
        public IReadOnlyList<AxisPoint> PointTable => _pointTable.AsReadOnly();

        /// <summary>
        /// 新增或更新点位
        /// </summary>
        public void AddOrUpdatePoint(AxisPoint point)
        {
            var existing = _pointTable.FirstOrDefault(p => p.Name == point.Name);
            if (existing != null)
            {
                existing.TargetPosition = point.TargetPosition;
                existing.Speed = point.Speed;
                existing.Description = point.Description;
                existing.SortOrder = point.SortOrder;
                _logger?.Info($"[{DeviceName}] 更新点表 '{point.Name}' → {point.TargetPosition:F2} mm @ {point.Speed} mm/s");
            }
            else
            {
                _pointTable.Add(point);
                _logger?.Info($"[{DeviceName}] 新增点表 '{point.Name}' → {point.TargetPosition:F2} mm @ {point.Speed} mm/s");
            }
        }

        /// <summary>
        /// 删除点位
        /// </summary>
        public bool DeletePoint(string pointName)
        {
            var target = _pointTable.FirstOrDefault(p => p.Name == pointName);
            if (target == null) return false;
            _pointTable.Remove(target);
            _logger?.Info($"[{DeviceName}] 删除点表 '{pointName}'");
            return true;
        }

        /// <summary>
        /// 保存点位表到文件
        /// </summary>
        public void SavePointTable()
        {
            try
            {
                var sorted = _pointTable.OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToList();
                var json = JsonSerializer.Serialize(sorted, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_pointTableFilePath, json);
                _logger?.Success($"[{DeviceName}] 点表已保存（{_pointTable.Count} 条）→ {_pointTableFilePath}");
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
            var point = _pointTable.FirstOrDefault(p => p.Name == pointName)
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

        /// <summary>当前实时物理位置（工程单位，如 mm）</summary>
        public virtual double? CurrentPosition
        {
            get
            {
                EnsureCardAttached();
                if (IsSimulated) { return _simulatedPosition; }
                return ParentCard!.GetAxisCurrentPosition(AxisIndex);
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

            return await ParentCard!.HomeAxisAsync(AxisIndex, Param.HomeModel, (int)Param.HomeVel, (int)Param.HomeAcc, (int)Param.HomeDec, (int)Param.HomeOffset, token).ConfigureAwait(false);
        }

        /// <summary>绝对位置定位</summary>
        public virtual async Task<bool> MoveAbsoluteAsync(double targetPosition, double velocity, double Acc, double Dec, double STime, CancellationToken token = default)
        {
            EnsureCardAttached();

            if (IsSimulated) { await Task.Delay(1000, token); _simulatedPosition = targetPosition; return true; }

            return await ParentCard!.MoveAbsoluteAsync(AxisIndex, targetPosition, velocity, Acc, Dec, STime, token).ConfigureAwait(false);
        }

        /// <summary>相对位置定位</summary>
        public virtual async Task<bool> MoveRelativeAsync(double distance, double velocity, double Acc, double Dec, double STime, CancellationToken token = default)
        {
            EnsureCardAttached();
            if (IsSimulated) { await Task.Delay(1000, token); _simulatedPosition += distance; return true; }

            return await ParentCard!.MoveRelativeAsync(AxisIndex, distance, velocity, Acc, Dec, STime, token).ConfigureAwait(false);
        }

        /// <summary>持续点动（Jog）</summary>
        public virtual async Task<bool> JogAsync(double velocity, bool isPositive, double Acc, double Dec)
        {
            EnsureCardAttached();

            if (IsSimulated) { await Task.Delay(1000); return true; }
            return await ParentCard!.JogAsync(AxisIndex, velocity, Acc, Dec, isPositive).ConfigureAwait(false);
        }



        #region 高级功能

        #region 位置锁存



        
        /// <summary>
        /// 设置锁存模式
        /// </summary>
        public virtual async Task<bool> SetLatchMode(int LatchNo, int InPutPort, int LtcMode = 1, int LtcLogic = 0, double Filter = 0, double LatchSource = 0, int LatchType = 0, CancellationToken token = default)
        {
            EnsureCardAttached();
            if (IsSimulated) { await Task.Delay(1000, token); return true; }
            return LatchType == 0 ? await ParentCard!.SetSoftWareLatchMode(LatchNo, this.AxisIndex, InPutPort, LtcMode, LtcLogic, Filter, LatchSource, token) : await ParentCard!.SetLtcLatchMode(LatchNo, this.AxisIndex,  LtcMode, LtcLogic, Filter, LatchSource, token);
        }



        
        /// <summary>
        /// 获取锁存编号
        /// </summary>
        public virtual async Task<int> GetLatchNumber(int LatchNo,int LatchType=0, CancellationToken token = default)
        {
            EnsureCardAttached();
            if (IsSimulated) { await Task.Delay(1000, token); return 1; }
            return  LatchType ==0 ? await ParentCard!.GetSoftWareLatchNumber(LatchNo, this.AxisIndex, token) : await ParentCard!.GetLtcLatchNumber(LatchNo, this.AxisIndex, token);
        }



        /// <summary>
        /// 获取锁存位置
        /// </summary>
        public virtual async Task<double?> GetLatchPos(int LatchNo, int LatchType = 0, CancellationToken token = default)
        {
            EnsureCardAttached();
            // 模拟模式返回当前虚拟位置（锁存语义即"捕获触发瞬间的轴位置"），不再返回 0 哨兵值
            if (IsSimulated) { await Task.Delay(1000, token); return _simulatedPosition; }
            return LatchType == 0 ? await ParentCard!.GetSoftWareLatchPos(LatchNo, this.AxisIndex, token) : await ParentCard!.GetLtcLatchPos(LatchNo, this.AxisIndex, token);
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

            var (times, positions) = BuildVelocityProfileTable(targetPosition, standardVelocity, segmentPercents, segmentVelocityPercents);

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

            var (times, positions) = BuildVelocityProfileTable(targetPosition, standardVelocity, segmentPercents, segmentVelocityPercents);

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
        public virtual async   Task<bool> SetExtraPos(int Channel, int Pos, CancellationToken token = default)
        {
            EnsureCardAttached();
            if (IsSimulated) { await Task.Delay(1000, token); return true ; }
            return await ParentCard!.SetExtraPos(Channel , Pos, token);
        }

        #endregion 辅助编码器功能

        #endregion 高级功能






        /// <summary>检查当前位置是否在指定点位（含容差）</summary>
        public bool IsAtPoint(string pointName, double tolerance = 0.5)
        {
            if (CurrentPosition == null) return false;
            var point = _pointTable.FirstOrDefault(p => p.Name == pointName);
            return point != null && Math.Abs(CurrentPosition.Value - point.TargetPosition) <= tolerance;
        }

        /// <summary>轴是否在安全位置；默认查找点表中名称包含"待机"的点位，子类可覆写</summary>
        public virtual bool IsInSafePosition
        {
            get
            {
                if (CurrentPosition == null) return true;
                var safePoint = _pointTable.FirstOrDefault(p => p.Name.Contains("待机"));
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
                    _pointTable.AddRange(loaded);
                    _logger?.Info($"[{DeviceName}] 加载点表成功（{_pointTable.Count} 条）");
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn($"[{DeviceName}] 点表加载失败，将使用空表: {ex.Message}");
            }
        }
    }
}
