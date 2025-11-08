using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ServoPress.Models
{
    public partial class TimerStep : SequenceStep
    {
        public override string StepType => "时间";
        public override string IconSource => "/Resources/Icons/TIMER.svg";
        public override string Summary => $"延时: {Duration} ms";

        [ObservableProperty]
        [Required(ErrorMessage = "必须填写描述")]
        private string _description = "TIMER";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Summary))]
        [Range(1, 10000, ErrorMessage = "延时必须在 1 到 10000 ms 之间")]
        private double _duration = 2000;
    }
}
