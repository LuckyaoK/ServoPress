using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ServoPress.Models;
using ServoPress.Services;
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
        private int _okCount;
        [ObservableProperty]
        private int _nokCount;

        [ObservableProperty]
        private double _yield;

        [ObservableProperty]
        public int _totalCount;
        //  曲线系列引用
        private LineSeries _curveSeries;


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

            _curveSeries = new LineSeries
            {
                Title = "Line0",
                Color = OxyColors.Black,
                StrokeThickness = 2
            };
            PlotModel.Series.Add(_curveSeries);
        }
        public void UpdateWithNewData(DataResult data)
        {
            // 1. 更新判定结果
            Result = data.Result; // OnResultChanged 会自动更新背景色

            // 2. 更新曲线
            _curveSeries.Points.Clear();
            _curveSeries.Points.AddRange(data.CurveData);
            PlotModel.InvalidatePlot(true); // 刷新图表

            // 3. 更新过程值
            // (确保 ProcessValues 集合已初始化)
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




        [RelayCommand]
        private void ClearCount()
        {
            OkCount = 0;
            NokCount = 0;
            Yield = 0;
        }


        /// <summary>
        /// (已修改)
        /// 当点击 "确认" 时，不再自己处理，而是发送一个全局消息
        /// 请求 MainWindowViewModel 来保存 *所有* 工位的设置。
        /// </summary>
        [RelayCommand]
        private void ConfirmChanges()
        {
            // 3. 发送保存请求消息
            WeakReferenceMessenger.Default.Send(new SaveAllUniboxesMessage());
        }

    }
}