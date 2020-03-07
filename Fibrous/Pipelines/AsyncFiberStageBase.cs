﻿using System;
using System.Threading.Tasks;

namespace Fibrous.Pipelines
{
    public abstract class AsyncFiberStageBase<TIn, TOut> : StageBase<TIn, TOut>, IHaveAsyncFiber
    {
        protected AsyncFiberStageBase(Action<Exception> errorCallback = null)
        {
            IAsyncExecutor executor = errorCallback == null
                ? (IAsyncExecutor)new AsyncExecutor()
                : new AsyncExceptionHandlingExecutor(errorCallback);
            Fiber = new AsyncFiber(executor);
            Fiber.Subscribe(In, Receive);
        }

        public IAsyncFiber Fiber { get; }

        public override void Dispose()
        {
            Fiber?.Dispose();
        }

        protected abstract Task Receive(TIn @in);
    }
}