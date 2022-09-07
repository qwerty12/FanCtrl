using FanCtrlCommon;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.ServiceModel;
using System.Windows.Forms;

namespace FanCtrlTray
{
#if DEBUG
    // Pavlo K: https://stackoverflow.com/a/48864902
    using Microsoft.Win32.SafeHandles;
    using System.IO;

    static class WinConsole
    {
        static public void Initialize(bool alwaysCreateNewConsole = true)
        {
            bool consoleAttached = true;
            if (alwaysCreateNewConsole
                || (AttachConsole(ATTACH_PARRENT) == 0
                && Marshal.GetLastWin32Error() != ERROR_ACCESS_DENIED))
            {
                consoleAttached = AllocConsole() != 0;
            }

            if (consoleAttached)
            {
                InitializeOutStream();
                InitializeInStream();
            }
        }

        private static void InitializeOutStream()
        {
            var fs = CreateFileStream("CONOUT$", GENERIC_WRITE, FILE_SHARE_WRITE, FileAccess.Write);
            if (fs != null)
            {
                var writer = new StreamWriter(fs) { AutoFlush = true };
                Console.SetOut(writer);
                Console.SetError(writer);
            }
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
            if (!file.IsInvalid)
            {
                var fs = new FileStream(file, dotNetFileAccess);
                return fs;
            }
            return null;
        }

    #region Win API Functions and Constants
        [DllImport("kernel32.dll",
            EntryPoint = "AllocConsole",
            SetLastError = true,
            CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall)]
        private static extern int AllocConsole();

        [DllImport("kernel32.dll",
            EntryPoint = "AttachConsole",
            SetLastError = true,
            CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall)]
        private static extern UInt32 AttachConsole(UInt32 dwProcessId);

        [DllImport("kernel32.dll",
            EntryPoint = "CreateFileW",
            SetLastError = true,
            CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr CreateFileW(
              string lpFileName,
              UInt32 dwDesiredAccess,
              UInt32 dwShareMode,
              IntPtr lpSecurityAttributes,
              UInt32 dwCreationDisposition,
              UInt32 dwFlagsAndAttributes,
              IntPtr hTemplateFile
            );

        private const UInt32 GENERIC_WRITE = 0x40000000;
        private const UInt32 GENERIC_READ = 0x80000000;
        private const UInt32 FILE_SHARE_READ = 0x00000001;
        private const UInt32 FILE_SHARE_WRITE = 0x00000002;
        private const UInt32 OPEN_EXISTING = 0x00000003;
        private const UInt32 FILE_ATTRIBUTE_NORMAL = 0x80;
        private const UInt32 ERROR_ACCESS_DENIED = 5;

        private const UInt32 ATTACH_PARRENT = 0xFFFFFFFF;

    #endregion
    }
#endif

    class Program
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);

        const int UpdateTime = 1000;
        const int CycleTime = 50;
        const int CyclesToWaste = (UpdateTime / CycleTime) - 1;

        static bool run = true;
        static bool getFanSpeed = false;
        static IFanCtrlInterface interf = null;
        static ToolStripMenuItem forceItem = null;
        static ToolStripItem exitItem = null;


        static void Main(string[] args)
        {
#if DEBUG
            WinConsole.Initialize();
#endif
            ChannelFactory<IFanCtrlInterface> pipeFactory = new ChannelFactory<IFanCtrlInterface>(new NetNamedPipeBinding(NetNamedPipeSecurityMode.None), new EndpointAddress("net.pipe://localhost/FanCtrlInterface"));

            ContextMenuStrip strip = new ContextMenuStrip();

            ToolStripMenuItem rpmItem = (ToolStripMenuItem)strip.Items.Add("Fan0: 0 RPM");
            //rpmItem.Enabled = false;
            forceItem = (ToolStripMenuItem)strip.Items.Add("Force full speed");
            strip.Items.Add("-");
            exitItem = strip.Items.Add("Exit");
            strip.Opening += Strip_Opening;
            strip.Closing += Strip_Closing;
            strip.ItemClicked += Strip_ItemClicked;

            NotifyIcon icon = new NotifyIcon();
            icon.Text = "FanCtrl";
            icon.Visible = true;
            icon.ContextMenuStrip = strip;

            Pen[] pens = new Pen[] { new Pen(Color.Green), new Pen(Color.White), new Pen(Color.Red) };
            SolidBrush brush = new SolidBrush(Color.White);
            Font font = new Font("Tahoma", 8);
            Bitmap bitmap = new Bitmap(16, 16);
            Graphics graph = Graphics.FromImage(bitmap);
            SizeF txtSize;
            string txt;
            uint fanlvl;
            FanCtrlData d;
            uint lastFanSpeed = 0;

            uint counter = 0;

            while (run)
            {
                if (counter == 0)
                {
                    try
                    {
                        if (interf == null)
                            interf = pipeFactory.CreateChannel();

                        d = interf.GetData();

                        if (getFanSpeed)
                        {
                            uint fanSpeed = interf.GetFan1Rpm();
#if DEBUG
                            Console.WriteLine(fanSpeed);
#endif
                            if (lastFanSpeed != fanSpeed)
                            {
                                lastFanSpeed = fanSpeed;
                                rpmItem.Text = $"Fan0: {fanSpeed} RPM";
                            }
                        }

                        if (d.SystemTemperature < 100)
                            txt = d.SystemTemperature.ToString();
                        else
                            txt = "!";

                        fanlvl = Math.Min(d.FanLevel, 2);

                        forceItem.Checked = interf.Level2IsForced();
                    }
                    catch
                    {
                        txt = "?";
                        fanlvl = 2;
                        interf = null;
                    }

                    graph.Clear(Color.Transparent);
                    graph.DrawRectangle(pens[fanlvl], 0, 0, 15, 15);
                    txtSize = graph.MeasureString(txt, font);
                    graph.DrawString(txt, font, brush, 8 - txtSize.Width / 2, 8 - txtSize.Height / 2);

                    icon.Icon = Icon.FromHandle(bitmap.GetHicon());
                    DestroyIcon(icon.Icon.Handle);

                    counter = CyclesToWaste;
                }
                else
                {
                    counter--;
                }

                Application.DoEvents();
                System.Threading.Thread.Sleep(CycleTime);
            }

            icon.Visible = false;

            try
            {
                pipeFactory.Close();
            }
            catch {}
        }

        private static void Strip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            getFanSpeed = true;
        }

        private static void Strip_Closing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            getFanSpeed = false;
        }

        private static void Strip_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (e.ClickedItem == exitItem)
            {
                run = false;
            }
            else if (e.ClickedItem == forceItem && interf != null)
            {
                getFanSpeed = false;
                interf.SetLevel2IsForced(forceItem.Checked = !forceItem.Checked);
            }
        }
    }
}
