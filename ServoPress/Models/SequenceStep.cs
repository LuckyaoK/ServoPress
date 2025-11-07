using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServoPress.Models
{
    public class SequenceStep
    {
        public int Step { get; set; }
        public string Command { get; set; }
        public string Params { get; set; }
    }

}
