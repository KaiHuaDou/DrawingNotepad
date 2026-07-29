using System;

namespace InkCanvasNext;

internal sealed class RingBuffer<T>(int capacity)
{
    private readonly T[] buffer = new T[capacity];
    private readonly int capacity = capacity;
    private int head;
    private int count;

    public int Count => count;

    public T this[int index]
    {
        get
        {
            if ((uint) index >= (uint) count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return buffer[(head + index) % capacity];
        }
    }

    public void Enqueue(T item)
    {
        if (count == capacity)
        {
            buffer[head] = item;
            head = (head + 1) % capacity;
        }
        else
        {
            buffer[(head + count) % capacity] = item;
            count++;
        }
    }

    public void Truncate(int newCount)
    {
        if (newCount < 0 || newCount > count)
        {
            throw new ArgumentOutOfRangeException(nameof(newCount));
        }

        count = newCount;
    }

    public void Clear( )
    {
        Array.Clear(buffer, 0, capacity);
        head = 0;
        count = 0;
    }

    public T[] ToArray( )
    {
        var arr = new T[count];
        for (var i = 0; i < count; i++)
        {
            arr[i] = buffer[(head + i) % capacity];
        }

        return arr;
    }
}
