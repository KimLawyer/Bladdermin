using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS;

namespace BladderMin
{
    //A class for logging errors and exceptions into a log file that can then be read by the user.
    public static class SeriLog
    {
        public static void Initialize(string user = "RunFromLauncher")
        {
            var SessionTimeStart = DateTime.Now;
            var AssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var directory = Path.Combine(AssemblyPath, @"Logs");
            var logpath = Path.Combine(directory, string.Format(@"BladderminLog_{0}_{1}_{2}.txt", SessionTimeStart.ToString("dd-MMM-yyyy"), SessionTimeStart.ToString("hh-mm-ss"), user.Replace(@"\", @"_")));
            Log.Logger = new LoggerConfiguration().WriteTo.File(logpath, Serilog.Events.LogEventLevel.Information,
                "{Timestamp:dd-MMM-yyy HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}").CreateLogger();
        }
        public static void AddInfo(string logInfo)
        {
            Log.Information(logInfo);
        }
        public static void AddWarning(string logInfo, Exception ex = null)
        {
            Log.Warning(logInfo);
        }
        public static void AddError(string logInfo, Exception ex = null)
        {
            if (ex == null)
                Log.Error(logInfo);
            else
                Log.Error(ex, logInfo);
        }
        public static void AddFatal(string logInfo, Exception ex = null)
        {
            Log.Fatal(ex, logInfo);
        }

    }

}
