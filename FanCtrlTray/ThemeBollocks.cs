using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FanCtrlTray
{
    internal sealed class ThemeBollocks : NativeWindow
    {
        private const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x320,
                          WM_DWMCOMPOSITIONCHANGED = 0x31E,
                          WM_THEMECHANGED = 0x031A,
                          WM_SETTINGCHANGE = 0x001A;
        private Action ThemeChangedCallback;
        private readonly Timer t = new Timer();

        public ThemeBollocks(NotifyIcon parent, Action fnThemeChangedCallback)
        {
            ThemeChangedCallback = fnThemeChangedCallback;
            t.Interval = 1000;
            t.Tick += OnTimerTick;
            AssignHandle(NotifyIconHelper.GetHandle(parent));
            parent.Disposed += this.OnHandleDestroyed;
        }

        ~ThemeBollocks()
        {
            t.Dispose();
            ThemeChangedCallback = null;
            ReleaseHandle();
        }

        private void OnThemeColorsChanged()
        {
            t.Stop();
            t.Start();
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_DWMCOLORIZATIONCOLORCHANGED:
                //case WM_DWMCOMPOSITIONCHANGED:
                //case WM_THEMECHANGED:
                    OnThemeColorsChanged();
                    break;

                case WM_SETTINGCHANGE:
                    var settingChanged = Marshal.PtrToStringUni(m.LParam);
                    if (settingChanged == "ImmersiveColorSet" || // Accent color
                        settingChanged == "WindowsThemeElement") // High contrast
                    {
                        OnThemeColorsChanged();
                    }
                    break;
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
