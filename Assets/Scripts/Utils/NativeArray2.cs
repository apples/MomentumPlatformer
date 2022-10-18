// Copied and modified from:
// https://gist.github.com/a3geek/8532817159b77c727040cf67c92af322

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

public struct NativeArray2<T> : IDisposable where T : unmanaged
{
    private NativeArray<T> array;
    private int width;

    public NativeArray2(int width, int height, Allocator allocator)
    {
        this.array = new NativeArray<T>(width * height, allocator);
        this.width = width;
    }

    public T this[int y, int x]
    {
        get => array[y * width + x];
        set => array[y * width + x] = value;
    }

    public void Dispose()
    {
        array.Dispose();
    }
}
