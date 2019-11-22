using System.Collections.Generic;
using System.Linq;

namespace GlassTL.Telegram.Utils
{
    class ConcurrentCircularBuffer<T>
    {
        private readonly LinkedList<T> _buffer;
        private readonly int _maxItemCount;

        public ConcurrentCircularBuffer(int maxItemCount)
        {
            _maxItemCount = maxItemCount;
            _buffer = new LinkedList<T>();
        }

        public void Put(T item)
        {
            lock (_buffer)
            {
                _buffer.AddFirst(item);
                if (_buffer.Count > _maxItemCount)
                {
                    _buffer.RemoveLast();
                }
            }
        }
        public void PutRange(T[] items)
        {
            lock (_buffer)
            {
                foreach (var item in items)
                {
                    _buffer.AddFirst(item);
                    if (_buffer.Count > _maxItemCount)
                    {
                        _buffer.RemoveLast();
                    }
                }
            }
        }

        public IEnumerable<T> Read()
        {
            lock (_buffer) { return _buffer.ToArray(); }
        }
    }
}
