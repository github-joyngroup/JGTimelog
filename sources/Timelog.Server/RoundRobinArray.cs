using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelog.Server
{
    internal class RoundRobinArray<T> where T : struct
    {
        private readonly T[] _array;
        private readonly int _limit;
        private int _index = 0;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();


        public RoundRobinArray(int limit)
        {
            _limit = limit;
            _array = new T[limit];
        }

        public void Add(T item)
        {
            _lock.EnterWriteLock();
            try
            {
                _array[_index] = item;
                _index = (_index + 1) % _limit;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public T[] GetItems()
        {
            _lock.EnterReadLock();
            try
            {
                var result = new T[_limit];
                Array.Copy(_array, result, _limit);
                return result;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
}
