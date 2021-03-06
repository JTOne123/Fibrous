﻿using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Fibrous
{
    /// <summary>
    /// It is suggested to always use an Exception callback with the IAsyncFiber
    /// </summary>
    public class AsyncFiber : AsyncFiberBase
    {
        private readonly ArrayQueue<Func<Task>> _queue;
        private readonly TaskFactory _taskFactory;
        private readonly Func<Task> _flushCache;
        private bool _flushPending;
        private SpinLock _spinLock = new SpinLock(false);

        public AsyncFiber(IAsyncExecutor executor = null, int size = QueueSize.DefaultQueueSize, TaskFactory taskFactory = null, IAsyncFiberScheduler scheduler = null)
            : base(executor, scheduler)
        {
            
            _queue = new ArrayQueue<Func<Task>>(size);
            _taskFactory = taskFactory ?? new TaskFactory(TaskCreationOptions.PreferFairness, TaskContinuationOptions.None);
            _flushCache = Flush;
        }

        public AsyncFiber(Action<Exception> errorCallback, int size = QueueSize.DefaultQueueSize, TaskFactory taskFactory = null, IAsyncFiberScheduler scheduler = null)
            : this(new AsyncExceptionHandlingExecutor(errorCallback), size, taskFactory, scheduler)
        {
        }

        protected override void InternalEnqueue(Func<Task> action)
        {
            var spinWait = default(AggressiveSpinWait);
            while (_queue.IsFull) spinWait.SpinOnce();

            var lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);

                _queue.Enqueue(action);

                if (_flushPending) return;

                _flushPending = true;
                _taskFactory.StartNew(_flushCache);
            }
            finally
            {
                if (lockTaken) _spinLock.Exit(false);
            }
        }
        
        private async Task Flush()
        {
            var (count, actions) = Drain();

            for (var i = 0; i < count; i++)
            {
                await Executor.Execute(actions[i]);
            }

            var lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);

                if (_queue.Count > 0)
                    //don't monopolize thread.
#pragma warning disable 4014
                    _taskFactory.StartNew(_flushCache);
#pragma warning restore 4014
                else
                    _flushPending = false;
            }
            finally
            {
                if (lockTaken) _spinLock.Exit(false);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (int, Func<Task>[]) Drain()
        {
            var lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);

                return _queue.Drain();
            }
            finally
            {
                if (lockTaken) _spinLock.Exit(false);
            }
        }
    }
}