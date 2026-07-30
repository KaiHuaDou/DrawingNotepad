using System.Collections.Generic;

namespace LightBoard.Raster;

internal sealed class LruCache<TKey, TValue>(int capacity) where TKey : notnull
{
    private readonly int capacity = capacity;
    private readonly Dictionary<TKey, TValue> values = [];
    private readonly LinkedList<TKey> order = [];
    private readonly object gate = new( );

    public bool TryGet(TKey key, out TValue? value)
    {
        lock (gate)
        {
            if (values.TryGetValue(key, out value))
            {
                order.Remove(key);
                order.AddFirst(key);
                return true;
            }

            return false;
        }
    }

    public void Set(TKey key, TValue value)
    {
        lock (gate)
        {
            if (values.TryGetValue(key, out _))
            {
                values[key] = value;
                order.Remove(key);
                order.AddFirst(key);
                return;
            }

            values[key] = value;
            order.AddFirst(key);

            while (values.Count > capacity && order.Count > 0)
            {
                var last = order.Last!;
                order.RemoveLast( );
                values.Remove(last.Value);
            }
        }
    }

    public void Clear( )
    {
        lock (gate)
        {
            values.Clear( );
            order.Clear( );
        }
    }
}
