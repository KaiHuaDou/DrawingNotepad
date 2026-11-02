#if OPENCV
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.MediaFoundation;

namespace LightBoard;

/// <summary>
/// 采集设备：枚举下标（OpenCV 按同一顺序开设备）、显示名与可申请的最大画面档
/// </summary>
internal readonly record struct CameraDevice(int Index, string Name, CameraFormat Format);

// 视频采集设备清单：顺序与 OpenCV 的 MSMF 后端设备索引一致（两者同出 MFEnumDeviceSources）。
// 失败时返回空表，调用方按无设备处理。
internal static class CameraDevices
{
    // MF_VERSION = MF_SDK_VERSION 0x0002 << 16 | MF_API_VERSION 0x0070；MFSTARTUP_LITE 等价 MFSTARTUP_NOSOCKET
    private const uint MfVersion = 0x00020070;
    private const uint MfStartupLite = 0x00000001;

    // 设备名 → 探测到的档位：设备被本进程占用时探测会失败，重进时直接用上次结果
    private static readonly Dictionary<string, CameraFormat> Known = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// 枚举设备，只探测 <paramref name="preferred"/> 指向的那一台；其余用上次探测的结果
    /// </summary>
    public static List<CameraDevice> Enumerate(int preferred)
    {
        List<CameraDevice> devices = [];
        unsafe
        {
            try
            {
                PInvoke.MFStartup(MfVersion, MfStartupLite).ThrowOnFailure( );

                PInvoke.MFCreateAttributes(out var attributes, 1).ThrowOnFailure( );
                attributes.SetGUID(PInvoke.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE, PInvoke.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID);
                PInvoke.MFEnumDeviceSources(attributes, out var sources, out var count).ThrowOnFailure( );

                var deviceCount = (int) count;
                var selected = deviceCount > 0 ? Resolve(preferred, deviceCount) : -1;

                for (var i = 0; i < deviceCount; i++)
                {
                    var source = sources[i];
                    source->GetAllocatedString(PInvoke.MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME, out var name, out _);
                    var nameText = name.Value is null ? "" : name.ToString( );
                    Marshal.FreeCoTaskMem((nint) name.Value);

                    // 表长必须与设备数一致：OpenCV 按同一次枚举的下标开设备，跳过空名会让两者错位
                    var deviceName = nameText.Length > 0 ? nameText : $"摄像头 {i + 1}";
                    var format = i == selected ? Probe(deviceName, source) : Known.GetValueOrDefault(deviceName);
                    devices.Add(new CameraDevice(i, deviceName, format));
                    source->Release( );
                }

                Marshal.FreeCoTaskMem((nint) sources);
            }
            catch (Exception e)
            {
                App.LogException(e);
            }
            finally
            {
                PInvoke.MFShutdown( );
            }
        }

        return devices;
    }

    /// <summary>
    /// 把设备下标归一进 <paramref name="count"/> 台设备的表内，越界与负数都绕回；表为空时返回 0
    /// </summary>
    public static int Resolve(int preferred, int count)
    {
        return count > 0 ? (int) (((long) preferred % count + count) % count) : 0;
    }

    // 探测过就复用：设备被本进程占用时重测会失败，也省掉一次设备激活
    private static unsafe CameraFormat Probe(string name, IMFActivate_unmanaged* source)
    {
        if (Known.TryGetValue(name, out var known))
        {
            return known;
        }

        var format = CameraFormats.Best(source);
        if (!format.IsEmpty)
        {
            Known[name] = format;
        }

        return format;
    }
}
#endif
