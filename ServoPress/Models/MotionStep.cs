using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;


namespace ServoPress.Models
{
    public partial class MotionStep : SequenceStep
    {
        public override string StepType => "移动";
        public override string IconSource => "/Resources/Icons/Motion.svg";
        public override string Summary => $"位置: {Position} mm, 速度: {Speed} mm/s, 最大力: {MaxForce} N";

        [ObservableProperty]
        [Required(ErrorMessage = "必须填写描述")]
        private string _description = "MOTION";

        /// <summary>
        /// 参考
        /// </summary>
        [ObservableProperty]
        private string _reference = "绝对位移";

        /// <summary>
        /// 位置
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Summary))]
        [Range(0, 100, ErrorMessage = "位置必须在 0 到 100 之间")]
        private double _position = 60.00;

        /// <summary>
        /// 速度
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Summary))]
        [Range(0, 100, ErrorMessage = "位置必须在 0 到 100 之间")]
        private double _speed = 50.00;

        /// <summary>
        /// 最大力
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Summary))]
        [Range(0, 100.0, ErrorMessage = "位置必须在 0 到 100 之间")]
        private double _maxForce = 1000;

       /// <summary>
       /// 加速度
       /// </summary>
        [ObservableProperty]
        [Range(0, 100, ErrorMessage = "位置必须在 0 到 100 之间")]
        private double _acceleration = 0;

        /// <summary>
        /// 减速度
        /// </summary>
        [ObservableProperty]
        [Range(0, 100, ErrorMessage = "位置必须在 0 到 100 之间")]
        private double _deceleration = 0;
    }
}
