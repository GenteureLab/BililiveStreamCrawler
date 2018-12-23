using System.Collections.Generic;

namespace BililiveStreamCrawler.Common
{
    public class LinkedListQueue<T>
    {
        private LinkedList<T> list = new LinkedList<T>();

        public int Count => list.Count;

        public T Peek() => list.First.Value;

        public bool Remove(T item) => list.Remove(item);

        public bool Contains(T item) => list.Contains(item);

        public void Enqueue(T item) => list.AddLast(item);

        public T Dequeue()
        {
            var result = list.First.Value;
            list.RemoveFirst();
            return result;
        }
    }
}
