using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Wpf;
using ServoPress.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ServoPress.Views
{
    public partial class StationView : UserControl
    {
        private bool isDragging = false;
        private DataPoint startPoint;
        private RectangleAnnotation tempRectangle; // 仅用于拖拽时的临时显示

        public StationView()
        {
            InitializeComponent();
        }

        // 获取 ViewModel 的引用
        private StationViewModel ViewModel => DataContext as StationViewModel;

        private void PlotView1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if(e.ChangedButton == MouseButton.Left)
            {
             
                var position = e.GetPosition(PlotView1);
                startPoint = ScreenToDataPoint(position);

                if (double.IsNaN(startPoint.X) || double.IsNaN(startPoint.Y)) return;

                isDragging = true;

                // 2. 创建临时视觉反馈 (View Logic - 仅为了让用户看到他在拖拽)
                tempRectangle = new RectangleAnnotation
                {
                    MinimumX = startPoint.X,
                    MaximumX = startPoint.X,
                    MinimumY = startPoint.Y,
                    MaximumY = startPoint.Y,
                    Fill = OxyColor.FromArgb(0x40, 0x00, 0xFF, 0x00), // 40% 透明度的绿色
                    Stroke = OxyColors.Green,
                    StrokeThickness = 2,
                    TextColor = OxyColors.Black
                };

                ViewModel?.PlotModel.Annotations.Add(tempRectangle);
                ViewModel?.PlotModel.InvalidatePlot(true);
                //防止鼠标丢失
                PlotView1.CaptureMouse();
                e.Handled = true;
            }
        }

        private void PlotView1_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && tempRectangle != null)
            {
                var currentPoint = ScreenToDataPoint(e.GetPosition(PlotView1));
                tempRectangle.MinimumX = Math.Min(startPoint.X, currentPoint.X);
                tempRectangle.MaximumX = Math.Max(startPoint.X, currentPoint.X);
                tempRectangle.MinimumY = Math.Min(startPoint.Y, currentPoint.Y);
                tempRectangle.MaximumY = Math.Max(startPoint.Y, currentPoint.Y);

                ViewModel?.PlotModel.InvalidatePlot(true);
            }
        }

        private void PlotView1_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging && tempRectangle != null)
            {
                isDragging = false;
                PlotView1.ReleaseMouseCapture();

                // 1. 清理临时视觉元素 (View Logic)
                double minX = tempRectangle.MinimumX;
                double maxX = tempRectangle.MaximumX;
                double minY = tempRectangle.MinimumY;
                double maxY = tempRectangle.MaximumY;

                ViewModel?.PlotModel.Annotations.Remove(tempRectangle);
                
                ViewModel?.CreateNewEvalWindow(minX, maxX, minY, maxY);
            }
        }

       
        private DataPoint ScreenToDataPoint(Point screenPoint)
        {
            double xRelative = screenPoint.X;
            double yRelative = screenPoint.Y;

            // 使用坐标轴进行转换
            var xAxis = PlotView1.ActualModel.Axes[0]; // 通常是X轴
            var yAxis = PlotView1.ActualModel.Axes[1]; // 通常是Y轴

            double dataX = xAxis.InverseTransform(xRelative);
            double dataY = yAxis.InverseTransform(yRelative);

            return new DataPoint(dataX, dataY);
        }


    }
}