using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServoPress.Models
{
    public class EvalWindow
    {
        public bool Enabled { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }

        public double StartX { get; set; }
        public double EndX { get; set; }
        public double StartY { get; set; }
        public double EndY { get; set; }
        public string EntryDirection { get; set; }
        public string ExitDirection { get; set; }
        public bool AllowReentry { get; set; }
    }
}
