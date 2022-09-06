using FanCtrlCommon;
using System;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceProcess;
using System.Timers;
using LibreHardwareMonitor.Hardware;

namespace FanCtrl
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public partial class FanCtrl : ServiceBase, IFanCtrlInterface
    {
        DellSMMIO io;
        Timer timer;
        ServiceHost host;
        Computer computer;

        public FanCtrl()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
            InitializeComponent();
            io = new DellSMMIO();
            computer = new Computer
            {
                IsCpuEnabled = true,
                IsStorageEnabled = true,
                IsGpuEnabled = false,
                IsMemoryEnabled = false,
                IsMotherboardEnabled = false,
                IsControllerEnabled = false,
                IsNetworkEnabled = false,
                IsBatteryEnabled = false,
                IsPsuEnabled = false
            };
            timer = new Timer();
            timer.Interval = 1000;
            timer.Elapsed += Timer_Elapsed;
            host = new ServiceHost(this, new Uri[] { new Uri("net.pipe://localhost") });
            host.AddServiceEndpoint(typeof(IFanCtrlInterface), new NetNamedPipeBinding(NetNamedPipeSecurityMode.None), "FanCtrlInterface");
        }

        sbyte fanlvl = -1;
        uint maxTemp;
        ushort startTries = 5;
        ushort ticksToSkip = 0;
        ushort ticksToSkip2 = 0;
        bool _level2forced = false;
        const uint lv2MaxTemp = 65;

        private uint MaxTemperature()
        {
            uint result = 0;

            foreach (IHardware hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Storage && hardware.Name.StartsWith("Samsung"))
                    continue;

                hardware.Update();

                foreach (ISensor sensor in hardware.Sensors)
                {
                    if (sensor.SensorType != SensorType.Temperature || !sensor.Value.HasValue)
                        continue;

                    //Debug.WriteLine("\tSensor: {0} ({2}), value: {1}", sensor.Name, sensor.Value.Value, hardware.Name);
                    uint val = (uint)sensor.Value;
                    if (val <= result)
                        continue;

                    // "CPU Core #x" / "CPU Package"
                    if ((hardware.HardwareType == HardwareType.Cpu && sensor.Name.Length != 11) || (hardware.HardwareType == HardwareType.Storage && sensor.Index != 0))
                        continue;

                    if (val >= lv2MaxTemp) // short-circuit eval
                        return val;
                    result = val;
                }
            }

            return result;
        }

        private void Timer_Elapsed(object sender, EventArgs e)
        {
            if (!io.Opened)
            {
                if (!io.BDSID_InstallDriver() || !io.BDSID_StartDriver() || !io.Open())
                {
                    startTries--;

                    if (startTries == 0)
                        Stop();

                    return;
                }

                computer.Open();
                io.dell_smm_io(DellSMMIO.DELL_SMM_IO_DISABLE_FAN_CTL1, DellSMMIO.DELL_SMM_IO_NO_ARG);
            }

            maxTemp = MaxTemperature();

            if(maxTemp == 0)
            {
                //Something is very wrong
                Stop();
                return;
            }
            else if(maxTemp >= lv2MaxTemp)
            {
                ticksToSkip = 5;
                ticksToSkip2 = 30;
            }
            else if (ticksToSkip > 0)
            {
                ticksToSkip--;
            }
            else if (maxTemp >= 45)
            {
                ticksToSkip2 = 30;
            }
            else if (ticksToSkip2 > 0 && maxTemp <= 42)
            {
                ticksToSkip2--;
            }

            if (ticksToSkip > 0)
            {
                SetFanLevel(DellSMMIO.DELL_SMM_IO_FAN_LV2);
            }
            else if (ticksToSkip2 > 0)
            {
                SetFanLevel(_level2forced ? DellSMMIO.DELL_SMM_IO_FAN_LV2 : DellSMMIO.DELL_SMM_IO_FAN_LV1);
            }
            else
            {
                SetFanLevel(DellSMMIO.DELL_SMM_IO_FAN_LV0);
                _level2forced = false;
            }
        }

        protected override void OnStart(string[] args)
        {
            timer.Start();
            Timer_Elapsed(null, null);
            host.Open();
        }

        protected override void OnStop()
        {
            host.Close();
            timer.Stop();

            io.dell_smm_io(DellSMMIO.DELL_SMM_IO_ENABLE_FAN_CTL1, DellSMMIO.DELL_SMM_IO_NO_ARG);
            fanlvl = -1;
            io.BDSID_Shutdown();

            try
            {
                computer.Close();
            } catch {}
        }

        public FanCtrlData GetData()
        {
            return new FanCtrlData(maxTemp,fanlvl);
        }

        private void SetFanLevel(uint level)
        {
            io.dell_smm_io_set_fan_lv(DellSMMIO.DELL_SMM_IO_FAN1, level);
            fanlvl = (sbyte)level;
        }

        public bool Level2IsForced()
        {
            return _level2forced;
        }

        public void SetLevel2IsForced(bool forced)
        {
            _level2forced = forced;
        }
    }
}
