using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServoPress.Models
{
    public class HistoryItem
    {
        public string Timestamp { get; set; }
        public string Program { get; set; }
        public string Result { get; set; }
        public double MaxForce { get; set; }
    }

    public class EvalWindow
    {
        public bool Enabled { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Params { get; set; }
    }

    public class PValueConfig
    {
        public string Name { get; set; }
        public string Method { get; set; }
        public string Param { get; set; }
    }

    public class ProcessValue
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
    }

}
