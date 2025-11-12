using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ServoPress.ViewModels
{
    /// <summary>
    /// 生产主页的 ViewModel
    /// </summary>
    public partial class ProductionViewModel : ObservableObject
    {
        /// <summary>
        /// 绑定到主页 ItemsControl 的工位集合
        /// </summary>
        public ObservableCollection<StationViewModel> Stations { get; }

        public ProductionViewModel()
        {
            Stations = new ObservableCollection<StationViewModel>();

            // 初始化四个工位
            for (int i = 1; i <= 4; i++)
            {
                var stationVM = new StationViewModel
                {
                    Id = i,
                    StationName = $"工位 {i}",
                    Result="OK",
                    // 在这里，您可以注入数据采集服务或事件总线
                    // 以便 StationViewModel 能够独立接收数据
                };
                Stations.Add(stationVM);
            }
        }
    }
}