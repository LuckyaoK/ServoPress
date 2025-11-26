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
        private string _description = "TIMER";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Summary))]
        private double _duration = 2000;
    }
}
