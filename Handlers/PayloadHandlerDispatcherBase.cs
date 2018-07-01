﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ace.Networking.Interfaces;
using Ace.Networking.MicroProtocol.Interfaces;

namespace Ace.Networking.Handlers
{
    public abstract class PayloadHandlerDispatcherBase
    {
        public delegate object GenericPayloadHandler<in T>(IConnection connection, T payload);

        public delegate void GlobalPayloadHandler(IConnection connection, object payload, Type type);

        public delegate object PayloadHandler(IConnection connection, object payload, Type type);

        public delegate bool RequestHandler(RequestWrapper request);

        protected ConcurrentDictionary<Type, LinkedList<RequestHandler>> RequestHandlers =
            new ConcurrentDictionary<Type, LinkedList<RequestHandler>>();

        protected ConcurrentDictionary<Type, LinkedList<IPayloadHandlerWrapper>> TypeHandlers =
            new ConcurrentDictionary<Type, LinkedList<IPayloadHandlerWrapper>>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void AppendTypeHandler(Type type, IPayloadHandlerWrapper handler)
        {
            TypeHandlers.TryAddLast(type, handler);
        }

        protected bool RemoveTypeHandler(Type type, object obj)
        {
            if (!TypeHandlers.ContainsKey(type)) return false;
            bool ret;
            lock (TypeHandlers[type])
            {
                ret = TypeHandlers[type].RemoveFirst(t => t.HandlerEquals(obj));
            }

            return ret;
        }

        protected bool RemoveAllTypeHandlers(Type type)
        {
            RequestHandlers.TryRemove(type, out _);
            if (TypeHandlers.TryGetValue(type, out var list))
            {
                lock (list)
                {
                    list.Clear();
                }

                return true;
            }

            return false;
        }

        public void RemoveAllTypeHandlers()
        {
            TypeHandlers.Clear();
            RequestHandlers.Clear();
        }
    }
}