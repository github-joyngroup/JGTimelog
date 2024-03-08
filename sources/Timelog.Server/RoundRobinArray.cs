﻿using System;
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
        private int _roundRobinCounter = 0;
        
        /// <summary>
        /// The current index of the array
        /// </summary>
        public int CurrentIndex
        {
            get
            {
                _lock.EnterReadLock();
                try { return _index; }
                finally { _lock.ExitReadLock(); }
            }
        }
        
        /// <summary>
        /// The current round robin counter, meaning how many times the array has been filled
        /// </summary>
        public int RoundRobinCounter
        {
            get
            {
                _lock.EnterReadLock();
                try { return _roundRobinCounter; }
                finally { _lock.ExitReadLock(); }
            }
        }

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
                
                //increment the round robin counter each time we reach the limit
                if (_index == 0)
                {
                    _roundRobinCounter++;
                }

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
