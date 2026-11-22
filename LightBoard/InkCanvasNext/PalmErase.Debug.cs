using System.Windows;

namespace InkCanvasNext;

/// <summary>
/// 掌擦调试快照：由 DebugVisual 按需读取，触摸事件路径不参与计算
/// </summary>
public partial class InkCanvasNext
{
    internal PalmDebugSnapshot PalmDebug
    {
        get
        {
            var palmCount = 0;
            var last = default(Size);

            foreach (var contact in touches.Values)
            {
                if (!contact.IsPalm)
                {
                    continue;
                }

                palmCount++;
                last = contact.Size;
            }

            if (palmCount == 0)
            {
                return EmptyPalmDebug( );
            }

            var geometry = GetPalmGeometry( );
            var final = areaEraseCycle && frozenPalmDiameter > 0
                ? frozenPalmDiameter
                : geometry.RawDiameter;

            return new(
                palmCount,
                IsUsableContactSize(last),
                last,
                geometry.Width,
                geometry.Height,
                geometry.Diagonal,
                geometry.AreaDiameter,
                geometry.EnvelopeDiameter,
                final,
                areaEraseCycle,
                options.PalmMinWidth,
                options.PalmMinHeight);
        }
    }

    private PalmDebugSnapshot EmptyPalmDebug( )
    {
        return new(0, false, default, 0, 0, 0, 0, 0, 0, false, options.PalmMinWidth, options.PalmMinHeight);
    }
}

internal readonly record struct PalmDebugSnapshot(
    int PalmCount,
    bool HasContactSize,
    Size LastContact,
    double ClusterWidth,
    double ClusterHeight,
    double Diagonal,
    double AreaDiameter,
    double EnvelopeDiameter,
    double FinalDiameter,
    bool IsAreaPalm,
    double MinWidth,
    double MinHeight);
