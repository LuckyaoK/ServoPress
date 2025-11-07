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
}
