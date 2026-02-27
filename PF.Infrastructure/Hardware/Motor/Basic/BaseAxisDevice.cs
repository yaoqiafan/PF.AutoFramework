using PF.Core.Entities.Hardware;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Card;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Logging;
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

        public IReadOnlyList<AxisPoint> PointTable => _pointTable.AsReadOnly();

        public void AddOrUpdatePoint(AxisPoint point)
        {
            var existing = _pointTable.FirstOrDefault(p => p.Name == point.Name);
            if (existing != null)
            {
                existing.TargetPosition = point.TargetPosition;
                existing.Speed          = point.Speed;
                existing.Description    = point.Description;
                existing.SortOrder      = point.SortOrder;
                _logger?.Info($"[{DeviceName}] 更新点表 '{point.Name}' → {point.TargetPosition:F2} mm @ {point.Speed} mm/s");
            }
            else
            {
                _pointTable.Add(point);
                _logger?.Info($"[{DeviceName}] 新增点表 '{point.Name}' → {point.TargetPosition:F2} mm @ {point.Speed} mm/s");
            }
        }

        public bool DeletePoint(string pointName)
        {
            var target = _pointTable.FirstOrDefault(p => p.Name == pointName);
            if (target == null) return false;
            _pointTable.Remove(target);
            _logger?.Info($"[{DeviceName}] 删除点表 '{pointName}'");
            return true;
        }

        public void SavePointTable()
        {
            try
            {
                var sorted = _pointTable.OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToList();
                var json   = JsonSerializer.Serialize(sorted, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_pointTableFilePath, json);
                _logger?.Success($"[{DeviceName}] 点表已保存（{_pointTable.Count} 条）→ {_pointTableFilePath}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{DeviceName}] 点表保存失败: {ex.Message}");
            }
        }

        public async Task<bool> MoveToPointAsync(string pointName, CancellationToken token = default)
        {
            var point = _pointTable.FirstOrDefault(p => p.Name == pointName)
                ?? throw new KeyNotFoundException($"[{DeviceName}] 点表中未找到点位 '{pointName}'，请先在点表中添加。");

            _logger?.Info($"[{DeviceName}] MoveToPoint '{pointName}' → {point.TargetPosition:F2} mm @ {point.Speed} mm/s");
            return await MoveAbsoluteAsync(point.TargetPosition, point.Speed, token).ConfigureAwait(false);
        }

        // ── IAxis 轴标识（由子类/配置提供，标识本轴在父板卡中的物理索引）─────────

        /// <summary>
        /// 本轴在父板卡中的物理索引（0-based）。
        /// 由子类通过配置或构造参数提供，供委托调用时作为 axisIndex 参数传入板卡方法。
        /// </summary>
        public abstract int AxisIndex { get; }

        // ── IAxis 轴状态属性（委托给 ParentCard 读取，替代原来的抽象属性）──────────

        /// <summary>当前实时物理位置（工程单位，如 mm）</summary>
        public virtual double CurrentPosition
        {
            get
            {
                EnsureCardAttached();
                return ParentCard!.GetAxisCurrentPosition(AxisIndex);
            }
        }

        /// <summary>是否正在运动中</summary>
        public virtual bool IsMoving
        {
            get
            {
                EnsureCardAttached();
                return ParentCard!.IsAxisMoving(AxisIndex);
            }
        }

        /// <summary>是否触碰正向硬件限位传感器</summary>
        public virtual bool IsPositiveLimit
        {
            get
            {
                EnsureCardAttached();
                return ParentCard!.IsAxisPositiveLimit(AxisIndex);
            }
        }

        /// <summary>是否触碰负向硬件限位传感器</summary>
        public virtual bool IsNegativeLimit
        {
            get
            {
                EnsureCardAttached();
                return ParentCard!.IsAxisNegativeLimit(AxisIndex);
            }
        }

        /// <summary>伺服是否已使能（Servo On）</summary>
        public virtual bool IsEnabled
        {
            get
            {
                EnsureCardAttached();
                return ParentCard!.IsAxisEnabled(AxisIndex);
            }
        }

        // ── IAxis 运动控制方法（委托给 ParentCard，替代原来的抽象方法）────────────

        /// <summary>伺服使能</summary>
        public virtual async Task<bool> EnableAsync()
        {
            EnsureCardAttached();
            return await ParentCard!.EnableAxisAsync(AxisIndex).ConfigureAwait(false);
        }

        /// <summary>伺服断使能</summary>
        public virtual async Task<bool> DisableAsync()
        {
            EnsureCardAttached();
            return await ParentCard!.DisableAxisAsync(AxisIndex).ConfigureAwait(false);
        }

        /// <summary>停止运动</summary>
        public virtual async Task<bool> StopAsync()
        {
            EnsureCardAttached();
            return await ParentCard!.StopAxisAsync(AxisIndex).ConfigureAwait(false);
        }

        /// <summary>回原点（Home）</summary>
        public virtual async Task<bool> HomeAsync(CancellationToken token = default)
        {
            EnsureCardAttached();
            return await ParentCard!.HomeAxisAsync(AxisIndex, token).ConfigureAwait(false);
        }

        /// <summary>绝对位置定位</summary>
        public virtual async Task<bool> MoveAbsoluteAsync(double targetPosition, double velocity, CancellationToken token = default)
        {
            EnsureCardAttached();
            return await ParentCard!.MoveAbsoluteAsync(AxisIndex, targetPosition, velocity, token).ConfigureAwait(false);
        }

        /// <summary>相对位置定位</summary>
        public virtual async Task<bool> MoveRelativeAsync(double distance, double velocity, CancellationToken token = default)
        {
            EnsureCardAttached();
            return await ParentCard!.MoveRelativeAsync(AxisIndex, distance, velocity, token).ConfigureAwait(false);
        }

        /// <summary>持续点动（Jog）</summary>
        public virtual async Task<bool> JogAsync(double velocity, bool isPositive)
        {
            EnsureCardAttached();
            return await ParentCard!.JogAsync(AxisIndex, velocity, isPositive).ConfigureAwait(false);
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
                var json   = File.ReadAllText(_pointTableFilePath);
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
