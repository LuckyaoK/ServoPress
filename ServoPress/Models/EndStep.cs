using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServoPress.Models
{
    public partial class EndStep : SequenceStep
    {
        public override string StepType => "结束";
        public override string IconSource => "/Resources/Icons/SequenceEnd.svg";
        public override string Summary => "序列结束";


        [ObservableProperty]
        [Required(ErrorMessage = "必须填写描述")]
        private string _description = "SEQUENCE_END";
    }
}
