﻿using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Fibrous
{
    public class LockFiber : FiberBase
    {
        private readonly ArrayQueue<Action> _queue;
        private readonly TaskFactory _taskFactory;
        private readonly Action _flushCache;
        private bool _flushPending;
        private readonly object _lock = new object();
        public LockFiber(IExecutor executor = null, int size = QueueSize.DefaultQueueSize, TaskFactory taskFactory = null, IFiberScheduler scheduler = null)
            : base(executor, scheduler)
        {
            _queue = new ArrayQueue<Action>(size);
            _taskFactory = taskFactory ?? new TaskFactory(TaskCreationOptions.PreferFairness, TaskContinuationOptions.None);
            _flushCache = Flush;
        }

        public LockFiber(Action<Exception> errorCallback, int size = QueueSize.DefaultQueueSize, TaskFactory taskFactory = null, IFiberScheduler scheduler = null)
            : this(new ExceptionHandlingExecutor(errorCallback), size, taskFactory, scheduler)
        {
        }

        protected override void InternalEnqueue(Action action)
        {
            var spinWait = default(AggressiveSpinWait);
            while (_queue.IsFull) spinWait.SpinOnce();

            lock (_lock)
            {
                _queue.Enqueue(action);

                if (_flushPending) return;

                _flushPending = true;
                _taskFactory.StartNew(_flushCache);
            }
        }

        private void Flush()
        {
            var (count, actions) = Drain();

            for (var i = 0; i < count; i++)
            {
                Executor.Execute(actions[i]);
            }

            lock (_lock)
            {
                if (_queue.Count > 0)
                    //don't monopolize thread.
                    _taskFactory.StartNew(_flushCache);
                else
                    _flushPending = false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (int, Action[]) Drain()
        {
            lock(_lock)
            { 
                return _queue.Drain();
            }
        }
    }
}