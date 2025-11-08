using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServoPress.Models
{
    public partial class LabelStep : SequenceStep
    {
        public override string StepType => "标签";
        public override string IconSource => "/Resources/Icons/Label.svg";
        public override string Summary => $"标签ID: {LabelId}";

        [ObservableProperty]
        [Required(ErrorMessage = "必须填写描述")]
        private string _description = "LABEL";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Summary))]
        private int _labelId = 1;

    }
}
