using Aimmy2.Class;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Visuality;

namespace Other
{
    internal class LogManager
    {
        private static readonly object LogLock = new();
        private static readonly Dictionary<string, long> LastLogged = new();
        public enum LogLevel
        {
            Info,
            Warning,
            Error
        }

        public static void Log(LogLevel lvl, string message, bool notifyUser = false, int waitingTime = 4000)
        {
            if (notifyUser) NoticeBar.Show(message, waitingTime);
#if DEBUG
            Debug.WriteLine(message);
#endif
            if(Dictionary.toggleState["Debug Mode"])
            {
                lock (LogLock)
                {
                var now = Environment.TickCount64;
                if (LastLogged.TryGetValue(message, out var last) && now - last < 1000) return;
                if (LastLogged.Count >= 256) LastLogged.Clear();
                LastLogged[message] = now;
                try
                {
                string logFilepath = "debug.txt";
                using StreamWriter w = new(logFilepath, true);
                string lvlPrefix = lvl.ToString().ToUpper();
                w.WriteLine($"[{DateTime.Now}] [{lvlPrefix}]: {message}");
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                }
            }
        }
    }
}
