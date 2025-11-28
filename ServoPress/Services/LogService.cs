using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServoPress.Services
{
    public static class LogService
    {
        private static readonly Logger _logger = LogManager.GetLogger("App");

        // 定义一个静态事件，用于将日志消息传递给 UI
        public static event Action<string> OnNewLog;

        private static void NotifyUI(string level,string message)
        {
            // 格式化日志消息: [时间] [级别] 内容
            string formattedMsg = $"[{DateTime.Now:HH:mm:ss:fff}] {message}";
            OnNewLog?.Invoke(formattedMsg);
        }


        public static void Debug(string message)
        {
            _logger.Debug(message);
            NotifyUI("DEBUG", message); 
        }

        public static void Info(string message)
        {
            _logger.Info(message);
            NotifyUI("INFO", message);
        }

        public static void Error(string message)
        {
            _logger.Error(message);
            NotifyUI("ERROR", message);
        }

        public static void Error(Exception ex, string message)
        {
            _logger.Error(ex, message);
            NotifyUI("ERROR", $"{message} : {ex.Message}");
        }
    }
}
