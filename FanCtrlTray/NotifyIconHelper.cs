using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static FanCtrlTray.NativeMethods;

namespace FanCtrlTray
{
    // Karsten: https://stackoverflow.com/a/26695961
    internal static class NotifyIconHelper
    {
        public static Rectangle GetIconRect(NotifyIcon icon)
        {
            RECT rect = new RECT();
            NOTIFYICONIDENTIFIER notifyIcon = new NOTIFYICONIDENTIFIER
            {
                hWnd = GetHandle(icon),
                uID = GetId(icon)
            };
            notifyIcon.cbSize = (uint)Marshal.SizeOf(notifyIcon);

            Marshal.ThrowExceptionForHR(NativeMethods.Shell_NotifyIconGetRect(ref notifyIcon, ref rect));

            return Rectangle.FromLTRB(rect.left, rect.top, rect.right, rect.bottom);
        }

        private static readonly FieldInfo windowField = typeof(NotifyIcon).GetField("window", BindingFlags.NonPublic | BindingFlags.Instance);

        public static IntPtr GetHandle(NotifyIcon icon)
        {
            if (windowField == null)
                throw new InvalidOperationException("[Useful error message]");

            NativeWindow window = (NativeWindow)windowField.GetValue(icon);
            return window.Handle;
        }

        private static readonly FieldInfo idField = typeof(NotifyIcon).GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);

        public static uint GetId(NotifyIcon icon)
        {
            if (idField == null)
                throw new InvalidOperationException("[Useful error message]");

            return (uint)idField.GetValue(icon);
        }
    }
}