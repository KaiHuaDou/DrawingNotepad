using System;

namespace InkCanvasNext;

internal sealed class RingBuffer<T>(int capacity)
{
    private readonly T[] buffer = new T[capacity];
    private readonly int capacity = capacity;
    private int head;

    public int Count { get; private set; }

    public T this[int index]
    {
        get
        {
            if ((uint) index >= (uint) Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return buffer[(head + index) % capacity];
        }
    }

    public void Enqueue(T item)
    {
        if (Count == capacity)
        {
            buffer[head] = item;
            head = (head + 1) % capacity;
        }
        else
        {
            buffer[(head + Count) % capacity] = item;
            Count++;
        }
    }

    public void Truncate(int newCount)
    {
        if (newCount < 0 || newCount > Count)
        {
            throw new ArgumentOutOfRangeException(nameof(newCount));
        }

        for (var i = newCount; i < Count; i++)
        {
            buffer[(head + i) % capacity] = default!;
        }

        Count = newCount;
    }

    public void Clear( )
    {
        Array.Clear(buffer, 0, capacity);
        head = 0;
        Count = 0;
    }

    public T[] ToArray( )
    {
        var arr = new T[Count];
        for (var i = 0; i < Count; i++)
        {
            arr[i] = buffer[(head + i) % capacity];
        }

        return arr;
    }
}
