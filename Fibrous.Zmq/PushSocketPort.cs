namespace Fibrous.Zmq
{
    using System;
    using ZeroMQ;

    public sealed class PushSocketPort<T> : SendSocketBase<T>
    {
        public PushSocketPort(IZmqContext context, string address, Action<T, ISendSocket> msgSender, bool bind = false)
            : base(msgSender)
        {
            Socket = context.CreatePushSocket();
            if (bind)
            {
                Socket.Bind(address);
            }
            else
            {
                Socket.Connect(address);
            }
        }
    }
}