#pragma warning disable CA5394

using System;

namespace LightBoard;

internal static class LotteryDraw
{
    /// <summary>
    /// 从 0 到 total - 1 中等概率取 count 个互不相同的下标，按抽取顺序返回
    /// </summary>
    /// <param name="count">请求数量，超过 total 时取 total</param>
    internal static int[] PickIndices(Random random, int total, int count)
    {
        if (total <= 0)
        {
            return [];
        }

        count = Math.Clamp(count, 0, total);
        if (count == 0)
        {
            return [];
        }

        var indices = new int[total];
        for (var i = 0; i < total; i++)
        {
            indices[i] = i;
        }

        for (var i = 0; i < count; i++)
        {
            var j = random.Next(i, total);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        return indices[..count];
    }
}
