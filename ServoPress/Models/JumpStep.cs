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
        private string _description = "JUMP";

        [ObservableProperty]
        private int _targetLabelId = 1;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Summary))]
        private int _count = 10;

    }
}
