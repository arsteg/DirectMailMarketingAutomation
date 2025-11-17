using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace MailMergeUI.Services
{
    public class LogService
    {
        private readonly string _logPath = "mailmax_log.txt";

        public void Log(string message)
        {
            var entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {message}{Environment.NewLine}";
            File.AppendAllText(_logPath, entry);
        }

        public string ReadLog()
        {
            return File.Exists(_logPath) ? File.ReadAllText(_logPath) : "No log entries.";
        }
    }
}
