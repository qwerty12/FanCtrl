using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FanCtrlTray
{
    internal class ThemeBollocks : NativeWindow
    {
        private const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x320,
                          WM_DWMCOMPOSITIONCHANGED = 0x31E,
                          WM_THEMECHANGED = 0x031A,
                          WM_SETTINGCHANGE = 0x001A;
        private Action ThemeChangedCallback;
        private readonly System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();

        public ThemeBollocks(NotifyIcon parent, Action fnThemeChangedCallback)
        {
            ThemeChangedCallback = fnThemeChangedCallback;
            t.Interval = 1000;
            t.Tick += new EventHandler(OnTimerTick);
            AssignHandle(NotifyIconGetHwnd(parent));
            parent.Disposed += new EventHandler(this.OnHandleDestroyed);
        }

        ~ThemeBollocks()
        {
            t.Dispose();
            ThemeChangedCallback = null;
            ReleaseHandle();
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_DWMCOLORIZATIONCOLORCHANGED:
                //case WM_DWMCOMPOSITIONCHANGED:
                //case WM_THEMECHANGED:
                    t.Stop();
                    t.Start();
                    break;
                /*case WM_SETTINGCHANGE:
                    var settingChanged = Marshal.PtrToStringUni(m.LParam);
                    if (settingChanged == "ImmersiveColorSet" || // Accent color
                        settingChanged == "WindowsThemeElement") // High contrast
                    {
                        t.Stop();
                        t.Start();
                    }
                    break;*/
            }
            base.WndProc(ref m);
        }

        internal void OnHandleDestroyed(object sender, EventArgs e)
        {
            ReleaseHandle();
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            t.Stop();
            ThemeChangedCallback();
        }

        public static bool IsTaskbarDark()
        {
            return ReadDword(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme") != 1;
        }

        // Karsten: https://stackoverflow.com/a/26695961
        private static FieldInfo nfWindowField = typeof(NotifyIcon).GetField("window", BindingFlags.NonPublic | BindingFlags.Instance);
        private static IntPtr NotifyIconGetHwnd(NotifyIcon icon)
        {
            if (nfWindowField == null) throw new InvalidOperationException("[Useful error message]");
            NativeWindow window = nfWindowField.GetValue(icon) as NativeWindow;

            if (window == null) throw new InvalidOperationException("[Useful error message]");  // should not happen?
            return window.Handle;
        }

        // Pilfered, like much of this file, from EarTrumpet
        private static int ReadDword(string key, string valueName, int defaultValue = 0)
        {
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            using (var subKey = baseKey.OpenSubKey(key))
            {
                return (int)subKey.GetValue(valueName, defaultValue);
            }
        }
    }
}
