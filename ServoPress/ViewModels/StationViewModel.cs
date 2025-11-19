using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using ServoPress.Models;
using ServoPress.Services;
using System.Collections.ObjectModel; 
using System.Linq;
using System.Windows.Media;

namespace ServoPress.ViewModels
{
    /// <summary>
    /// 单个工位的 ViewModel
    /// </summary>
    public partial class StationViewModel : ObservableObject
    {
        #region 工位属性
        // 工位ID
        public int Id { get; set; }

        // 工位名称
        [ObservableProperty]
        private string _stationName = "Station";

        // 最终判定结果
        [ObservableProperty]
        private string _result = ""; 

        // 判定结果的背景色
        [ObservableProperty]
        private Brush _resultBrush = Brushes.Gray;

        // OxyPlot 图表模型
        [ObservableProperty]
        private PlotModel _plotModel;

        // 过程值集合 (保留)
        [ObservableProperty]
        private ObservableCollection<ProcessValue> _processValues;

        // 单个 Unibox 设置
        [ObservableProperty]
        private EvalWindow _uniboxSettings;

        // 1. 添加方向选项集合 (供 View 中的 ComboBox 绑定)
        public ObservableCollection<string> EntryDirectionsOptions{ get; }= new ObservableCollection<string> { "上进", "下进", "左进", "右进" };
        public ObservableCollection<string> ExitDirectionsOptions { get; } = new ObservableCollection<string> { "上出", "下出", "左出", "右出", "不出" };
        public ObservableCollection<EvalWindow> EvalWindows { get; } = new ObservableCollection<EvalWindow>();
        // 2. 添加当前选中的方向属性
        [ObservableProperty]
        private string _selectedEntryDirection = "左进"; // 默认值

        [ObservableProperty]
        private string _selectedExitDirection = "右出"; // 默认值

        // 统计
        [ObservableProperty]
        private int _okCount;
        [ObservableProperty]
        private int _nokCount;

        [ObservableProperty]
        private double _yield;

        [ObservableProperty]
        public int _totalCount;
        //  曲线系列引用
        private LineSeries _curveSeries;

     
        /// <summary>
        /// 自动生成的 OnResultChanged 方法，在 _result 属性更新时触发
        /// </summary>
        partial void OnResultChanged(string value)
        {
            switch (value?.ToUpper())
            {
                case "OK":
                    ResultBrush = Brushes.Green; // 或者使用 #FF3D8B3D
                    break;
                case "NG":
                    ResultBrush = Brushes.Red; // 或者使用 #FFD9534F
                    break;
                default:
                    ResultBrush = Brushes.Gray;
                    break;
            }
        }

        /// <summary>
        /// 当 StationName 属性更改时，自动更新图表标题
        /// </summary>
        partial void OnStationNameChanged(string value)
        {
            if (PlotModel != null)
            {
                PlotModel.Title = value;
                PlotModel.InvalidatePlot(true); // 刷新图表以显示新标题
            }
        }
        #endregion

        public StationViewModel()
        {
            // 初始化图表
            InitializePlot();

            // 初始化示例数据
            ProcessValues = new ObservableCollection<ProcessValue>
            {
                new ProcessValue { Name = "起始位移", Value = "0.0", Unit = "mm" },
                new ProcessValue { Name = "最终位移", Value = "0.0", Unit = "mm" },
                new ProcessValue { Name = "起始压力", Value = "0.0", Unit = "N" },
                new ProcessValue { Name = "最终压力", Value = "0.0", Unit = "N" }
            };

            // 初始化 UniboxSettings
            UniboxSettings = new EvalWindow
            {
                Enabled = true,
                Name = "Unibox 0", // Name 可能不再需要显示，但保留
                Type = "UniBox",

                StartX = 2.65,
                EndX = 4.58,
                StartY = 12.40,
                EndY = 9.80,
                EntryDirection = "下进",
                ExitDirection = "不出",
                AllowReentry = true
            };
        }

        #region 图表操作
        /// <summary>
        /// 初始化图表
        /// </summary>
        private void InitializePlot()   
        {
            PlotModel = new PlotModel
            {
                Title = this.StationName, // 将标题绑定到 StationName
            };

            PlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "位移 (mm)",
                Minimum = 0,
                Maximum = 100
            });
            PlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "压力 (N)",
                Minimum = 0,
                Maximum = 5000
            });

            _curveSeries = new LineSeries
            {
                Title = "Line0",
                Color = OxyColors.Black,
                StrokeThickness = 2
            };
            PlotModel.Series.Add(_curveSeries);
        }


        /// <summary>
        /// 更新图表
        /// </summary>
        /// <param name="data"></param>
        public void UpdateWithNewData(DataResult data)
        {
            // 1. 更新判定结果
            Result = data.Result; // OnResultChanged 会自动更新背景色

            // 2. 更新曲线
            _curveSeries.Points.Clear();
            _curveSeries.Points.AddRange(data.CurveData);
            PlotModel.InvalidatePlot(true); // 刷新图表
            // 3. 更新过程值
            ProcessValues[0].Value = data.StartPosition.ToString("F2");
            ProcessValues[1].Value = data.EndPosition.ToString("F2");
            ProcessValues[2].Value = data.StartForce.ToString("F2");
            ProcessValues[3].Value = data.MaxForce.ToString("F2");

            // 4. 更新统计
            if (data.IsOk)
            {
                OkCount++;
            }
            else
            {
                NokCount++;
            }
            TotalCount++;
            Yield = (double)OkCount / TotalCount * 100.0;
        }



        /// <summary>
        /// 创建评估窗口UniBox
        /// </summary>
        /// <param name="minX"></param>
        /// <param name="maxX"></param>
        /// <param name="minY"></param>
        /// <param name="maxY"></param>
        public void CreateNewEvalWindow(double minX, double maxX, double minY, double maxY)
        {
            // 1. 业务逻辑：创建数据模型
            var evalWindow = new EvalWindow
            {
                Enabled = true,
                Name = $"Window {EvalWindows.Count + 1}",
                StartX = minX,
                EndX = maxX,
                StartY = minY,
                EndY = maxY,
                EntryDirection = SelectedEntryDirection,
                ExitDirection = SelectedExitDirection
            };

            // 2. 数据存储
            EvalWindows.Add(evalWindow);

            // 3. 更新图表 (创建注解)
            AddAnnotationToPlot(evalWindow);

            // 4. 刷新图表
            PlotModel.InvalidatePlot(true);
        }
      
        /// <summary>
        /// 创建Box
        /// </summary>
        /// <param name="window"></param>
        private void AddAnnotationToPlot(EvalWindow window)
        {
            // 创建矩形
            var rect = new RectangleAnnotation
            {
                MinimumX = window.StartX,
                MaximumX = window.EndX,
                MinimumY = window.StartY,
                MaximumY = window.EndY,
                Fill = OxyColor.FromArgb(0x40, 0x00, 0xFF, 0x00),
                Stroke = OxyColors.Green,
                StrokeThickness = 1,
                Text = window.Name,
                TextPosition = new DataPoint((window.StartX + window.EndX) / 2, (window.StartY + window.EndY) / 2)
            };
          

            BoxSide inBoxSide=new BoxSide();
            switch (window.EntryDirection)
            {
                case "上进":
                    inBoxSide = BoxSide.Top; break;
                case "下进":
                    inBoxSide = BoxSide.Bottom; break;
                case "左进":
                    inBoxSide = BoxSide.Left; break;
                case "右进":
                    inBoxSide = BoxSide.Right; break;
            }

            BoxSide outBoxSide = new BoxSide();
            switch (window.ExitDirection)
            {
                case "上出":
                    outBoxSide = BoxSide.Top; break;
                case "下出":
                    outBoxSide = BoxSide.Bottom; break;
                case "左出":
                    outBoxSide = BoxSide.Left; break;
                case "右出":
                    outBoxSide = BoxSide.Right; break;
                case "不出":
                    outBoxSide = BoxSide.None; break;
            }

            PolygonAnnotation triangleAnnotation1 = new PolygonAnnotation();
            //进入
            triangleAnnotation1 = CreatePoly(true, inBoxSide, triangleAnnotation1, window.StartX, window.EndX, window.StartY, window.EndY);

            PolygonAnnotation triangleAnnotation2 = new PolygonAnnotation();
            //退出
            triangleAnnotation2 = CreatePoly(false, outBoxSide, triangleAnnotation2, window.StartX, window.EndX, window.StartY, window.EndY);

            PlotModel.Annotations.Add(rect);
            PlotModel.Annotations.Add(triangleAnnotation1);
            PlotModel.Annotations.Add(triangleAnnotation2);
        }


        /// <summary>
        /// 创建箭头
        /// </summary>
        /// <param name="boxSide"></param>
        /// <param name="triangleAnnotation"></param>
        /// <returns></returns>
        private PolygonAnnotation CreatePoly(bool IsEnter, BoxSide boxSide, PolygonAnnotation triangleAnnotation, double startX, double endX, double startY, double endY)
        {
            triangleAnnotation = new PolygonAnnotation
            {
                Fill = OxyColor.FromArgb(0x40, 0x00, 0x00, 0xFF), // 40%透明度的蓝色, 
                Stroke = OxyColor.FromArgb(0x40, 0x00, 0x00, 0xFF), // 40%透明度的蓝色,               
                StrokeThickness = 2,
                Layer = AnnotationLayer.BelowSeries
            };

            if (IsEnter)
            {
                if (boxSide == BoxSide.Top)
                {
                    double pointCenterX = (startX + endX) / 2;
                    double length = (endX - startX) / 32;
                    double y = Math.Sqrt(3) * length;
                    triangleAnnotation.Points.Add(new DataPoint(pointCenterX - length, endY));
                    triangleAnnotation.Points.Add(new DataPoint(pointCenterX + length, endY));
                    triangleAnnotation.Points.Add(new DataPoint(pointCenterX, endY - y));
                }

                else if (boxSide == BoxSide.Bottom)
                {
                    double pointCenterX = (startX + endX) / 2;
                    double length = (endX - startX) / 32;

                    double y = Math.Sqrt(3) * length;
                    triangleAnnotation.Points.Add(new DataPoint(pointCenterX - length, startY));
                    triangleAnnotation.Points.Add(new DataPoint(pointCenterX + length, startY));
                    triangleAnnotation.Points.Add(new DataPoint(pointCenterX, startY + y));
                }

                else if (boxSide == BoxSide.Left)
                {
                    double pointCenterY = (startY + endY) / 2;
                    double length = (endY - startY) / 32;
                    double x = Math.Sqrt(3) * length;
                    triangleAnnotation.Points.Add(new DataPoint(startX, pointCenterY - length));
                    triangleAnnotation.Points.Add(new DataPoint(startX, pointCenterY + length));
                    triangleAnnotation.Points.Add(new DataPoint(startX + x, pointCenterY));
                }

                else if (boxSide == BoxSide.Right)
                {
                    double pointCenterY = (startY + endY) / 2;
                    double length = (endY - startY) / 32;
                    double x = Math.Sqrt(3) * length;
                    triangleAnnotation.Points.Add(new DataPoint(endX, pointCenterY - length));
                    triangleAnnotation.Points.Add(new DataPoint(endX, pointCenterY + length));
                    triangleAnnotation.Points.Add(new DataPoint(endX - x, pointCenterY));
                }
            }

            else
            {
                if (boxSide == BoxSide.Top)
                {
                    double pointCenterX = (startX + endX) / 2;
                    double length = (endX - startX) / 32;
                    double y = Math.Sqrt(3) * length;
                    triangleAnnotation.Points.Add(new DataPoint(pointCenterX - length, endY));
                    triangleAnnotation.Points.Add(new DataPoint(pointCenterX + length, endY));
                    triangleAnnotation.Points.Add(new DataPoint(pointCenterX, endY + y));
                }

                else if (boxSide == BoxSide.Bottom)
                {
                    double pointCenterX = (startX + endX) / 2;
                    double length = (endX - startX) / 32;
                    double y = Math.Sqrt(3) * length;
                    triangleAnnotation.Points.Add(new DataPoint(pointCenterX - length, startY));
                    triangleAnnotation.Points.Add(new DataPoint(pointCenterX + length, startY));
                    triangleAnnotation.Points.Add(new DataPoint(pointCenterX, startY - y));
                }

                else if (boxSide == BoxSide.Left)
                {
                    double pointCenterY = (startY + endY) / 2;
                    double length = (endY - startY) / 32;
                    double x = Math.Sqrt(3) * length;
                    triangleAnnotation.Points.Add(new DataPoint(startX, pointCenterY - length));
                    triangleAnnotation.Points.Add(new DataPoint(startX, pointCenterY + length));
                    triangleAnnotation.Points.Add(new DataPoint(startX - x, pointCenterY));
                }

                else if (boxSide == BoxSide.Right)
                {
                    double pointCenterY = (startY + endY) / 2;
                    double length = (endY - startY) / 32;
                    double x = Math.Sqrt(3) * length;
                    triangleAnnotation.Points.Add(new DataPoint(endX, pointCenterY - length));
                    triangleAnnotation.Points.Add(new DataPoint(endX, pointCenterY + length));
                    triangleAnnotation.Points.Add(new DataPoint(endX + x, pointCenterY));
                }

            }

            return triangleAnnotation;
        }
        #endregion


        #region 命令绑定
        [RelayCommand]
        private void ClearCount()
        {
            OkCount = 0;
            NokCount = 0;
            Yield = 0;
        }


        [RelayCommand]
        private void ConfirmChanges()
        {
            // 发送保存请求消息
            WeakReferenceMessenger.Default.Send(new SaveAllUniboxesMessage());
        }
        #endregion
    }
}