using System;
using System.Globalization;

namespace LightBoard;

// 抽选状态的抽选部分：取下标，连续模式换成数字，集合模式取元素
public sealed partial class LotteryViewModel
{
    public void DrawOne( )
    {
        Draw(1);
    }

    public void DrawMany( )
    {
        Draw(Count);
    }

    private void Draw(int drawCount)
    {
        var pool = PoolSize;
        if (pool == 0)
        {
            return;
        }

        var indices = LotteryDraw.PickIndices(Random.Shared, pool, drawCount);
        Results.Clear( );

        if (Mode == LotteryMode.Continuous)
        {
            foreach (var index in indices)
            {
                Results.Add((Lower + index).ToString(CultureInfo.InvariantCulture));
            }

            return;
        }

        if (SelectedDataset is { } dataset)
        {
            foreach (var index in indices)
            {
                Results.Add(dataset.Items[index]);
            }
        }
    }
}
