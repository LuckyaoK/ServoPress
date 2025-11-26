using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ServoPress.Database.Entities;
using ServoPress.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace ServoPress.ViewModels
{
   
    public partial class HistoryViewModel : ObservableObject
    {
        private readonly DataStorageService _dataService;

        public HistoryViewModel()
        {
            _dataService = new DataStorageService();
            // 默认查最近一天
            EndDate = DateTime.Now.Date;
            StartDate = DateTime.Now.Date.AddDays(-1);

            // 初始化一个空的图表模型
            InitPlotModel();
        }

        // --- 查询条件属性 ---
        [ObservableProperty]
        private DateTime _startDate;

        [ObservableProperty]
        private DateTime _endDate;

        [ObservableProperty]
        private string _productModelKeyword;

        [ObservableProperty]
        private int _selectedStationIndex = 0; // 0=全部

        [ObservableProperty]
        private int _selectedResultIndex = 0; // 0=全部, 1=OK, 2=NG

        // --- 数据列表 ---
        [ObservableProperty]
        private ObservableCollection<ProductionRecord> _historyRecords = new ObservableCollection<ProductionRecord>();

        [ObservableProperty]
        private ProductionRecord _selectedRecord;

        // --- 图表模型 ---
        [ObservableProperty]
        private PlotModel _curveModel;


        /// <summary>
        /// 查询按钮命令
        /// </summary>
        [RelayCommand]
        public void Search()
        {
            try
            {
                DateTime searchStart = StartDate.Date;
                DateTime searchEnd = EndDate.Date.AddDays(1).AddSeconds(-1);
                var list = _dataService.QueryHistory(searchStart, searchEnd, SelectedStationIndex, SelectedResultIndex, ProductModelKeyword);
                HistoryRecords = new ObservableCollection<ProductionRecord>(list);

                if (list.Count == 0)
                {
                    MessageBox.Show("未找到符合条件的记录。");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询出错: {ex.Message}");
            }
        }


        /// <summary>
        /// 导出
        /// </summary>
        [RelayCommand]
        public void Export()
        {
            if (HistoryRecords == null || HistoryRecords.Count == 0)
            {
                MessageBox.Show("当前列表中没有数据可导出，请先进行查询。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV 文件 (*.csv)|*.csv",
                FileName = $"History_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                Title = "导出历史数据"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var sb = new StringBuilder();

                    // 写入表头
                    sb.AppendLine("ID,序列号,工站,产品型号,时间,结果,判定," +
                        "最大压力(N),最小压力(N),最终压力(N),最大位移(mm),最小位移(mm),最终位移(mm),测试点");

                    // 写入数据行
                    foreach (var item in HistoryRecords)
                    {
                        string res = item.ResulEnd ? "OK" : "NG";
                        sb.AppendLine($"{item.Id},{item.SerialNumber},{item.StationId},{item.ProductModel},{item.Timestamp:yyyy-MM-dd HH:mm:ss},{res}" +
                        $"{item.MaxForce},{item.MinForce},{item.EndForce},{item.MaxPosition},{item.MinPosition},{item.EndPosition},{item.CurveDataJson}");
                    }

                    // 使用 UTF8 编码写入文件 (带BOM，以便Excel正确识别中文)
                    File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);

                    MessageBox.Show("导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 双击表格行命令
        /// </summary>
        [RelayCommand]
        public void ShowCurve(ProductionRecord record)
        {
            if (record == null) return;
            UpdateChart(record);
        }




        // --- 辅助方法 ---

        private void InitPlotModel()
        {
            var model = new PlotModel { Title = "历史曲线" };
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "位移 (mm)" });
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "压力 (N)" });
            CurveModel = model;
        }

        private void UpdateChart(ProductionRecord record)
        {
            if (string.IsNullOrEmpty(record.CurveDataJson)) return;

            try
            {
                // 反序列化
                var points = JsonSerializer.Deserialize<List<JsonDataPoint>>(record.CurveDataJson);
                var curveData = points.Select(p => new DataPoint(p.X, p.Y)).ToList();
                var model = new PlotModel { Title = $"曲线 - {record.Id}"};
                model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "位移 (mm)", MajorGridlineStyle = LineStyle.Solid, MinorGridlineStyle = LineStyle.Dot });
                model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "压力 (N)", MajorGridlineStyle = LineStyle.Solid, MinorGridlineStyle = LineStyle.Dot });

                var series = new LineSeries
                {
                    Color = record.ResulEnd ? OxyColors.Green : OxyColors.Red, // OK绿色，NG红色
                    StrokeThickness = 2
                };

                if (points != null)
                {
                    series.Points.AddRange(curveData);
                }

                model.Series.Add(series);
                CurveModel = model; // 更新属性，通知界面重绘
            }
            catch
            {
                // 解析失败处理
            }
        }
    }


    // 1. 定义一个可读写的中间类
    public class JsonDataPoint
    {
        public double X { get; set; } 
        public double Y { get; set; }
    }


}