using ServoPress.Services;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ServoPress
{

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 重写 OnClosed 方法，在窗口关闭后执行清理
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

           
            if (this.DataContext is IDisposable disposableVm)
            {
                disposableVm.Dispose();
            }

            // 建议：对于硬件设备，显式调用 GC 有助于加速非托管资源的释放确认
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // 彻底关闭应用程序（防止有后台线程卡住进程）
            Application.Current.Shutdown();
        }

    }

}