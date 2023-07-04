using System.Collections;
using System.ComponentModel;
using System.ServiceProcess;

namespace FanCtrl
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }
        protected override void OnAfterUninstall(IDictionary savedState)
        {
            DellSMMIO.BDSID_RemoveDriver();
            base.OnAfterUninstall(savedState);
        }
        protected override void OnAfterInstall(IDictionary savedState)
        {
            using (var sc = new ServiceController(serviceInstaller1.ServiceName))
            {
                sc.Start();
            }
            base.OnAfterInstall(savedState);
        }
    }
}
