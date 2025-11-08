using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;


namespace ServoPress.Models
{
    public partial class JumpStep : SequenceStep
    {
        public override string StepType => "跳转";
        public override string IconSource => "/Resources/Icons/Jump.svg";
        public override string Summary => $"跳转到标签: {TargetLabelId}, 计数: {Count}";

        [ObservableProperty]
        [Required(ErrorMessage = "必须填写描述")]
        private string _description = "JUMP";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Summary))] 
        private int _targetLabelId = 1;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Summary))]
        [Range(0, 100, ErrorMessage = "位置必须在 0 到 100 之间")]
        private int _count = 10;

    }
}
