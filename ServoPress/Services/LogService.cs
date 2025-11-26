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

        public static void Debug(string message) => _logger.Debug(message);

        public static void Info(string message) => _logger.Info(message);

        public static void Error(string message) => _logger.Error(message);

        public static void Error(Exception ex, string message) => _logger.Error(ex, message);
    }
}
