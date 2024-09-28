using System;
using System.Collections.Generic;

namespace GameCore.Utility;

public static class ListPool
{
    private const int Limit = 100;
    private static readonly Dictionary<Type, Queue<object>> s_pool = [];

    public static List<T> Get<T>() where T : IPoolable
    {
        Type type = typeof(T);

        if (s_pool.TryGetValue(type, out Queue<object>? poolQueue) && poolQueue.Count > 0)
            return (List<T>)poolQueue.Dequeue();

        return [];
    }

    public static void Return<T>(List<T> list) where T : IPoolable
    {
        foreach (T item in list)
            item.ReturnToPool();

        list.Clear();
        Type type = typeof(T);

        if (!s_pool.TryGetValue(type, out Queue<object>? poolQueue))
        {
            poolQueue = new();
            s_pool[type] = poolQueue;
        }

        if (poolQueue.Count < Limit)
            poolQueue.Enqueue(list);
    }

    public static void ReturnToPool<T>(this List<T> list) where T : IPoolable => Return(list);
}