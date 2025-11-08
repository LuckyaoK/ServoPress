using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;


namespace ServoPress.Models
{
    public partial class MeasureStep : SequenceStep
    {
        public override string StepType => "测量";
        public override string IconSource => "/Resources/Icons/Measure.svg";
        public override string Summary => $"模式: {(Mode == MeasureMode.Start ? "启动" : "停止")}; 目标:{Target}";

        public enum MeasureMode { Start, Stop }

        /// <summary>
        /// 描述
        /// </summary>
        [ObservableProperty]
        private string _description = "MEASURE";

        /// <summary>
        /// 测试模式 
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Summary))]
        [Required(ErrorMessage = "必须填写描述")]
        private MeasureMode _mode = MeasureMode.Start;

        /// <summary>
        /// 测量目标
        /// </summary>
        [ObservableProperty]
        private string _target = "TARGETRES_NONE";

    }
}
