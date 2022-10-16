
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ListExtensions
{
    public static void Shuffle<T>(this IList<T> list)
    {
        for (var i = list.Count - 1; i >= 1; --i)
        {
            var k = Random.Range(0, i + 1);
            var tmp = list[k];
            list[k] = list[i];
            list[i] = tmp;
        }
    }


    public static void Resize<T>(this List<T> list, int size, T element = default(T))
    {
        int cur = list.Count;

        if (size < cur)
        {
            list.RemoveRange(size, cur - size);
        }
        else if (size > cur)
        {
            if (size > list.Capacity)
            {
                list.Capacity = size;
            }

            list.AddRange(Enumerable.Repeat(element, size - cur));
        }
    }
}
