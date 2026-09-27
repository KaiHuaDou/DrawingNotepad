using System;

namespace InkCanvasNext;

internal sealed class RingBuffer<T>(int capacity)
{
    private readonly T[] buffer = new T[capacity];
    private int head;

    internal int Count { get; private set; }

    internal int Capacity { get; } = capacity;

    internal T this[int index]
    {
        get
        {
            if ((uint) index >= (uint) Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return buffer[(head + index) % Capacity];
        }
    }

    internal void Enqueue(T item)
    {
        if (Count == Capacity)
        {
            buffer[head] = item;
            head = (head + 1) % Capacity;
        }
        else
        {
            buffer[(head + Count) % Capacity] = item;
            Count++;
        }
    }

    internal void Truncate(int newCount)
    {
        if (newCount < 0 || newCount > Count)
        {
            throw new ArgumentOutOfRangeException(nameof(newCount));
        }

        for (var i = newCount; i < Count; i++)
        {
            buffer[(head + i) % Capacity] = default!;
        }

        Count = newCount;
    }

    internal void Clear( )
    {
        Array.Clear(buffer, 0, Capacity);
        head = 0;
        Count = 0;
    }

    internal T[] ToArray( )
    {
        var arr = new T[Count];
        for (var i = 0; i < Count; i++)
        {
            arr[i] = buffer[(head + i) % Capacity];
        }

        return arr;
    }
}
