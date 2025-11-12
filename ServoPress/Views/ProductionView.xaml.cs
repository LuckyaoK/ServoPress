using System.Windows.Controls;

namespace ServoPress.Views
{
    /// <summary>
    /// ProductionView.xaml 的交互逻辑
    /// </summary>
    public partial class ProductionView : UserControl
    {
        public ProductionView()
        {
            InitializeComponent();
            this.DataContext = new ServoPress.ViewModels.ProductionViewModel();
        }
    }
}