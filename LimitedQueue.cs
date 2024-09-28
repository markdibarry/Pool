namespace GameCore.Utility;

/// <summary>
/// A queue wrapper with an upper limit.
/// </summary>
public class LimitedQueue<T>
{
    public LimitedQueue(int limit, Func<T> createFunc)
    {
        Limit = limit;
        CreateFunc = createFunc;
    }

    /// <summary>
    /// The queue object
    /// </summary>
    public Queue<T> Queue { get; set; } = [];
    /// <summary>
    /// A delegate to create a new object of the queue's underlying type.
    /// </summary>
    public Func<T> CreateFunc { get; set; }
    /// <summary>
    /// The upper limit for adding items to the queue.
    /// </summary>
    public int Limit { get; set; } = -1;
    /// <summary>
    /// Gets the number of elements contained within the queue.
    /// </summary>
    public int Count => Queue.Count;

    /// <summary>
    /// Adds an object to the end of the queue.
    /// </summary>
    /// <param name="item">The object to add to the queue.</param>
    public void Enqueue(T item)
    {
        if (Limit == -1 || Count < Limit)
            Queue.Enqueue(item);
    }

    /// <summary>
    /// Removes and returns an object at the beginning of the queue.
    /// </summary>
    /// <returns>The object that is removed from the beginning of the queue.</returns>
    public T Dequeue() => Queue.Dequeue();
}