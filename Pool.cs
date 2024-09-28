namespace GameCore.Utility;

/// <summary>
/// Represents a pool of objects that can be borrowed and returned. 
/// </summary>
public static class Pool
{
    private static readonly object s_lock = new();
    private static readonly Dictionary<Type, LimitedQueue<IPoolable>> s_pool = [];

    /// <summary>
    /// Populates the provided queue with the specified number of objects.
    /// </summary>
    /// <typeparam name="T">The pool type to allocate to.</typeparam>
    /// <param name="limitedQueue">The queue to populate.</param>
    /// <param name="amount">The amount of objects to allocate to the pool.</param>
    private static void Allocate<T>(LimitedQueue<IPoolable> limitedQueue, int amount)
        where T : IPoolable, new()
    {
        int toAllocate = Math.Max(amount, 0);

        if (limitedQueue.Limit != -1)
            toAllocate = Math.Min(toAllocate, limitedQueue.Limit - limitedQueue.Count);

        for (int i = 0; i < toAllocate; i++)
        {
            IPoolable obj = new T();
            limitedQueue.Enqueue(obj);
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

        if (!s_pool.TryGetValue(type, out LimitedQueue<IPoolable>? limitedQueue))
            throw new UnregisteredTypeException(type);

        Allocate<T>(limitedQueue, amount);
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

        if (!s_pool.TryGetValue(type, out LimitedQueue<IPoolable>? limitedQueue))
            throw new UnregisteredTypeException(type);

        lock (s_lock)
        {
            Allocate<T>(limitedQueue, amount);
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

        if (!s_pool.TryGetValue(type, out LimitedQueue<IPoolable>? limitedQueue))
            throw new UnregisteredTypeException(type);

        return limitedQueue.Count > 0 ? (T)limitedQueue.Dequeue() : new();
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

        if (!s_pool.TryGetValue(type, out LimitedQueue<IPoolable>? limitedQueue))
            throw new UnregisteredTypeException(type);

        lock (s_lock)
        {
            return limitedQueue.Count > 0 ? (T)limitedQueue.Dequeue() : new();
        }
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

        if (!s_pool.TryGetValue(type, out LimitedQueue<IPoolable>? limitedQueue))
            throw new UnregisteredTypeException(type);

        return (T)limitedQueue.CreateFunc();
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
        LimitedQueue<IPoolable> limitedQueue = new(limit, () => Get<T>());
        s_pool[type] = limitedQueue;
        Allocate<T>(limitedQueue, preAllocateAmount);
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
        LimitedQueue<IPoolable> limitedQueue = new(limit, () => Get<T>());
        s_pool[type] = limitedQueue;

        lock (s_lock)
        {
            Allocate<T>(limitedQueue, preAllocateAmount);
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

        if (!s_pool.TryGetValue(type, out LimitedQueue<IPoolable>? limitedQueue))
            throw new UnregisteredTypeException(type);

        limitedQueue.Enqueue(poolable);
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

        if (!s_pool.TryGetValue(type, out LimitedQueue<IPoolable>? limitedQueue))
            throw new UnregisteredTypeException(type);

        lock (s_lock)
        {
            limitedQueue.Enqueue(poolable);
        }
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
