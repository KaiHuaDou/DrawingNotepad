using System.Windows.Input;
using System.Windows.Input.StylusPlugIns;

namespace InkCanvasNext;

/// <summary>
/// 湿墨层过滤：低优先级输入不建立也不追加实时笔画
/// 抬起事件始终交给基类，清掉按下前已经存在的笔画
/// </summary>
internal sealed class FilteredDynamicRenderer(InputArbiter arbiter) : DynamicRenderer
{
    protected override void OnStylusDown(RawStylusInput input)
    {
        if (!arbiter.Allows(arbiter.KindOf(input)))
        {
            return;
        }

        base.OnStylusDown(input);
    }

    protected override void OnStylusMove(RawStylusInput input)
    {
        if (!arbiter.Allows(arbiter.KindOf(input)))
        {
            return;
        }

        base.OnStylusMove(input);
    }

    public override void Reset(StylusDevice stylusDevice, StylusPointCollection stylusPoints)
    {
        if (!arbiter.Allows(InputArbiter.KindOf(stylusDevice)))
        {
            return;
        }

        base.Reset(stylusDevice, stylusPoints);
    }
}
