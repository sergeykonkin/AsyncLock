/*
MIT License

Copyright (c) 2018 Sergey Konkin

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AsyncUtil
{
    using WaiterQueue = Queue<TaskCompletionSource<IDisposable>>;

    public static class AsyncMonitor
    {
        private static readonly IDictionary<object, byte> _takenLocks;
        private static readonly IDictionary<object, WaiterQueue> _waiterQueues;

        static AsyncMonitor()
        {
            _takenLocks = new ConcurrentDictionary<object, byte>();
            _waiterQueues = new ConcurrentDictionary<object, WaiterQueue>();
        }

        public static Task<IDisposable> EnterAsync(object obj)
        {
            lock (obj)
            {
                if (!_takenLocks.ContainsKey(obj))
                {
                    _takenLocks[obj] = default(byte);
                    return Task.FromResult<IDisposable>(new Lock(obj));
                }

                var waiterCompletionSource =
                    new TaskCompletionSource<IDisposable>(TaskCreationOptions.RunContinuationsAsynchronously);

                if (!_waiterQueues.ContainsKey(obj))
                {
                    _waiterQueues[obj] = new WaiterQueue();
                }

                _waiterQueues[obj].Enqueue(waiterCompletionSource);
                return waiterCompletionSource.Task;
            }
        }

        public static void Exit(object obj)
        {
            lock (obj)
            {
                if (!_waiterQueues.TryGetValue(obj, out var queue))
                {
                    _takenLocks.Remove(obj);
                    return;
                }

                var waiterCompletionSource = _waiterQueues[obj].Dequeue();

                if (queue.Count == 0)
                {
                    _waiterQueues.Remove(obj);
                }

                waiterCompletionSource.SetResult(new Lock(obj));
            }
        }

        private sealed class Lock : IDisposable
        {
            private readonly object _key;

            public Lock(object key)
            {
                _key = key;
            }

            public void Dispose()
            {
                Exit(_key);
            }
        }
    }
}

