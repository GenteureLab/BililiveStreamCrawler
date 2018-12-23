using System.Collections.Generic;

namespace BililiveStreamCrawler.Common
{
    public class LinkedListQueue<T>
    {
        public LinkedList<T> Rawlist { get; } = new LinkedList<T>();

        public int Count => Rawlist.Count;

        public T Peek() => Rawlist.First.Value;

        public bool Remove(T item) => Rawlist.Remove(item);

        public bool Contains(T item) => Rawlist.Contains(item);

        public void Enqueue(T item) => Rawlist.AddLast(item);

        public void AddFirst(T item) => Rawlist.AddFirst(item);

        public T Dequeue()
        {
            var result = Rawlist.First.Value;
            Rawlist.RemoveFirst();
            return result;
        }
    }
}
