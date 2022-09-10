using System.ServiceProcess;

namespace FanCtrl
{
    static class Program
    {
        /// <summary>
        /// Punto di ingresso principale dell'applicazione.
        /// </summary>
        static void Main()
        {
            ServiceBase.Run(new FanCtrl());
        }
    }
}
