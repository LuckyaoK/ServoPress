using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace ServoPress.ViewModels
{
    /// <summary>
    /// MainWindow 的 ViewModel，负责处理窗口状态和命令
    /// </summary>
    public partial class MainWindowViewModel : ObservableObject
    {
        // 1. 绑定到 Window.WindowState
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsMaximized))]
        private WindowState _windowState = WindowState.Normal;

        // 辅助属性，用于切换最大化/还原按钮的图标
        public bool IsMaximized => WindowState == WindowState.Maximized;

        // 2. 绑定到一个附加行为，用于触发窗口关闭
        [ObservableProperty]
        private bool _isCloseRequested;

        /// <summary>
        /// 最小化窗口
        /// </summary>
        [RelayCommand]
        private void MinimizeWindow()
        {
            // 触发双向绑定，View 将会收到此变更
            WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// 切换最大化/还原
        /// </summary>
        [RelayCommand]
        private void ToggleMaximizeWindow()
        {
            // 触发双向绑定，View 将会收到此变更
            WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        /// <summary>
        /// 请求关闭窗口
        /// </summary>
        [RelayCommand]
        private void CloseWindow()
        {
            // 触发附加行为
            IsCloseRequested = true;
        }
    }
}