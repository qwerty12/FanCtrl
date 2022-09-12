using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using static FanCtrlTray.NativeMethods;

namespace FanCtrlTray
{
    // Pavlo K: https://stackoverflow.com/a/48864902
    internal static class WinConsole
    {
        public static void Initialize(bool alwaysCreateNewConsole = true)
        {
            if (alwaysCreateNewConsole
                || (AttachConsole(ATTACH_PARENT_PROCESS)
                && Marshal.GetLastWin32Error() != ERROR_ACCESS_DENIED))
            {
                if (!AllocConsole())
                    return;

                InitializeOutStream();
                InitializeInStream();
            }
        }

        private static void InitializeOutStream()
        {
            var fs = CreateFileStream("CONOUT$", GENERIC_WRITE, FILE_SHARE_WRITE, FileAccess.Write);
            if (fs == null)
                return;

            var writer = new StreamWriter(fs) { AutoFlush = true };
            Console.SetOut(writer);
            Console.SetError(writer);
        }

        private static void InitializeInStream()
        {
            var fs = CreateFileStream("CONIN$", GENERIC_READ, FILE_SHARE_READ, FileAccess.Read);
            if (fs != null)
            {
                Console.SetIn(new StreamReader(fs));
            }
        }

        private static FileStream CreateFileStream(string name, uint win32DesiredAccess, uint win32ShareMode,
                                FileAccess dotNetFileAccess)
        {
            var file = new SafeFileHandle(CreateFileW(name, win32DesiredAccess, win32ShareMode, IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, IntPtr.Zero), true);
            if (file.IsInvalid)
                return null;

            var fs = new FileStream(file, dotNetFileAccess);
            return fs;
        }

        private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;
    }
}