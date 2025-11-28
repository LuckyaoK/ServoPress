using CommunityToolkit.Mvvm.ComponentModel;
using ServoPress.Services;
using System.Collections.ObjectModel;

namespace ServoPress.ViewModels
{
    /// <summary>
    /// 生产主页的 ViewModel
    /// </summary>
    public partial class ProductionViewModel : ObservableObject
    {
        public ObservableCollection<StationViewModel> Stations { get; }

        public ProductionViewModel(CurveBoxService curveBoxService, DataStorageService dataStorageService)
        {
            Stations = new ObservableCollection<StationViewModel>();

            // 初始化四个工位
            for (int i = 1; i <= 4; i++)
            {
                var stationVM = new StationViewModel(i, curveBoxService, dataStorageService);
                Stations.Add(stationVM);
            }
        }
    }
}