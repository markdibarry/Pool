using System;
using System.Collections.Generic;

namespace GameCore.Utility;

/// <summary>
/// Represents a pool of objects that can be borrowed and returned. 
/// </summary>
public static class Pool
{
    private static readonly object s_lock = new();
    private static readonly Dictionary<Type, PoolQueue<IPoolable>> s_pool = [];
    private static readonly Dictionary<Type, LimitedQueue<object>> s_listPool = [];
    private const int LimitDefault = 100;

    /// <summary>
    /// Populates the provided queue with the specified number of objects.
    /// </summary>
    /// <typeparam name="T">The pool type to allocate to.</typeparam>
    /// <param name="poolQueue">The queue to populate.</param>
    /// <param name="amount">The amount of objects to allocate to the pool.</param>
    private static void Allocate<T>(PoolQueue<IPoolable> poolQueue, int amount)
        where T : IPoolable, new()
    {
        int toAllocate = Math.Max(amount, 0);

        if (poolQueue.Limit != -1)
            toAllocate = Math.Min(toAllocate, poolQueue.Limit - poolQueue.Count);

        for (int i = 0; i < toAllocate; i++)
        {
            IPoolable obj = new T();
            poolQueue.Enqueue(obj);
        }
    }

    /// <summary>
    /// Populates an existing pool with the specified amount of objects.
    /// </summary>
    /// <typeparam name="T">The pool type to allocate to.</typeparam>
    /// <param name="amount">The amount of objects to allocate to the pool.</param>
    /// <exception cref="UnregisteredTypeException"></exception>
    public static void Allocate<T>(int amount) where T : IPoolable, new()
    {
        Type type = typeof(T);

        if (!s_pool.TryGetValue(type, out PoolQueue<IPoolable>? poolQueue))
            throw new UnregisteredTypeException(type);

        Allocate<T>(poolQueue, amount);
    }

    /// <summary>
    /// Populates an existing pool in a thread-safe way.
    /// </summary>
    /// <typeparam name="T">The pool type to allocate to.</typeparam>
    /// <param name="amount">The amount of objects to allocate to the pool.</param>
    /// <exception cref="UnregisteredTypeException"></exception>
    public static void AllocateSafe<T>(int amount) where T : IPoolable, new()
    {
        Type type = typeof(T);

        if (!s_pool.TryGetValue(type, out PoolQueue<IPoolable>? poolQueue))
            throw new UnregisteredTypeException(type);

        lock (s_lock)
        {
            Allocate<T>(poolQueue, amount);
        }
    }

    /// <summary>
    /// Retrieves an object from the pool of a registered type.
    /// If the pool is empty, a new object is created.
    /// </summary>
    /// <typeparam name="T">The type of object to borrow.</typeparam>
    /// <returns>An object of the specified type</returns>
    /// <exception cref="UnregisteredTypeException"></exception>
    public static T Get<T>() where T : IPoolable, new()
    {
        Type type = typeof(T);

        if (!s_pool.TryGetValue(type, out PoolQueue<IPoolable>? poolQueue))
            throw new UnregisteredTypeException(type);

        return poolQueue.Count > 0 ? (T)poolQueue.Dequeue() : new();
    }

    /// <summary>
    /// Retrieves an object from the pool of a registered type in a thread-safe way.
    /// If the pool is empty, a new object is created.
    /// </summary>
    /// <typeparam name="T">The type of object to borrow.</typeparam>
    /// <returns>An object of the specified type</returns>
    /// <exception cref="UnregisteredTypeException"></exception>
    public static T GetSafe<T>() where T : IPoolable, new()
    {
        Type type = typeof(T);

        if (!s_pool.TryGetValue(type, out PoolQueue<IPoolable>? poolQueue))
            throw new UnregisteredTypeException(type);

        lock (s_lock)
        {
            return poolQueue.Count > 0 ? (T)poolQueue.Dequeue() : new();
        }
    }

    /// <summary>
    /// Retrieves a List from the pool of a registered type.
    /// If the pool is empty, a new List is created.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static List<T> GetList<T>() where T : IPoolable
    {
        Type type = typeof(T);

        if (!s_listPool.TryGetValue(type, out LimitedQueue<object>? poolQueue))
            return [];

        return poolQueue.Count > 0 ? (List<T>)poolQueue.Dequeue() : [];
    }

    /// <summary>
    /// Retrieves an object from the pool of the same underlying registered type as the one provided.
    /// If the pool is empty, a new object is created.
    /// </summary>
    /// <typeparam name="T">The type of object to return.</typeparam>
    /// <param name="poolable">The object with the underlying registered type desired.</param>
    /// <returns>An object of the same type as the object provided.</returns>
    /// <exception cref="UnregisteredTypeException"></exception>
    public static T GetOfSameType<T>(this T poolable) where T : IPoolable
    {
        Type type = poolable.GetType();

        if (!s_pool.TryGetValue(type, out PoolQueue<IPoolable>? poolQueue))
            throw new UnregisteredTypeException(type);

        return poolQueue.Count > 0 ? (T)poolQueue.Dequeue() : (T)poolQueue.CreateFunc();
    }

    /// <summary>
    /// Registers a type for use with the pool.
    /// </summary>
    /// <typeparam name="T">The type to register.</typeparam>
    /// <param name="preAllocateAmount">Pre-populates the pool with objects of the provided
    /// type with the amount specified.</param>
    /// <param name="limit">Ensures the pool stops storing objects once the specified
    /// amount has been reached. No limit is enforced if a value of -1 is provided.</param>
    public static void Register<T>(int preAllocateAmount = 0, int limit = -1)
        where T : IPoolable, new()
    {
        Type type = typeof(T);
        PoolQueue<IPoolable> poolQueue = new(limit, () => Get<T>());
        s_pool[type] = poolQueue;
        Allocate<T>(poolQueue, preAllocateAmount);
    }

    /// <summary>
    /// Registers a type for use with the pool in a thread-safe way.
    /// </summary>
    /// <typeparam name="T">The type to register.</typeparam>
    /// <param name="preAllocateAmount">Pre-populates the pool with objects of the provided
    /// type with the amount specified.</param>
    /// <param name="limit">Ensures the pool stops storing objects once the specified
    /// amount has been reached. No limit is enforced if a value of -1 is provided.</param>
    public static void RegisterSafe<T>(int preAllocateAmount = 0, int limit = -1)
        where T : IPoolable, new()
    {
        Type type = typeof(T);
        PoolQueue<IPoolable> poolQueue = new(limit, () => Get<T>());
        s_pool[type] = poolQueue;

        lock (s_lock)
        {
            Allocate<T>(poolQueue, preAllocateAmount);
        }
    }

    /// <summary>
    /// Returns the provided object to the pool of the underlying registered type.
    /// </summary>
    /// <param name="poolable">The object to return.</param>
    /// <exception cref="UnregisteredTypeException"></exception>
    public static void Return(IPoolable poolable)
    {
        poolable.ClearObject();
        Type type = poolable.GetType();

        if (!s_pool.TryGetValue(type, out PoolQueue<IPoolable>? poolQueue))
            throw new UnregisteredTypeException(type);

        poolQueue.Enqueue(poolable);
    }

    /// <summary>
    /// Returns the provided object to the pool of the underlying registered type in a thread-safe
    /// way.
    /// </summary>
    /// <param name="poolable">The object to return.</param>
    /// <exception cref="UnregisteredTypeException"></exception>
    public static void ReturnSafe(IPoolable poolable)
    {
        poolable.ClearObject();
        Type type = poolable.GetType();

        if (!s_pool.TryGetValue(type, out PoolQueue<IPoolable>? poolQueue))
            throw new UnregisteredTypeException(type);

        lock (s_lock)
        {
            poolQueue.Enqueue(poolable);
        }
    }

    /// <summary>
    /// Returns the provided List to the pool of the underlying registered type.
    /// If the List contains IPoolable objects, they will be returned to their pool as well.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    public static void Return<T>(List<T> list)
    {
        Type type = typeof(T);

        if (type.IsAssignableTo(typeof(IPoolable)))
        {
            foreach (T item in list)
            {
                if (item is IPoolable poolable)
                    Return(poolable);
            }
        }

        list.Clear();

        if (!s_listPool.TryGetValue(type, out LimitedQueue<object>? limitedQueue))
        {
            limitedQueue = new(LimitDefault);
            s_listPool[type] = limitedQueue;
        }

        limitedQueue.Enqueue(list);
    }

    /// <summary>
    /// Returns the provided object to the pool of the underlying registered type.
    /// </summary>
    /// <param name="poolable">The object to return.</param>
    public static void ReturnToPool(this IPoolable poolable) => Return(poolable);

    /// <summary>
    /// Returns the provided object to the pool of the underlying registered type in a thread-safe
    /// way.
    /// </summary>
    /// <param name="poolable">The object to return.</param>
    public static void ReturnToPoolSafe(this IPoolable poolable) => ReturnSafe(poolable);

    /// <summary>
    /// An exception for accessing types that are not registered to the pool.
    /// </summary>
    [Serializable]
    private class UnregisteredTypeException : Exception
    {
        public UnregisteredTypeException(Type type)
            : base($"Type \"${type.Name}\" is not registered for Pool.")
        { }
    }
}
