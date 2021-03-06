﻿using System;
using System.Runtime.CompilerServices;
using Ace.Networking.MicroProtocol.Interfaces;
using Ace.Networking.Threading;

namespace Ace.Networking.Handlers
{
    public class GenericPayloadHandlerWrapper<T> : IPayloadHandlerWrapper
    {
        public GenericPayloadHandlerWrapper(GenericPayloadHandler<T> handler)
        {
            Handler = handler;
        }

        public GenericPayloadHandler<T> Handler { get; set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object Invoke(IConnection connection, object obj, Type type)
        {
            return Handler.Invoke(connection, (T) obj);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HandlerEquals(object obj)
        {
            return Handler.Equals(obj);
        }
    }
}