using CommunityToolkit.Mvvm.ComponentModel;

namespace ServoPress.Models
{
    /// <summary>
    /// 步序基类
    /// </summary>
    public abstract partial class SequenceStep : ObservableValidator
    {
        [ObservableProperty]
        private int _stepNumber;

        /// <summary>
        /// 步骤的类型名称 (例如 "移动", "时间")
        /// </summary>
        public abstract string StepType { get; }

        /// <summary>
        /// 步骤的图标路径
        /// </summary>
        public abstract string IconSource { get; }

        /// <summary>
        /// 在 ListView 中显示的步骤详情
        /// </summary>
        public abstract string Summary { get; }

    }

}
