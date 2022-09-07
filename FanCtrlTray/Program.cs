using FanCtrlCommon;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.ServiceModel;
using System.Windows.Forms;
using System.Diagnostics;

namespace FanCtrlTray
{

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
        static Process thisProcess = Process.GetCurrentProcess();

        static Pen normalBorderPen;
        static SolidBrush brush;
        static Graphics graph;

        static void Main(string[] args)
        {
#if DEBUG
            WinConsole.Initialize();
#endif
            thisProcess.PriorityClass = ProcessPriorityClass.BelowNormal;
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

            normalBorderPen = new Pen(Color.White);
            Pen[] pens = new Pen[] { new Pen(Color.Green), normalBorderPen, new Pen(Color.Red) };
            brush = new SolidBrush(Color.White);
            Font font = new Font("Tahoma", 8);
            Bitmap bitmap = new Bitmap(16, 16);
            graph = Graphics.FromImage(bitmap);
            SizeF txtSize;
            string txt;
            uint fanlvl;
            FanCtrlData d;
            uint lastFanSpeed = 0;

            adaptTrayIconForTheme();
            var themeChangeListener = new ThemeBollocks(icon, adaptTrayIconForTheme);

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

            themeChangeListener = null;
            icon.Visible = false;

            try
            {
                pipeFactory.Close();
            }
            catch {}
        }

        private static void Strip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            thisProcess.PriorityClass = ProcessPriorityClass.Normal;
            getFanSpeed = true;
        }

        private static void Strip_Closing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            getFanSpeed = false;
            thisProcess.PriorityClass = ProcessPriorityClass.BelowNormal;
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

        private static void adaptTrayIconForTheme()
        {
            if (ThemeBollocks.IsTaskbarDark())
            {
                normalBorderPen.Color = Color.White;
                brush.Color = Color.White;
                graph.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SystemDefault;
            }
            else
            {
                normalBorderPen.Color = Color.Black;
                brush.Color = Color.Black;
                graph.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            }
        }

    }
}
