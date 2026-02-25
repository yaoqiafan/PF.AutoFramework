using PF.Core.Entities.Hardware;
using PF.Core.Interfaces.Hardware.Motor.Basic;
using PF.Core.Interfaces.Logging;
using System.Text.Json;

namespace PF.Infrastructure.Hardware
{
    /// <summary>
    /// 轴设备抽象基类
    ///
    /// 在 BaseDevice 的基础上，实现 IAxis 中的点表管理（PointTable CRUD + JSON 持久化）
    /// 和 MoveToPointAsync 便捷方法。子类只需实现具体的运动控制方法即可。
    ///
    /// 点表存储：每个轴在 <see cref="_dataDirectory"/>/AxisPoints/{DeviceId}.json 下存一个 JSON 文件。
    /// </summary>
    public abstract class BaseAxisDevice : BaseDevice, IAxis
    {
        private readonly List<AxisPoint> _pointTable = new();
        private readonly string _pointTableFilePath;

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
                existing.TargetPosition    = point.TargetPosition;
                existing.SuggestedVelocity = point.SuggestedVelocity;
                existing.Description       = point.Description;
                existing.SortOrder         = point.SortOrder;
                _logger?.Info($"[{DeviceName}] 更新点表 '{point.Name}' → {point.TargetPosition:F2} mm @ {point.SuggestedVelocity} mm/s");
            }
            else
            {
                _pointTable.Add(point);
                _logger?.Info($"[{DeviceName}] 新增点表 '{point.Name}' → {point.TargetPosition:F2} mm @ {point.SuggestedVelocity} mm/s");
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
                var json = JsonSerializer.Serialize(sorted, new JsonSerializerOptions { WriteIndented = true });
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

            _logger?.Info($"[{DeviceName}] MoveToPoint '{pointName}' → {point.TargetPosition:F2} mm @ {point.SuggestedVelocity} mm/s");
            return await MoveAbsoluteAsync(point.TargetPosition, point.SuggestedVelocity, token);
        }

        // ── IAxis 运动控制方法（留给子类实现）────────────────────────────────────

        public abstract int AxisIndex { get; }
        public abstract double CurrentPosition { get; }
        public abstract bool IsMoving { get; }
        public abstract bool IsPositiveLimit { get; }
        public abstract bool IsNegativeLimit { get; }
        public abstract bool IsEnabled { get; }

        public abstract Task<bool> EnableAsync();
        public abstract Task<bool> DisableAsync();
        public abstract Task<bool> StopAsync();
        public abstract Task<bool> HomeAsync(CancellationToken token = default);
        public abstract Task<bool> MoveAbsoluteAsync(double targetPosition, double velocity, CancellationToken token = default);
        public abstract Task<bool> MoveRelativeAsync(double distance, double velocity, CancellationToken token = default);
        public abstract Task<bool> JogAsync(double velocity, bool isPositive);

        // ── 私有工具 ────────────────────────────────────────────────────────────

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
