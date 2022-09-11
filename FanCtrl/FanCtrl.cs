using FanCtrlCommon;
using System;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceProcess;
using Timer = System.Timers.Timer;
using LibreHardwareMonitor.Hardware;

namespace FanCtrl
{
    [System.ComponentModel.DesignerCategory("Code")]
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class FanCtrl : ServiceBase, IFanCtrlInterface
    {
        DellSMMIO io;
        Timer timer;
        ServiceHost host;
        Computer computer;

        public FanCtrl()
        {
            CanHandlePowerEvent = true;
            //CanShutdown = true;
            ServiceName = "FanCtrl";
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;

#if DEBUG
            Serilog.Log.Information("{ServiceName} starting", ServiceName);
#endif

            DellSMMIO.StopService("R0FanCtrl");
            DellSMMIO.RemoveService("R0FanCtrl", false);

            computer = new Computer
            {
                IsCpuEnabled = true,
                IsStorageEnabled = false,
                IsGpuEnabled = false,
                IsMemoryEnabled = false,
                IsMotherboardEnabled = false,
                IsControllerEnabled = false,
                IsNetworkEnabled = false,
                IsBatteryEnabled = false,
                IsPsuEnabled = false
            };
            io = new DellSMMIO();

            timer = new Timer();
            timer.Interval = 1000;
            timer.Elapsed += Timer_Elapsed;

            host = new ServiceHost(this, new Uri("net.pipe://localhost"));
            host.AddServiceEndpoint(typeof(IFanCtrlInterface), new NetNamedPipeBinding(NetNamedPipeSecurityMode.None), "FanCtrlInterface");
        }

        uint fanlvl = uint.MaxValue;
        uint maxTemp;
        ushort startTries = 5;
        ushort ticksToSkip;
        ushort ticksToSkip2;
        bool _level2forced = false;
        const uint lv2MaxTemp = 65;

        private uint MaxTemperature()
        {
            uint result = 0;

            foreach (IHardware hardware in computer.Hardware)
            {
                hardware.Update();

                foreach (ISensor sensor in hardware.Sensors)
                {
                    if (sensor.SensorType != SensorType.Temperature || !sensor.Value.HasValue)
                        continue;

#if DEBUG
                    Serilog.Log.Debug("Sensor: {0} ({2}), value: {1}", sensor.Name, sensor.Value.Value, hardware.Name);
#endif
                    uint val = (uint)sensor.Value;
                    if (val <= result)
                        continue;

                    // "CPU Core #x" / "CPU Package"
                    if (hardware.HardwareType == HardwareType.Cpu && sensor.Name.Length != 11)
                        continue;

                    if (val >= lv2MaxTemp) // short-circuit
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
                if (!io.Open())
                {
                    if (!io.BDSID_InstallDriver() || !io.BDSID_StartDriver())
                    {
                        if (--startTries == 0)
                            Stop();
                    }
                    return;
                }

                startTries = 5;
                computer.Open();
                EnableManualFanControl();
            }

            maxTemp = MaxTemperature();

            if (maxTemp == 0)
            {
                //Something is very wrong
                Stop();
                return;
            }
            else if (maxTemp >= lv2MaxTemp)
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

        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            switch (powerStatus)
            {
                case PowerBroadcastStatus.QuerySuspend:
                case PowerBroadcastStatus.Suspend:
                    timer.Stop();
                    io.dell_smm_io_set_fan_lv(DellSMMIO.DELL_SMM_IO_FAN1, DellSMMIO.DELL_SMM_IO_FAN_LV0);
                    if (powerStatus == PowerBroadcastStatus.Suspend)
                        DisableManualFanControl();
                    break;
                case PowerBroadcastStatus.QuerySuspendFailed:
                case PowerBroadcastStatus.ResumeSuspend:
                    if (io.Opened)
                        EnableManualFanControl();
                    OnStart(null);
                    break;
            }

            return base.OnPowerEvent(powerStatus);
        }

        protected override void OnStart(string[] args)
        {
            ticksToSkip = ticksToSkip2 = 0;
            fanlvl = uint.MaxValue;
            timer.Start();
            Timer_Elapsed(null, null);
            if (host.State == CommunicationState.Created)
                host.Open();
        }

        private void EnableManualFanControl()
        {
            io.dell_smm_io(DellSMMIO.DELL_SMM_IO_DISABLE_FAN_CTL1, DellSMMIO.DELL_SMM_IO_NO_ARG);
        }

        private void DisableManualFanControl()
        {
            io.dell_smm_io(DellSMMIO.DELL_SMM_IO_ENABLE_FAN_CTL1, DellSMMIO.DELL_SMM_IO_NO_ARG);
        }

        protected override void OnStop()
        {
            timer.Stop();
            DisableManualFanControl();
            try
            {
                computer.Close();
            }
            catch { }
            io.BDSID_Shutdown();
            host.Abort();
            try
            {
                host.Close();
            }
            catch { }
        }

        public FanCtrlData GetData()
        {
            return new FanCtrlData(maxTemp, fanlvl);
        }

        public uint GetFan1Rpm()
        {
            return io.Opened ? io.dell_smm_io(DellSMMIO.DELL_SMM_IO_GET_FAN_RPM, DellSMMIO.DELL_SMM_IO_FAN1) : 0;
        }

        private void SetFanLevel(uint level)
        {
            if (level == fanlvl)
                return;
            io.dell_smm_io_set_fan_lv(DellSMMIO.DELL_SMM_IO_FAN1, level);
            fanlvl = level;
        }

        public bool Level2IsForced()
        {
            return _level2forced;
        }

        public void SetLevel2IsForced(bool forced)
        {
            _level2forced = forced;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                io.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
