using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;
using ServoPress.Models;
using ServoPress.Services;
using System.Collections.ObjectModel; 
using System.Linq;
using System.Windows;
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
        private string _stationName = "";

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
        
        [ObservableProperty]
        public ObservableCollection<EvalWindow> evalWindows;

        /// <summary>
        /// 矩形框和箭头集合
        /// </summary>
        public List<Unibox> Uniboxes { get; }=new List<Unibox>();
        // 统计
        [ObservableProperty]
        private int _okCount;
        [ObservableProperty]
        private int _ngCount;

        [ObservableProperty]
        private double _yield;

        [ObservableProperty]
        public int _totalCount;
        //  曲线系列引用
        private LineSeries _curveSeries;

        private readonly CurveBoxService _curveBoxService;
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


        public StationViewModel(int stationId, CurveBoxService curveBoxService)
        {
            Id=stationId;
            StationName = $"工位 {Id}";
            _curveBoxService = curveBoxService;
            // 初始化图表
            InitializePlot();

            // 1. 初始化 EvalWindows 集合 (防止为空)
            EvalWindows = new ObservableCollection<EvalWindow>();

            // 1. 加载特定工位的配置
            if (_curveBoxService != null)
            {
                var mySettings = _curveBoxService.GetSettingsForStation(Id);
                foreach (var window in mySettings)
                {
                    EvalWindows.Add(window);
                    AddAnnotationToPlot(window);
                }
            }


            // 初始化示例数据
            ProcessValues = new ObservableCollection<ProcessValue>
            {
                new ProcessValue { Name = "起始位移", Value = "0.0", Unit = "mm" },
                new ProcessValue { Name = "最终位移", Value = "0.0", Unit = "mm" },
                new ProcessValue { Name = "起始压力", Value = "0.0", Unit = "N" },
                new ProcessValue { Name = "最终压力", Value = "0.0", Unit = "N" },
                new ProcessValue { Name = "结果详情", Value = "", Unit = "" }
            };

            // 初始化 UniboxSettings
            UniboxSettings = new EvalWindow
            {
                Enabled = true,
                EntryDirection = "左进",
                ExitDirection = "右出",
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
                Maximum = 10
            });
            PlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "压力 (N)",
                Minimum = 0,
                Maximum = 50
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
        /// 同步最新配置回服务用于保存
        /// </summary>
        public void SyncDataToService()
        {
            if (_curveBoxService == null) return;
            // 更新 Service 中属于本工位的数据
            _curveBoxService.UpdateStationSettings(Id, EvalWindows.ToList());
        }

        /// <summary>
        /// 更新判定过程值图表
        /// </summary>
        /// <param name="data"></param>
        public void UpdateWithNewData(DataResult data)
        {
            // 1. 更新判定结果
            Result = data.Result ? "OK" : "NG"; 

            // 2. 更新曲线
            _curveSeries.Points.Clear();
            _curveSeries.Points.AddRange(data.CurveData);
            PlotModel.InvalidatePlot(true); // 刷新图表
            // 3. 更新过程值
            ProcessValues[0].Value = data.StartPosition.ToString("F2");
            ProcessValues[1].Value = data.EndPosition.ToString("F2");
            ProcessValues[2].Value = data.StartForce.ToString("F2");
            ProcessValues[3].Value = data.MaxForce.ToString("F2");
            ProcessValues[4].Value = data.ResultText;

            // 4. 更新统计
            if (data.Result)
            {
                OkCount++;
            }
            else
            {
                NgCount++;
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
                Name = $"UniBox {EvalWindows.Count + 1}",
                StartX = minX,
                EndX = maxX,
                StartY = minY,
                EndY = maxY,
                EntryDirection = UniboxSettings.EntryDirection,
                ExitDirection = UniboxSettings.ExitDirection,
                AllowReentry= UniboxSettings.AllowReentry
            };
            UniboxSettings = evalWindow;
            // 2. 数据存储
            EvalWindows.Add(evalWindow);

            // 3. 添加矩形框和箭头
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
                TextPosition = new DataPoint((window.StartX + window.EndX) / 2, window.EndY+ Math.Abs(window.StartY - window.EndY) * 0.15)
            };
            PlotModel.Annotations.Add(rect);

            BoxSide inBoxSide = new BoxSide();
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
            PlotModel.Annotations.Add(triangleAnnotation1);


            PolygonAnnotation triangleAnnotation2 = new PolygonAnnotation();
            //退出
            if (outBoxSide != BoxSide.None)
            {
                triangleAnnotation2 = CreatePoly(false, outBoxSide, triangleAnnotation2, window.StartX, window.EndX, window.StartY, window.EndY);
                PlotModel.Annotations.Add(triangleAnnotation2);
            }

            Uniboxes.Add(new Unibox
            {
                RectangleAnnotation = rect,
                InSideAnnotation = triangleAnnotation1,
                OutSideAnnotation=triangleAnnotation2
            }) ;
        }
        /// <summary>
        /// 创建箭头
        /// </summary>
        /// <param name="boxSide"></param>
        /// <param name="triangleAnnotation"></param>
        /// <returns></returns>
        private PolygonAnnotation CreatePoly(bool IsEnter, BoxSide boxSide, PolygonAnnotation triangleAnnotation, double startX, double endX, double startY, double endY)
        {
            #region 旧逻辑
            //triangleAnnotation = new PolygonAnnotation
            //{
            //    Fill = OxyColor.FromArgb(0x40, 0x00, 0x00, 0xFF), // 40%透明度的蓝色, 
            //    Stroke = OxyColor.FromArgb(0x40, 0x00, 0x00, 0xFF), // 40%透明度的蓝色,               
            //    StrokeThickness = 2,
            //    Layer = AnnotationLayer.BelowSeries
            //};

            //if (IsEnter)
            //{
            //    if (boxSide == BoxSide.Top)
            //    {
            //        double pointCenterX = (startX + endX) / 2;
            //        double length = (endX - startX) / 32;
            //        double y = Math.Sqrt(3) * length;
            //        triangleAnnotation.Points.Add(new DataPoint(pointCenterX - length, endY));
            //        triangleAnnotation.Points.Add(new DataPoint(pointCenterX + length, endY));
            //        triangleAnnotation.Points.Add(new DataPoint(pointCenterX, endY - y));
            //    }

            //    else if (boxSide == BoxSide.Bottom)
            //    {
            //        double pointCenterX = (startX + endX) / 2;
            //        double length = (endX - startX) / 32;

            //        double y = Math.Sqrt(3) * length;
            //        triangleAnnotation.Points.Add(new DataPoint(pointCenterX - length, startY));
            //        triangleAnnotation.Points.Add(new DataPoint(pointCenterX + length, startY));
            //        triangleAnnotation.Points.Add(new DataPoint(pointCenterX, startY + y));
            //    }

            //    else if (boxSide == BoxSide.Left)
            //    {
            //        double pointCenterY = (startY + endY) / 2;
            //        double length = (endY - startY) / 32;
            //        double x = Math.Sqrt(3) * length;
            //        triangleAnnotation.Points.Add(new DataPoint(startX, pointCenterY - length));
            //        triangleAnnotation.Points.Add(new DataPoint(startX, pointCenterY + length));
            //        triangleAnnotation.Points.Add(new DataPoint(startX + x, pointCenterY));
            //    }

            //    else if (boxSide == BoxSide.Right)
            //    {
            //        double pointCenterY = (startY + endY) / 2;
            //        double length = (endY - startY) / 32;
            //        double x = Math.Sqrt(3) * length;
            //        triangleAnnotation.Points.Add(new DataPoint(endX, pointCenterY - length));
            //        triangleAnnotation.Points.Add(new DataPoint(endX, pointCenterY + length));
            //        triangleAnnotation.Points.Add(new DataPoint(endX - x, pointCenterY));
            //    }
            //}

            //else
            //{
            //    if (boxSide == BoxSide.Top)
            //    {
            //        double pointCenterX = (startX + endX) / 2;
            //        double length = (endX - startX) / 32;
            //        double y = Math.Sqrt(3) * length;
            //        triangleAnnotation.Points.Add(new DataPoint(pointCenterX - length, endY));
            //        triangleAnnotation.Points.Add(new DataPoint(pointCenterX + length, endY));
            //        triangleAnnotation.Points.Add(new DataPoint(pointCenterX, endY + y));
            //    }

            //    else if (boxSide == BoxSide.Bottom)
            //    {
            //        double pointCenterX = (startX + endX) / 2;
            //        double length = (endX - startX) / 32;
            //        double y = Math.Sqrt(3) * length;
            //        triangleAnnotation.Points.Add(new DataPoint(pointCenterX - length, startY));
            //        triangleAnnotation.Points.Add(new DataPoint(pointCenterX + length, startY));
            //        triangleAnnotation.Points.Add(new DataPoint(pointCenterX, startY - y));
            //    }

            //    else if (boxSide == BoxSide.Left)
            //    {
            //        double pointCenterY = (startY + endY) / 2;
            //        double length = (endY - startY) / 32;
            //        double x = Math.Sqrt(3) * length;
            //        triangleAnnotation.Points.Add(new DataPoint(startX, pointCenterY - length));
            //        triangleAnnotation.Points.Add(new DataPoint(startX, pointCenterY + length));
            //        triangleAnnotation.Points.Add(new DataPoint(startX - x, pointCenterY));
            //    }

            //    else if (boxSide == BoxSide.Right)
            //    {
            //        double pointCenterY = (startY + endY) / 2;
            //        double length = (endY - startY) / 32;
            //        double x = Math.Sqrt(3) * length;
            //        triangleAnnotation.Points.Add(new DataPoint(endX, pointCenterY - length));
            //        triangleAnnotation.Points.Add(new DataPoint(endX, pointCenterY + length));
            //        triangleAnnotation.Points.Add(new DataPoint(endX + x, pointCenterY));
            //    }

            //}

            //return triangleAnnotation;
            #endregion

            // 为了保证箭头在视觉上协调，我们根据 Box 的尺寸动态计算箭头大小
            // 例如：长度取 Box 宽度的 15%，宽度取 Box 高度的 10% (近似等边视觉效果)
            double boxWidth = Math.Abs(endX - startX);
            double boxHeight = Math.Abs(endY - startY);

            // 设置最小尺寸，防止 Box 太小时箭头消失
            double lenX = Math.Max(boxWidth * 0.15, boxWidth > 0 ? boxWidth * 0.15 : 1.0);
            double lenY = Math.Max(boxHeight * 0.15, boxHeight > 0 ? boxHeight * 0.15 : 10.0);



            DataPoint tip = new DataPoint(0, 0);  // 箭头顶点
            DataPoint p1 = new DataPoint(0, 0);   // 底边角点1
            DataPoint p2 = new DataPoint(0, 0);   // 底边角点2

            double cx = (startX + endX) / 2;
            double cy = (startY + endY) / 2;

            // 定义等边三角形的比例因子：底边宽度 = 高度 * (2 / sqrt(3)) ≈ 1.155
            // 但因为XY轴比例不同，我们这里用 lenX 和 lenY 分别作为基准

            bool isGenerated = false;

            // 处理 EntryDirection
            if (IsEnter)
            {
                switch (boxSide)
                {
                    case BoxSide.Left: // 从左边进入，箭头在左边框，指向右(内部)
                        tip = new DataPoint(startX + lenX, cy);
                        p1 = new DataPoint(startX, cy - lenY / 2);
                        p2 = new DataPoint(startX, cy + lenY / 2);
                        isGenerated = true;
                        break;
                    case BoxSide.Right: // 从右边进入，箭头在右边框，指向左(内部)
                        tip = new DataPoint(endX - lenX, cy);
                        p1 = new DataPoint(endX, cy - lenY / 2);
                        p2 = new DataPoint(endX, cy + lenY / 2);
                        isGenerated = true;
                        break;
                    case BoxSide.Top: // 从上方进入，箭头在上边框，指向下
                        tip = new DataPoint(cx, endY - lenY);
                        p1 = new DataPoint(cx - lenX / 2, endY); 
                        p2 = new DataPoint(cx + lenX / 2, endY);
                        isGenerated = true;
                        break;
                    case BoxSide.Bottom: // 从下方进入，箭头在下边框，指向上
                        tip = new DataPoint(cx, startY+lenY);
                        p1 = new DataPoint(cx - lenX / 2, startY);
                        p2 = new DataPoint(cx + lenX / 2, startY);
                        isGenerated = true;
                        break;
                }
            }
            
            else
            {
                // 退出箭头的逻辑：
                switch (boxSide)
                {
                    case BoxSide.Left: // 向左退出，箭头在左边框，指向左(外部)
                        tip = new DataPoint(startX - lenX, cy); // 顶点向外延伸
                        p1 = new DataPoint(startX, cy - lenY / 2); // 底边在框上
                        p2 = new DataPoint(startX, cy + lenY / 2);
                        isGenerated = true;
                        break;
                    case BoxSide.Right: // 向右退出，箭头在右边框，指向右(外部)
                        tip = new DataPoint(endX + lenX, cy);
                        p1 = new DataPoint(endX, cy - lenY / 2);
                        p2 = new DataPoint(endX, cy + lenY / 2);
                        isGenerated = true;
                        break;
                    case BoxSide.Top: // 向上退出
                        tip = new DataPoint(cx, endY + lenY);
                        p1 = new DataPoint(cx - lenX / 2, endY);
                        p2 = new DataPoint(cx + lenX / 2, endY);
                        isGenerated = true;
                        break;
                    case BoxSide.Bottom: // 向下退出
                        tip = new DataPoint(cx, startY - lenY);
                        p1 = new DataPoint(cx - lenX / 2, startY);
                        p2 = new DataPoint(cx + lenX / 2, startY);
                        isGenerated = true;
                        break;
                }
            }

            if (!isGenerated) return null;

            var poly = new PolygonAnnotation
            {
                Fill = OxyColor.FromArgb(0x40, 0x00, 0x00, 0xFF), // 40%透明度的蓝色, 
                Stroke = OxyColor.FromArgb(0x40, 0x00, 0x00, 0xFF), // 40%透明度的蓝色,               
                StrokeThickness = 2,
                Layer = AnnotationLayer.BelowSeries
            };

            poly.Points.Add(tip);
            poly.Points.Add(p1);
            poly.Points.Add(p2);

            return poly;
        }
        #endregion


        #region 命令绑定
        [RelayCommand]
        private void ClearCount()
        {
            OkCount = 0;
            NgCount = 0;
            Yield = 0;
        }


        [RelayCommand]
        private void SaveConfig()
        {
            // 发送保存请求消息
            WeakReferenceMessenger.Default.Send(new SaveAllUniboxesMessage());
        }

        [RelayCommand]
        private void RemoveBox()
        {
            try
            {
                int count = Uniboxes.Count;
                var values = PlotModel.Annotations.Where(p => p == Uniboxes[count - 1].RectangleAnnotation
                || p == Uniboxes[count - 1].InSideAnnotation
                || p == Uniboxes[count - 1].OutSideAnnotation).
                ToList();

                foreach (var item in values)
                {
                    PlotModel.Annotations.Remove(item);
                }

                EvalWindows.RemoveAt(EvalWindows.Count - 1);
                Uniboxes.RemoveAt(Uniboxes.Count - 1);
                // 刷新图表
                PlotModel.InvalidatePlot(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion
    }
}