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
using System.Windows.Documents;
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

        // 单个NoPass设置
        [ObservableProperty]
        private EvalWindow _noPassSettings;

        //所有评估窗口的集合
        public List<EvalWindow> EvalWindows { get; set; }

        // 1. 添加方向选项集合 (供 View 中的 ComboBox 绑定)
        public ObservableCollection<string> EntryDirectionsOptions{ get; }= new ObservableCollection<string> { "上进", "下进", "左进", "右进", "不进"};
        public ObservableCollection<string> ExitDirectionsOptions { get; } = new ObservableCollection<string> { "上出", "下出", "左出", "右出", "不出" };
        
       
        /// <summary>
        /// Unibox矩形框和箭头集合
        /// </summary>
        public List<Unibox> Uniboxes { get; }=new List<Unibox>();
        /// <summary>
        /// NoPass直线和标注
        /// </summary>
        public List<NoPass> NoPasses { get; } = new List<NoPass>();
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
            EvalWindows = new List<EvalWindow>();

            // 1. 加载特定工位的评估窗口配置
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
                new ProcessValue { Name = "最小位移", Value = "0.0", Unit = "mm" },
                new ProcessValue { Name = "最大位移", Value = "0.0", Unit = "mm" },
                new ProcessValue { Name = "最终位移", Value = "0.0", Unit = "mm" },
                new ProcessValue { Name = "最小压力", Value = "0.0", Unit = "N" },
                new ProcessValue { Name = "最大压力", Value = "0.0", Unit = "N" },
                new ProcessValue { Name = "最终压力", Value = "0.0", Unit = "N" },
                new ProcessValue { Name = "结果详情", Value = "", Unit = "" }
            };
         
            // 初始化 UniboxSettings
            UniboxSettings = new EvalWindow
            {
                Enabled = true,
                EntryDirection = "左进",
                ExitDirection = "右出",
                AllowReentry = true,
                AllowJudge = true
            };

            // 初始化 NoPassSettings
            NoPassSettings = new EvalWindow
            {
                Enabled = true,
                AllowJudge = true,
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
            ProcessValues[0].Value = data.MinPosition.ToString("F2");
            ProcessValues[1].Value = data.MaxPosition.ToString("F2");
            ProcessValues[2].Value = data.EndPosition.ToString("F2");
            ProcessValues[3].Value = data.MinForce.ToString("F2");
            ProcessValues[4].Value = data.MaxForce.ToString("F2");
            ProcessValues[5].Value = data.EndForce.ToString("F2");
            ProcessValues[6].Value = data.ResultText;

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
        /// 手动拖拽创建评估窗口
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
                Name = $"UniBox {Uniboxes.Count + 1}",
                StartX = minX,
                EndX = maxX,
                StartY = minY,
                EndY = maxY,
                EntryDirection = UniboxSettings.EntryDirection,
                ExitDirection = UniboxSettings.ExitDirection,
                AllowReentry= UniboxSettings.AllowReentry,
                AllowJudge= UniboxSettings.AllowJudge
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
        /// 创建评估窗口
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
                default:
                    inBoxSide = BoxSide.None; break;
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
                default:
                    outBoxSide = BoxSide.None; break;
            }

            PolygonAnnotation triangleAnnotation1 = new PolygonAnnotation();
            //进入
            if(inBoxSide !=BoxSide.None)
            {
                triangleAnnotation1 = _curveBoxService.CreatePoly(true, inBoxSide, triangleAnnotation1, window.StartX, window.EndX, window.StartY, window.EndY);
                PlotModel.Annotations.Add(triangleAnnotation1);
            }

            PolygonAnnotation triangleAnnotation2 = new PolygonAnnotation();
            //退出
            if (outBoxSide != BoxSide.None)
            {
                triangleAnnotation2 = _curveBoxService.CreatePoly(false, outBoxSide, triangleAnnotation2, window.StartX, window.EndX, window.StartY, window.EndY);
                PlotModel.Annotations.Add(triangleAnnotation2);
            }
            if(window.Type==WindowType.UniBox)
            {
                Uniboxes.Add(new Unibox
                {
                    RectangleAnnotation = rect,
                    InSideAnnotation = triangleAnnotation1,
                    OutSideAnnotation = triangleAnnotation2
                });
            }

            else if(window.Type == WindowType.NoPass)
            {
                NoPasses.Add(new NoPass
                {
                    RectangleAnnotation = rect
                });
            }

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
        private void AddUniBox()
        {
            // 创建数据模型
            var evalWindow = new EvalWindow
            {
                Enabled = true,
                Name = $"UniBox {Uniboxes.Count + 1}",
                Type = WindowType.UniBox,
                StartX = UniboxSettings.StartX,
                EndX = UniboxSettings.EndX,
                StartY = UniboxSettings.StartY,
                EndY = UniboxSettings.EndY,
                EntryDirection = UniboxSettings.EntryDirection,
                ExitDirection = UniboxSettings.ExitDirection,
                AllowReentry = UniboxSettings.AllowReentry,
                AllowJudge = UniboxSettings.AllowJudge
            };
            //数据存储
            EvalWindows.Add(evalWindow);

            //添加矩形框和箭头
            AddAnnotationToPlot(evalWindow);

            //刷新图表
            PlotModel.InvalidatePlot(true);
     
        }

        [RelayCommand]
        private void RemoveUniBox()
        {
            try
            {
                int count = Uniboxes.Count;
                var values = PlotModel.Annotations.Where(p => p == Uniboxes[count - 1].RectangleAnnotation
                || p == Uniboxes[count - 1].InSideAnnotation
                || p == Uniboxes[count - 1].OutSideAnnotation).
                ToList();

                //移除UI
                foreach (var item in values)
                {
                    PlotModel.Annotations.Remove(item);
                }
                //移除
                var itemToRemove = EvalWindows.LastOrDefault(x => x.Type == WindowType.UniBox);
                EvalWindows.Remove(itemToRemove);
                Uniboxes.RemoveAt(Uniboxes.Count - 1);
                // 刷新图表
                PlotModel.InvalidatePlot(true);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }



        [RelayCommand]
        private void SaveConfig()
        {
          
            // 发送保存请求消息
            WeakReferenceMessenger.Default.Send(new SaveAllUniboxesMessage());
        }


        [RelayCommand]
        private void AddNoPass()
        {
            //创建NoPass窗口配置用于传入服务
            var evalWindow = new EvalWindow
            {
                Enabled = true,
                Name = $"NoPass {NoPasses.Count + 1}",
                Type = WindowType.NoPass,
                StartX = NoPassSettings.StartX,
                EndX = NoPassSettings.EndX,
                StartY = NoPassSettings.EndY,
                EndY = NoPassSettings.EndY,
                AllowJudge = NoPassSettings.AllowJudge
            };

            //数据存储
            EvalWindows.Add(evalWindow);
            //创建NoPass直线标注
            AddAnnotationToPlot(evalWindow);
            //刷新图表
            PlotModel.InvalidatePlot(true);

        }


        [RelayCommand]
        private void RemoveNoPass()
        {
            try
            {
                int count = NoPasses.Count;
                var values = PlotModel.Annotations.Where(p => p == NoPasses[count - 1].RectangleAnnotation).ToList();

                //移除UI
                foreach (var item in values)
                {
                    PlotModel.Annotations.Remove(item);
                }

                var itemToRemove = EvalWindows.LastOrDefault(x => x.Type == WindowType.NoPass);
                EvalWindows.Remove(itemToRemove);
                NoPasses.RemoveAt(NoPasses.Count - 1);
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