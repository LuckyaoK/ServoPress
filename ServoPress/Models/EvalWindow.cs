using CommunityToolkit.Mvvm.ComponentModel;
using OxyPlot.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServoPress.Models
{
    public enum WindowType
    {
        UniBox,
        NoPass
    }
    /// <summary>
    /// 过程值
    /// </summary>
    public partial class ProcessValue : ObservableObject
    {
        [ObservableProperty]
        public string _name;

        [ObservableProperty]
        public string _value;

        [ObservableProperty]
        public string _unit;
    }

    /// <summary>
    /// 评估窗口
    /// </summary>
    public class EvalWindow
    {
        public bool Enabled { get; set; }
        public string Name { get; set; }
        public WindowType Type { get; set; }
        /// <summary>
        /// 起始位移
        /// </summary>
        public double StartX { get; set; }
        /// <summary>
        /// 最终位移
        /// </summary>
        public double EndX { get; set; }
        /// <summary>
        /// 起始压力
        /// </summary>
        public double StartY { get; set; }
        /// <summary>
        /// 最终压力
        /// </summary>
        public double EndY { get; set; }
        public string EntryDirection { get; set; }
        public string ExitDirection { get; set; }
        public bool AllowReentry { get; set; }
        public bool AllowJudge { get; set; }
    }
    public class Unibox
    {
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

    public class NoPass
    {
        public RectangleAnnotation RectangleAnnotation { get; set; }

    }
}
