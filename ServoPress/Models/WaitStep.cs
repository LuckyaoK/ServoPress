using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServoPress.Models
{
    public partial class WaitStep : SequenceStep
    {
        public override string StepType => "等待";
        public override string IconSource => "/Resources/Icons/Wait.svg";
        public override string Summary => "等待信号";

        [ObservableProperty]
        private string _description = "WAIT";
       
    }
}
