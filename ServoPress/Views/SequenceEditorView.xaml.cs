using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ServoPress.Views
{
    /// <summary>
    /// SequenceEditorView.xaml 的交互逻辑
    /// </summary>
    public partial class SequenceEditorView : UserControl
    {
        public SequenceEditorView()
        {
            InitializeComponent();
            // 默认显示第一个编辑器 (动作)
            if (DetailEditorTabs != null)
            {
                DetailEditorTabs.SelectedIndex = 0;
            }
        }

        // --- 事件处理器: 切换第三列的 TabControl ---

        private void AddMotionButton_Click(object sender, RoutedEventArgs e)
        {
            DetailEditorTabs.SelectedIndex = 0;
        }

        private void AddMeasureButton_Click(object sender, RoutedEventArgs e)
        {
            DetailEditorTabs.SelectedIndex = 1;
        }

        private void AddTimeButton_Click(object sender, RoutedEventArgs e)
        {
            DetailEditorTabs.SelectedIndex = 2;
        }

        private void AddWaitButton_Click(object sender, RoutedEventArgs e)
        {
            DetailEditorTabs.SelectedIndex = 3;
        }

        private void AddLabelButton_Click(object sender, RoutedEventArgs e)
        {
            DetailEditorTabs.SelectedIndex = 4;
        }

        private void AddJumpButton_Click(object sender, RoutedEventArgs e)
        {
            DetailEditorTabs.SelectedIndex = 5;
        }

        private void AddHomeButton_Click(object sender, RoutedEventArgs e)
        {
            DetailEditorTabs.SelectedIndex = 6;
        }

        private void AddEndButton_Click(object sender, RoutedEventArgs e)
        {
            DetailEditorTabs.SelectedIndex = 7;
        }
    }
}
