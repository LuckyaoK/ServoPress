using ServoPress.ViewModels;
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
            DataContext = new SequenceEditorViewModel();
        }
    }
}
