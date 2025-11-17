using System.Windows;
using System.Windows.Input;

namespace ServoPress.Behaviors
{
    /// <summary>
    /// 包含用于窗口的附加行为 (Attached Behaviors)
    /// </summary>
    public static class WindowBehavior
    {
        // ====================================================================
        // 1. 附加行为: 启用拖动窗口 (EnableDragMove)
        // ====================================================================

        public static bool GetEnableDragMove(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableDragMoveProperty);
        }

        public static void SetEnableDragMove(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableDragMoveProperty, value);
        }

        // 使用 'prop' Gist 片段创建附加属性
        public static readonly DependencyProperty EnableDragMoveProperty =
            DependencyProperty.RegisterAttached(
                "EnableDragMove",
                typeof(bool),
                typeof(WindowBehavior),
                new PropertyMetadata(false, OnEnableDragMoveChanged));

        private static void OnEnableDragMoveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                if ((bool)e.NewValue)
                {
                    // 启用: 订阅事件
                    element.MouseLeftButtonDown += Element_MouseLeftButtonDown;
                }
                else
                {
                    // 禁用: 取消订阅
                    element.MouseLeftButtonDown -= Element_MouseLeftButtonDown;
                }
            }
        }

        private static void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // 找到附加此行为的元素所在的窗口
                Window window = Window.GetWindow(sender as DependencyObject);
                if (window != null)
                {
                    // 执行拖动
                    window.DragMove();
                }
            }
        }

        // ====================================================================
        // 2. 附加行为: 触发窗口关闭 (CloseWindowTrigger)
        // ====================================================================

        public static bool GetCloseWindowTrigger(DependencyObject obj)
        {
            return (bool)obj.GetValue(CloseWindowTriggerProperty);
        }

        public static void SetCloseWindowTrigger(DependencyObject obj, bool value)
        {
            obj.SetValue(CloseWindowTriggerProperty, value);
        }

        public static readonly DependencyProperty CloseWindowTriggerProperty =
            DependencyProperty.RegisterAttached(
                "CloseWindowTrigger",
                typeof(bool),
                typeof(WindowBehavior),
                new PropertyMetadata(false, OnCloseWindowTriggerChanged));

        private static void OnCloseWindowTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // 当 ViewModel 的 'IsCloseRequested' 变为 true 时
            if (d is Window window && (bool)e.NewValue)
            {
                // 关闭窗口
                window.Close();
            }
        }
    }
}