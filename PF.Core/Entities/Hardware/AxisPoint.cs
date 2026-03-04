using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PF.Core.Entities.Hardware
{
    /// <summary>
    /// 轴点表条目 - 存储轴的预设运动目标位置
    ///
    /// 设计用途：避免在业务代码中硬编码坐标值，
    /// 改为通过名称引用（如 "上料位"、"加工位"、"检测位"），
    /// 实现工艺参数与运动逻辑的解耦。
    /// </summary>
    public class AxisPoint : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private double _targetPosition;
        private double _suggestedVelocity;
        private string _description = string.Empty;
        private int _sortOrder;

        private double _STime = 0.08;

        private double _Acc;

        private double _Dec;



        /// <summary>点位唯一名称（如 "上料位"）</summary>
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        /// <summary>目标绝对位置（工程单位，如 mm）</summary>
        public double TargetPosition
        {
            get => _targetPosition;
            set { _targetPosition = value; OnPropertyChanged(); }
        }

        /// <summary>建议运动速度（工程单位，如 mm/s）</summary>
        public double Speed
        {
            get => _suggestedVelocity;
            set { _suggestedVelocity = value; OnPropertyChanged(); }
        }

        /// <summary>备注说明</summary>
        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        /// <summary>UI 显示排序序号</summary>
        public int SortOrder
        {

            get => _sortOrder;
            set { _sortOrder = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// S段时间（单位：秒）
        /// </summary>
        public double STime
        {
            get => _STime;
            set
            { _STime = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 加速度
        /// </summary>
        public double Acc
        {
            get => _Acc;
            set { _Acc = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 减速度
        /// </summary>
        public double Dec
        {
            get => _Dec;
            set { _Dec = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
