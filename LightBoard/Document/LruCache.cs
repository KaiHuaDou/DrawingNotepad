using System.Collections.Generic;
using System.Threading;
using System.Windows.Media;

namespace LightBoard;

/// <summary>
/// 文档页图像的最近使用缓存：翻页来回切换时省掉一次整页光栅化。
/// 图像必须已冻结（本类型自身加锁，但图像会被跨线程读取）。
/// </summary>
internal sealed class LruCache(int capacity)
{
    private readonly List<(int Index, ImageSource Image)> entries = [];
    private readonly Lock gate = new( );

    /// <summary>
    /// 命中则返回图像并置为最近使用；未命中返回 null。
    /// </summary>
    internal ImageSource? Take(int index)
    {
        lock (gate)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Index != index)
                {
                    continue;
                }

                var entry = entries[i];
                entries.RemoveAt(i);
                entries.Add(entry);
                return entry.Image;
            }
        }

        return null;
    }

    /// <summary>
    /// 加入缓存，超出容量时淘汰最久未使用者。
    /// </summary>
    internal void Put(int index, ImageSource image)
    {
        lock (gate)
        {
            entries.RemoveAll(e => e.Index == index);
            entries.Add((index, image));
            if (entries.Count > capacity)
            {
                entries.RemoveAt(0);
            }
        }
    }
}
