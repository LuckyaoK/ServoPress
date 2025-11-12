using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ServoPress.Models;
using System.Collections.ObjectModel; // 保留，为 ProcessValues 和 ComboBox 选项
using System.Linq;
using System.Windows.Media;

namespace ServoPress.ViewModels
{
    /// <summary>
    /// 单个工位的 ViewModel
    /// </summary>
    public partial class StationViewModel : ObservableObject
    {
        // 工位ID
        public int Id { get; set; }

        // 工位名称
        [ObservableProperty]
        private string _stationName = "Station";

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

        // 最终判定结果
        [ObservableProperty]
        private string _result = ""; // OK, NOK, WAIT

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
        // 判定结果的背景色
        [ObservableProperty]
        private Brush _resultBrush = Brushes.Gray;

        // OxyPlot 图表模型
        [ObservableProperty]
        private PlotModel _plotModel;

        // 过程值集合 (保留)
        [ObservableProperty]
        private ObservableCollection<ProcessValue> _processValues;

        // 新增：单个 Unibox 设置
        [ObservableProperty]
        private EvalWindow _uniboxSettings;

        // 新增：进入方向选项
        public ObservableCollection<string> EntryDirectionsOptions { get; }

        // 新增：退出方向选项
        public ObservableCollection<string> ExitDirectionsOptions { get; }

        // 统计 (保留)
        [ObservableProperty]
        private int _okCount = 0;
        [ObservableProperty]
        private int _nokCount = 0;
        [ObservableProperty]
        private int _totalCount = 0;
        [ObservableProperty]
        private double _yield = 100.0;

        public StationViewModel()
        {
            // 初始化图表
            InitializePlot();

            // 初始化 ComboBox 选项
            EntryDirectionsOptions = new ObservableCollection<string> {  "上进", "下进", "左进", "右进" };
            ExitDirectionsOptions = new ObservableCollection<string> {  "上出", "下出", "左出", "右出" ,"不出", };

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

            // 生成示例曲线
            GenerateSampleCurve();
        }

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
                Minimum = -2,
                Maximum = 16
            });
            PlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "压力 (N)",
                Minimum = -2,
                Maximum = 16
            });
        }

        private void GenerateSampleCurve()
        {
            var lineSeries = new LineSeries
            {
                Title = "Line0",
                Color = OxyColors.Black,
                StrokeThickness = 2
            };

            // 模拟截图中的曲线
            lineSeries.Points.Add(new DataPoint(0, 1));
            lineSeries.Points.Add(new DataPoint(1, 6));
            lineSeries.Points.Add(new DataPoint(1.5, 7.5));
            lineSeries.Points.Add(new DataPoint(2, 7));
            lineSeries.Points.Add(new DataPoint(2.5, 8));
            lineSeries.Points.Add(new DataPoint(3, 11));

            PlotModel.Series.Add(lineSeries);
            PlotModel.InvalidatePlot(true); // 刷新图表
        }

        [RelayCommand]
        private void ClearCount()
        {
            OkCount = 0;
            NokCount = 0;
            TotalCount = 0;
            Yield = 100.0;
        }


        [RelayCommand]
        private void ConfirmChanges()
        {
            // TODO: 在此实现 "确认" 逻辑
            // 例如，将 UniboxSettings 的更改发送到硬件或保存
            OnPropertyChanged(nameof(UniboxSettings));
        }
    }
}