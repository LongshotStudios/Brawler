using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class RingBuffer<T>
{
    private T[] buffer;
    private int first;
    private int last;

    public RingBuffer(uint size = 64)
    {
        buffer = new T[size];
        first = 0;
        last = 0;
    }
    
    private int ringIndex(int i) {
        return i % buffer.Length;
    }

    public int Count {
        get => (last >= first) ? last - first : last + buffer.Length - first;
    }
    
    public bool Empty {
        get => first == last;   
    }
    
    public ref T this[int key] {
        get => ref buffer[ringIndex(key + first)];
    }

    public ref T Front {
        get => ref buffer[first];
    }

    public bool Push(T item) {
        var newLast = ringIndex(last + 1);
        if (newLast == first) {
            return false;
        }
        buffer[last] = item;
        last = newLast;
        return true;
    }

    public bool Pop() {
        if (Empty) {
            return false;
        }
        first = ringIndex(first + 1);
        return true;
    }
}
