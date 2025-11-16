using Playnite.SDK;
using DisplayHelper.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DisplayHelper.Audio
{
    public static class AudioUtilities
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public static List<AudioDeviceInfo> GetPlaybackDevices()
        {
            var devices = new List<AudioDeviceInfo>();
            IMMDeviceEnumerator enumerator = null;
            IMMDeviceCollection collection = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                var hr = enumerator.EnumAudioEndpoints(EDataFlow.eRender, DeviceState.ACTIVE, out collection);
                if (hr != 0)
                {
                    logger.Warn($"Failed to enumerate audio endpoints. HRESULT: {hr}");
                    return devices;
                }

                collection.GetCount(out uint deviceCount);
                var defaultId = GetDefaultAudioDeviceId(enumerator);

                for (uint deviceIndex = 0; deviceIndex < deviceCount; deviceIndex++)
                {
                    collection.Item(deviceIndex, out var device);
                    if (device is null)
                    {
                        continue;
                    }

                    try
                    {
                        var id = GetDeviceId(device);
                        var name = GetDeviceFriendlyName(device);
                        if (id.IsNullOrEmpty())
                        {
                            continue;
                        }

                        var isDefault = !defaultId.IsNullOrEmpty() && id.Equals(defaultId, StringComparison.OrdinalIgnoreCase);
                        devices.Add(new AudioDeviceInfo(id, name, isDefault));
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(device);
                    }
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to enumerate audio devices.");
            }
            finally
            {
                if (collection != null)
                {
                    Marshal.ReleaseComObject(collection);
                }

                if (enumerator != null)
                {
                    Marshal.ReleaseComObject(enumerator);
                }
            }

            return devices;
        }

        public static string GetDefaultAudioDeviceId()
        {
            IMMDeviceEnumerator enumerator = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                return GetDefaultAudioDeviceId(enumerator);
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to get default audio device id.");
                return string.Empty;
            }
            finally
            {
                if (enumerator != null)
                {
                    Marshal.ReleaseComObject(enumerator);
                }
            }
        }

        private static string GetDefaultAudioDeviceId(IMMDeviceEnumerator enumerator)
        {
            try
            {
                enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device);
                if (device is null)
                {
                    return string.Empty;
                }

                try
                {
                    return GetDeviceId(device);
                }
                finally
                {
                    Marshal.ReleaseComObject(device);
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to read default audio device id.");
                return string.Empty;
            }
        }

        public static bool SetDefaultAudioPlaybackDevice(string deviceId)
        {
            if (deviceId.IsNullOrEmpty())
            {
                return false;
            }

            IPolicyConfig policyConfig = null;
            try
            {
                policyConfig = (IPolicyConfig)new PolicyConfigClient();
                var roles = new[] { ERole.eConsole, ERole.eMultimedia, ERole.eCommunications };
                foreach (var role in roles)
                {
                    var hr = policyConfig.SetDefaultEndpoint(deviceId, role);
                    if (hr != 0)
                    {
                        logger.Warn($"Failed to set default audio endpoint for role {role}. HRESULT: {hr}");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to change default audio playback device.");
                return false;
            }
            finally
            {
                if (policyConfig != null)
                {
                    Marshal.ReleaseComObject(policyConfig);
                }
            }
        }

        private static string GetDeviceId(IMMDevice device)
        {
            try
            {
                device.GetId(out var id);
                return id;
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to get audio device id.");
                return string.Empty;
            }
        }

        private static string GetDeviceFriendlyName(IMMDevice device)
        {
            IPropertyStore store = null;
            try
            {
                device.OpenPropertyStore(StorageAccessMode.STGM_READ, out store);
                var propertyKey = PropertyKeys.PKEY_Device_FriendlyName;
                store.GetValue(ref propertyKey, out var prop);
                var name = prop.Value;
                PropVariantHelper.Clear(ref prop);
                return name;
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to read audio device friendly name.");
                return ResourceProvider.GetString("LOCDisplayHelper_AudioDeviceUnknownLabel");
            }
            finally
            {
                if (store != null)
                {
                    Marshal.ReleaseComObject(store);
                }
            }
        }
    }

    #region Interop

    [Flags]
    internal enum DeviceState : uint
    {
        ACTIVE = 0x00000001,
        DISABLED = 0x00000002,
        NOTPRESENT = 0x00000004,
        UNPLUGGED = 0x00000008,
        MASK_ALL = 0x0000000F
    }

    internal enum EDataFlow
    {
        eRender,
        eCapture,
        eAll
    }

    internal enum ERole
    {
        eConsole,
        eMultimedia,
        eCommunications
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PropertyKey
    {
        public Guid fmtid;
        public int pid;

        public PropertyKey(Guid formatId, int propertyId)
        {
            fmtid = formatId;
            pid = propertyId;
        }
    }

    internal static class PropertyKeys
    {
        public static readonly PropertyKey PKEY_Device_FriendlyName = new PropertyKey(new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), 14);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PropVariant
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr pointerValue;
        public int intValue;
        public IntPtr pointerValue2;

        public string Value
        {
            get
            {
                if (vt == 31) // VT_LPWSTR
                {
                    return Marshal.PtrToStringUni(pointerValue);
                }

                if (vt == 30) // VT_LPSTR
                {
                    return Marshal.PtrToStringAnsi(pointerValue);
                }

                return string.Empty;
            }
        }
    }

    internal static class PropVariantHelper
    {
        [DllImport("Ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant pvar);

        public static void Clear(ref PropVariant prop)
        {
            PropVariantClear(ref prop);
        }
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumerator
    {
    }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(EDataFlow dataFlow, DeviceState dwStateMask, out IMMDeviceCollection ppDevices);
        [PreserveSig]
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
        [PreserveSig]
        int RegisterEndpointNotificationCallback(IntPtr pClientInterface);
        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IntPtr pClientInterface);
    }

    [Guid("0BD7A1BE-7A1A-44DB-8397-C0A7946CB8B4"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection
    {
        [PreserveSig]
        int GetCount(out uint pcDevices);
        [PreserveSig]
        int Item(uint nDevice, out IMMDevice ppDevice);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        [PreserveSig]
        int OpenPropertyStore(StorageAccessMode stgmAccess, out IPropertyStore ppProperties);
        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
        [PreserveSig]
        int GetState(out DeviceState pdwState);
    }

    internal enum StorageAccessMode
    {
        STGM_READ = 0x00000000,
        STGM_WRITE = 0x00000001,
        STGM_READWRITE = 0x00000002
    }

    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint cProps);
        [PreserveSig]
        int GetAt(uint iProp, out PropertyKey pkey);
        [PreserveSig]
        int GetValue(ref PropertyKey key, out PropVariant pv);
        [PreserveSig]
        int SetValue(ref PropertyKey key, ref PropVariant propvar);
        [PreserveSig]
        int Commit();
    }

    [ComImport]
    [Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
    internal class PolicyConfigClient
    {
    }

    [Guid("568b9108-44bf-40b4-9006-86afe5b5a620"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfig
    {
        int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr ppFormat);
        int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, bool bDefault, out IntPtr ppFormat);
        int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);
        int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, bool bDefault, out IntPtr pmftDefault, out IntPtr pmftMinimum);
        int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pmftPeriod);
        int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr pMode);
        int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);
        int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref PropertyKey key, out PropVariant pv);
        int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref PropertyKey key, ref PropVariant pv);
        int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ERole role);
        int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, bool bVisible);
    }

    #endregion
}
