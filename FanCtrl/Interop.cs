using System;
using System.Runtime.InteropServices;

//Parts taken from https://github.com/marcharding/DellFanControl

namespace DellFanControl
{
    public static class Interop
    {
        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [DllImport("advapi32.dll", SetLastError = true)]
        [
            return: MarshalAs(UnmanagedType.Bool)
        ]
        public static extern bool DeleteService(IntPtr hService);

        [StructLayout(LayoutKind.Sequential)]
        public class QUERY_SERVICE_CONFIG
        {
            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dwServiceType;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dwStartType;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dwErrorControl;

            [MarshalAs(UnmanagedType.LPWStr)]
            public String lpBinaryPathName;

            [MarshalAs(UnmanagedType.LPWStr)]
            public String lpLoadOrderGroup;

            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dwTagID;

            [MarshalAs(UnmanagedType.LPWStr)]
            public String lpDependencies;

            [MarshalAs(UnmanagedType.LPWStr)]
            public String lpServiceStartName;

            [MarshalAs(UnmanagedType.LPWStr)]
            public String lpDisplayName;
        };

        [Flags]
        public enum SERVICE_CONTROL : uint
        {
            STOP = 0x00000001,
            PAUSE = 0x00000002,
            CONTINUE = 0x00000003,
            INTERROGATE = 0x00000004,
            SHUTDOWN = 0x00000005,
            PARAMCHANGE = 0x00000006,
            NETBINDADD = 0x00000007,
            NETBINDREMOVE = 0x00000008,
            NETBINDENABLE = 0x00000009,
            NETBINDDISABLE = 0x0000000A,
            DEVICEEVENT = 0x0000000B,
            HARDWAREPROFILECHANGE = 0x0000000C,
            POWEREVENT = 0x0000000D,
            SESSIONCHANGE = 0x0000000E
        }

        public enum SERVICE_STATE : uint
        {
            SERVICE_STOPPED = 0x00000001,
            SERVICE_START_PENDING = 0x00000002,
            SERVICE_STOP_PENDING = 0x00000003,
            SERVICE_RUNNING = 0x00000004,
            SERVICE_CONTINUE_PENDING = 0x00000005,
            SERVICE_PAUSE_PENDING = 0x00000006,
            SERVICE_PAUSED = 0x00000007
        }

        [Flags]
        public enum SERVICE_ACCEPT : uint
        {
            STOP = 0x00000001,
            PAUSE_CONTINUE = 0x00000002,
            SHUTDOWN = 0x00000004,
            PARAMCHANGE = 0x00000008,
            NETBINDCHANGE = 0x00000010,
            HARDWAREPROFILECHANGE = 0x00000020,
            POWEREVENT = 0x00000040,
            SESSIONCHANGE = 0x00000080,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SERVICE_STATUS
        {
            public int serviceType;
            public int currentState;
            public int controlsAccepted;
            public int win32ExitCode;
            public int serviceSpecificExitCode;
            public int checkPoint;
            public int waitHint;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        [
            return: MarshalAs(UnmanagedType.Bool)
        ]
        public static extern bool ControlService(
            IntPtr hService,
            SERVICE_CONTROL dwControl,
            ref SERVICE_STATUS lpServiceStatus
                                                );

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern IntPtr CreateFileW(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            [Optional] IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            [Optional] IntPtr hTemplateFile
                                               );

        [Flags]
        public enum SCM_ACCESS : uint
        {
            STANDARD_RIGHTS_REQUIRED = 0xF0000,
            SC_MANAGER_CONNECT = 0x00001,
            SC_MANAGER_CREATE_SERVICE = 0x00002,
            SC_MANAGER_ENUMERATE_SERVICE = 0x00004,
            SC_MANAGER_LOCK = 0x00008,
            SC_MANAGER_QUERY_LOCK_STATUS = 0x00010,
            SC_MANAGER_MODIFY_BOOT_CONFIG = 0x00020,

            SC_MANAGER_ALL_ACCESS = STANDARD_RIGHTS_REQUIRED |
            SC_MANAGER_CONNECT |
            SC_MANAGER_CREATE_SERVICE |
            SC_MANAGER_ENUMERATE_SERVICE |
            SC_MANAGER_LOCK |
            SC_MANAGER_QUERY_LOCK_STATUS |
            SC_MANAGER_MODIFY_BOOT_CONFIG
        }

        public const int SERVICE_QUERY_CONFIG = 0x0001;
        public const int SERVICE_CHANGE_CONFIG = 0x0002;
        public const int SERVICE_QUERY_STATUS = 0x0004;
        public const int SERVICE_ENUMERATE_DEPENDENTS = 0x0008;
        public const int SERVICE_START = 0x0010;
        public const int SERVICE_STOP = 0x0020;
        public const int SERVICE_PAUSE_CONTINUE = 0x0040;
        public const int SERVICE_INTERROGATE = 0x0080;
        public const int SERVICE_USER_DEFINED_CONTROL = 0x0100;
        public const int SERVICE_CONTROL_STOP = 0x00000001;

        public const int STANDARD_RIGHTS_REQUIRED = 0xF0000;

        public const uint SERVICE_ALL_ACCESS = STANDARD_RIGHTS_REQUIRED |
            SERVICE_QUERY_CONFIG |
            SERVICE_CHANGE_CONFIG |
            SERVICE_QUERY_STATUS |
            SERVICE_ENUMERATE_DEPENDENTS |
            SERVICE_START |
            SERVICE_STOP |
            SERVICE_PAUSE_CONTINUE |
            SERVICE_INTERROGATE |
            SERVICE_USER_DEFINED_CONTROL;

        public const int SERVICE_DEMAND_START = 0x00000003;
        public const int SERVICE_KERNEL_DRIVER = 0x00000001;
        public const int SERVICE_ERROR_NORMAL = 0x00000001;
        public const int SERVICE_ERROR_IGNORE = 0x00000000;

        public const int DELETE = 0x00010000;
        public const int FILE_SHARE_READ = 0x00000001;
        public const int FILE_SHARE_WRITE = 0x00000002;
        public const int FILE_SHARE_DELETE = 0x00000004;
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const int GENERIC_EXECUTE = 0x20000000;
        public const int GENERIC_ALL = 0x10000000;
        public const int FILE_ATTRIBUTE_NORMAL = 0x80;
        public const int OPEN_EXISTING = 3;
        public const uint BZH_DELL_SMM_IOCTL_KEY = 0xB42;
        public const uint FILE_DEVICE_BZH_DELL_SMM = 0x0000B424;
        public const uint IOCTL_BZH_DELL_SMM_RWREG = (FILE_DEVICE_BZH_DELL_SMM << 16) | (BZH_DELL_SMM_IOCTL_KEY << 2);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hHandle);

        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr OpenSCManagerW(
            string machineName,
            string databaseName,
            uint dwAccess
                                                  );

        [DllImport("advapi32.dll", SetLastError = true)]
        [
            return: MarshalAs(UnmanagedType.Bool)
        ]
        public static extern bool CloseServiceHandle(IntPtr hSCObject);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern IntPtr OpenServiceW(
            IntPtr hSCManager,
            string lpServiceName,
            uint dwDesiredAccess
                                                );

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [
            return: MarshalAs(UnmanagedType.Bool)
        ]
        public static extern bool StartServiceW(
            IntPtr hService,
            int dwNumServiceArgs,
            string[] lpServiceArgVectors
                                               );

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        public static extern Boolean QueryServiceConfigW(IntPtr hService, IntPtr intPtrQueryConfig, UInt32 cbBufSize, out UInt32 pcbBytesNeeded);

        public const int ERROR_INSUFFICIENT_BUFFER = 122;

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern IntPtr CreateServiceW(
            IntPtr hSCManager,
            string lpServiceName,
            string lpDisplayName,
            uint dwDesiredAccess,
            uint dwServiceType,
            uint dwStartType,
            uint dwErrorControl,
            string lpBinaryPathName,
            string lpLoadOrderGroup,
            IntPtr lpdwTagId,
            string lpDependencies,
            string lpServiceStartName,
            string lpPassword
                                                  );

        public static uint CTL_CODE(uint DeviceType, uint Function, uint Method, uint Access)
        {
            return (((DeviceType) << 16) | ((Access) << 14) | ((Function) << 2) | (Method));
        }

        public struct SMBIOS_PKG
        {
            public uint cmd;
            public uint data;
            public uint stat1;
            public uint stat2;
        }

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            [In] ref SMBIOS_PKG lpInBuffer,
            [In] uint nInBufferSize,
            ref SMBIOS_PKG lpOutBuffer,
            [In] uint nOutBufferSize,
            out uint lpBytesReturned,
            [Optional] IntPtr lpOverlapped
                                                 );
    }
}