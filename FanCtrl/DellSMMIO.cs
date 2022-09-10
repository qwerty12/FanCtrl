using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DellFanControl;

//Parts taken from https://github.com/marcharding/DellFanControl

namespace FanCtrl
{
    public class DellSMMIO : IDisposable
    {
        IntPtr hDriver = Interop.INVALID_HANDLE_VALUE;

        public const uint DELL_SMM_IO_FAN1 = 0;
        public const uint DELL_SMM_IO_FAN2 = 1;

        public const uint DELL_SMM_IO_GET_POWER_STATUS = 0x0069;
        public const uint DELL_SMM_IO_POWER_STATUS_AC = 0x05;
        public const uint DELL_SMM_IO_POWER_STATUS_BATTERY = 0x01;

        public const uint DELL_SMM_IO_GET_SENSOR_TEMP = 0x10a3;
        public const uint DELL_SMM_IO_SENSOR_CPU = 0; // Probably Core 1
        public const uint DELL_SMM_IO_SENSOR_GPU = 5; // ?? how many sensors
        public const uint DELL_SMM_IO_SENSOR_MAX_TEMP = 127;

        public const uint DELL_SMM_IO_SET_FAN_LV = 0x01a3;
        public const uint DELL_SMM_IO_GET_FAN_LV = 0x00a3;
        public const uint DELL_SMM_IO_GET_FAN_RPM = 0x02a3;

        public const uint DELL_SMM_IO_FAN_LV0 = 0;
        public const uint DELL_SMM_IO_FAN_LV1 = 1;
        public const uint DELL_SMM_IO_FAN_LV2 = 2;

        public const uint DELL_SMM_IO_DISABLE_FAN_CTL1 = 0x30a3;
        public const uint DELL_SMM_IO_ENABLE_FAN_CTL1 = 0x31a3;
        public const uint DELL_SMM_IO_DISABLE_FAN_CTL2 = 0x34a3;
        public const uint DELL_SMM_IO_ENABLE_FAN_CTL2 = 0x35a3;
        public const uint DELL_SMM_IO_NO_ARG = 0x0;

        private readonly Process thisProcess = Process.GetCurrentProcess();
        private readonly IntPtr defaultProcessAffinity = Process.GetCurrentProcess().ProcessorAffinity;

        public const string driverName = "BZHDELLSMMIO";

        public bool BDSID_InstallDriver()
        {
            BDSID_RemoveDriver();

            IntPtr hSCManager = Interop.OpenSCManagerW(null, null, (uint)Interop.SCM_ACCESS.SC_MANAGER_CREATE_SERVICE);
            if (hSCManager == IntPtr.Zero)
                return false;

            IntPtr hService = Interop.CreateServiceW(
                hSCManager,
                driverName,
                driverName,
                Interop.SERVICE_ALL_ACCESS,
                Interop.SERVICE_KERNEL_DRIVER,
                Interop.SERVICE_DEMAND_START,
                Interop.SERVICE_ERROR_NORMAL,
                getDriverPath(),
                null,
                IntPtr.Zero,
                null,
                null,
                null
            );

            Interop.CloseServiceHandle(hSCManager);

            if (hService == IntPtr.Zero)
                return false;

            Interop.CloseServiceHandle(hService);

            return true;
        }

        public bool Open()
        {
            hDriver = Interop.CreateFileW(@"\\?\global\globalroot\DosDevices\BZHDELLSMMIO",
                    Interop.GENERIC_READ,
                    Interop.FILE_SHARE_READ,
                    IntPtr.Zero,
                    Interop.OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

            return hDriver != Interop.INVALID_HANDLE_VALUE;
        }

        public bool Opened => hDriver != Interop.INVALID_HANDLE_VALUE;

        public bool BDSID_StartDriver()
        {
            IntPtr hSCManager = Interop.OpenSCManagerW(null, null, (uint)Interop.SCM_ACCESS.SC_MANAGER_CONNECT);
            if (hSCManager == IntPtr.Zero)
                return false;

            IntPtr hService = Interop.OpenServiceW(hSCManager, driverName, Interop.SERVICE_START);

            Interop.CloseServiceHandle(hSCManager);

            if (hService == IntPtr.Zero)
                return false;

            bool bResult = Interop.StartServiceW(hService, 0, null); // || GetLastError() == ERROR_SERVICE_ALREADY_RUNNING;
            Interop.CloseServiceHandle(hService);

            return bResult;
        }

        public void dell_smm_io_set_fan_lv(uint fan_no, uint lv)
        {
            uint arg = (lv << 8) | fan_no;
            dell_smm_io(DELL_SMM_IO_SET_FAN_LV, arg);
        }

        public uint dell_smm_io_get_fan_lv(uint fan_no)
        {
            return dell_smm_io(DELL_SMM_IO_SET_FAN_LV, fan_no);
        }

        public uint dell_smm_io(uint cmd, uint data)
        {
            Interop.SMBIOS_PKG cam = new Interop.SMBIOS_PKG
            {
                cmd = cmd,
                data = data,
                stat1 = 0,
                stat2 = 0
            };

            uint result_size = 0;

            // TODO: is this needed? It's the driver making the request, not us...
            thisProcess.ProcessorAffinity = (IntPtr) 1;

            bool status_dic = Interop.DeviceIoControl(hDriver,
                Interop.IOCTL_BZH_DELL_SMM_RWREG,
                ref cam,
                (uint)Marshal.SizeOf(cam),
                ref cam,
                (uint)Marshal.SizeOf(cam),
                ref result_size,
                IntPtr.Zero);

            thisProcess.ProcessorAffinity = defaultProcessAffinity;

            return status_dic ? cam.cmd : 0;
        }

        public bool BDSID_RemoveDriver()
        {
            BDSID_StopDriver();

            IntPtr hSCManager = Interop.OpenSCManagerW(null, null, (uint)Interop.SCM_ACCESS.SC_MANAGER_CONNECT);

            if (hSCManager == IntPtr.Zero)
            {
                return false;
            }

            IntPtr hService = Interop.OpenServiceW(hSCManager, driverName, Interop.SERVICE_QUERY_CONFIG | Interop.DELETE);
            Interop.CloseServiceHandle(hSCManager);

            if (hService == IntPtr.Zero)
            {
                return false;
            }

            bool bResult = Interop.QueryServiceConfigW(hService, IntPtr.Zero, 0, out uint dwBytesNeeded);

            if (Marshal.GetLastWin32Error() == Interop.ERROR_INSUFFICIENT_BUFFER)
            {
                uint cbBufSize = dwBytesNeeded;
                IntPtr ptr = Marshal.AllocHGlobal((int)dwBytesNeeded);

                bResult = Interop.QueryServiceConfigW(hService, ptr, cbBufSize, out dwBytesNeeded);

                if (!bResult)
                {
                    Marshal.FreeHGlobal(ptr);
                    Interop.CloseServiceHandle(hService);
                    return false;
                }

                var pServiceConfig = (Interop.QUERY_SERVICE_CONFIG)Marshal.PtrToStructure(ptr, typeof(Interop.QUERY_SERVICE_CONFIG));

                // If service is set to load automatically, don't delete it!
                if (pServiceConfig.dwStartType == Interop.SERVICE_DEMAND_START)
                {
                    bResult = Interop.DeleteService(hService);
                }

                Marshal.FreeHGlobal(ptr);
            }

            Interop.CloseServiceHandle(hService);

            return bResult;
        }

        public bool BDSID_StopDriver()
        {
            Interop.SERVICE_STATUS serviceStatus = new Interop.SERVICE_STATUS();

            IntPtr hSCManager = Interop.OpenSCManagerW(null, null, (uint)Interop.SCM_ACCESS.SC_MANAGER_CONNECT);

            if (hSCManager == IntPtr.Zero)
                return false;

            IntPtr hService = Interop.OpenServiceW(hSCManager, driverName, Interop.SERVICE_STOP);

            Interop.CloseServiceHandle(hSCManager);

            if (hService == IntPtr.Zero)
                return false;

            Interop.ControlService(hService, Interop.SERVICE_CONTROL.STOP, ref serviceStatus);
            Interop.CloseServiceHandle(hService);

            return true;
        }

        public void Close()
        {
            if (hDriver != Interop.INVALID_HANDLE_VALUE)
            {
                Interop.CloseHandle(hDriver);
                hDriver = Interop.INVALID_HANDLE_VALUE;
            }
        }
        public bool BDSID_Shutdown()
        {
            Close();
            return BDSID_RemoveDriver();
        }

        public static string getDriverPath()
        {
            string exeDirname;
            try
            {
                exeDirname = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
            catch
            {
                exeDirname = System.IO.Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            }
            return exeDirname + "\\bzh_dell_smm_io_x64.sys";
        }

        public void Dispose()
        {
            thisProcess.Dispose();
        }
    }
}
