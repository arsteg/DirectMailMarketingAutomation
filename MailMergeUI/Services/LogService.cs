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
        public void Log(string message)
        {
            Serilog.Log.Information(message);
        }

        public string ReadLog()
        {
            // Note: Reading the active log file might be tricky due to file locks.
            // For now, we'll return a placeholder or implement a safer read mechanism if needed.
            // Since we switched to Serilog, manually reading "mailmax_log.txt" is no longer relevant.
            // We could point to the logs folder or read the latest log file if really needed.
            return "Logs are now handled by Serilog. Check the 'Logs' folder.";
        }
    }
}
