using System.ServiceProcess;
#if DEBUG
using Serilog;
#endif

namespace FanCtrl
{
    static class Program
    {
        /// <summary>
        /// Punto di ingresso principale dell'applicazione.
        /// </summary>
        static void Main()
        {
#if !DEBUG
            ServiceBase.Run(new FanCtrl());
#else
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File($"{System.IO.Path.GetTempPath()}\\FanCtrl.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();
            try
            {
                ServiceBase.Run(new FanCtrl());
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "Error in FanCtrl");
            }
            finally
            {
                Log.CloseAndFlush();
            }
#endif
        }
    }
}
