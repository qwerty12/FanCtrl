using System;
using System.Diagnostics;
using System.Reflection;
using System.ServiceModel;
using System.ServiceProcess;
using FanCtrlCommon;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Storage;
using Timer = System.Timers.Timer;

namespace FanCtrl
{
    [System.ComponentModel.DesignerCategory("Code")]
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class FanCtrl : ServiceBase, IFanCtrlInterface
    {
        private readonly DellSMMIO io;
        private readonly Timer timer;
        private readonly ServiceHost host;
        private readonly Computer computer;

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
            initNvmeHd0Monitoring();
            io = new DellSMMIO();

            timer = new Timer
            {
                Interval = 1000
            };
            timer.Elapsed += Timer_Elapsed;

            host = new ServiceHost(this, new Uri("net.pipe://localhost"));
            host.AddServiceEndpoint(typeof(IFanCtrlInterface), new NetNamedPipeBinding(NetNamedPipeSecurityMode.None), "FanCtrlInterface");
        }

        private uint fanlvl = uint.MaxValue;
        private uint maxTemp;
        private ushort startTries = 5;
        private ushort ticksToSkip;
        private ushort ticksToSkip2;
        private bool _level2forced = false;
        private const uint lv2MaxTemp = 67;

        private NVMeSmart nvme0;

        private void initNvmeHd0Monitoring()
        {
            /* Yes, this is a hack using Reflection that may break if LHM is updated.
               I would still rather use this. Why?

               * There's no point in copying large swathes of LHM code when I'm already using it
               * I make it a point to avoid WMI when dealing with the local machine where possible,
                 which is why AbstractStorage.CreateInstance is avoided (and, indeed, IsStorageEnabled)
               * I only need the temperature of one drive
             */
            const string deviceId = @"\\.\PHYSICALDRIVE0";
            const uint driveIndex = 0;

            try
            {
                var WindowsStorage =
                    Type.GetType(
                        $"LibreHardwareMonitor.Hardware.Storage.WindowsStorage, {typeof(NVMeSmart).Assembly.FullName}");

                var GetStorageInfo = WindowsStorage.GetMethod("GetStorageInfo", BindingFlags.Public | BindingFlags.Static);

                var storageInfo = GetStorageInfo.Invoke(null, new object[] { deviceId, driveIndex });
                storageInfo.GetType().GetProperty("DeviceId", BindingFlags.Public | BindingFlags.Instance)
                    .SetValue(storageInfo, deviceId);

                nvme0 = Activator.CreateInstance(typeof(NVMeSmart), BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { storageInfo }, null) as NVMeSmart;
                if (nvme0 == null)
                    return;
                if (!nvme0.IsValid)
                    nvme0 = null;
            }
            catch (Exception e)
            {
                nvme0 = null;
#if DEBUG
                Serilog.Log.Error(e, "Monitoring temperature of NVMe SSD 0 failed");
#endif
                _ = e;
            }
        }

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
                    if (sensor.Name.Length != 11)
                        continue;

                    if (val >= lv2MaxTemp) // short-circuit
                        return val;
                    result = val;
                }
            }

            if (result == 0)
                return 0;

            NVMeHealthInfo health = nvme0?.GetHealthInfo();
            if (health == null)
                return result;
            var temperature = health.Temperature;
            if (temperature > 0 && temperature < 1000)
            {
#if DEBUG
                Serilog.Log.Information("NVME temp (deg. C): {temperature}", temperature);
#endif
                if (temperature > result)
                    result = (uint)temperature;
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
                DisableManualFanControl();
                timer.Stop();
                Stop();
                return;
            }
            else if (maxTemp >= lv2MaxTemp)
            {
                ticksToSkip = 7;
                ticksToSkip2 = 30;
            }
            else if (ticksToSkip > 0)
            {
                ticksToSkip--;
            }
            else if (maxTemp >= 48)
            {
                ticksToSkip2 = 30;
            }
            else if (ticksToSkip2 > 0 && maxTemp <= 45)
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
                nvme0?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}