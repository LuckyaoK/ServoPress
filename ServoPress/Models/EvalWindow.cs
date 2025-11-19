using OxyPlot.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServoPress.Models
{
    /// <summary>
    /// 评估窗口
    /// </summary>
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

        public RectangleAnnotation RectangleAnnotation { get; set; }

        /// <summary>
        /// 进入方向标注箭头
        /// </summary>
        public PolygonAnnotation InSideAnnotation { get; set; }

        /// <summary>
        /// 退出方向标注箭头
        /// </summary>
        public PolygonAnnotation OutSideAnnotation { get; set; }
    }
}
