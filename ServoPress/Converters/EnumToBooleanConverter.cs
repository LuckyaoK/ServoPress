using System.Globalization;
using System.Windows.Data;

namespace ServoPress.Converters
{
    /// <summary>
    /// 将 Enum 值转换为布尔值 (用于 RadioButton 绑定).
    /// </summary>
    public class EnumToBooleanConverter : IValueConverter
    {
        /// <summary>
        /// 从 ViewModel (Enum) 转换到 View (bool)
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            // value 是 ViewModel 中的 Enum 值 (例如 MeasureMode.Start)
            // parameter 是 XAML 中 RadioButton 的 ConverterParameter (例如 "Start")
            string enumValue = value.ToString();
            string targetValue = parameter.ToString();

            // 如果两者匹配, RadioButton 就应该被选中 (true)
            return enumValue.Equals(targetValue);
        }

        /// <summary>
        /// 从 View (bool) 转换回 ViewModel (Enum)
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool && (bool)value)
            {
                // value 是 RadioButton 的 IsChecked 值 (true)
                // targetType 是 ViewModel 属性的类型 (例如 typeof(MeasureMode))
                // parameter 是 XAML 中 RadioButton 的 ConverterParameter (例如 "Start")
                string parameterString = parameter as string;
                if (parameterString != null)
                {
                    // 将 "Start" 字符串解析为 MeasureMode.Start 枚举值
                    return Enum.Parse(targetType, parameterString);
                }
            }

            // 如果 RadioButton 是 false, 我们不做任何事
            return Binding.DoNothing;
        }
    }
}