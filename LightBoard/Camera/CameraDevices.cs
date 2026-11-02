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

    public static List<CameraDevice> Enumerate( )
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

                for (var i = 0; i < count; i++)
                {
                    var source = sources[i];
                    source->GetAllocatedString(PInvoke.MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME, out var name, out _);
                    var nameText = name.Value is null ? "" : name.ToString( );
                    Marshal.FreeCoTaskMem((nint) name.Value);

                    // 表长必须与设备数一致：OpenCV 按同一次枚举的下标开设备，跳过空名会让两者错位
                    devices.Add(new CameraDevice(
                        i,
                        nameText.Length > 0 ? nameText : $"摄像头 {i + 1}",
                        CameraFormats.Best(source)));
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
}
#endif
