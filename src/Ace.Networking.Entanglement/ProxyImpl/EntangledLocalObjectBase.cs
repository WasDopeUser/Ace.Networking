﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ace.Networking.Entanglement.Extensions;
using Ace.Networking.Entanglement.Packets;
using Ace.Networking.Entanglement.Reflection;
using Ace.Networking.Entanglement.Structures;
using Ace.Networking.Extensions;
using Ace.Networking.Threading;
using Ace.Networking.Memory;
using System.Threading;

namespace Ace.Networking.Entanglement.ProxyImpl
{
    public abstract class EntangledLocalObjectBase : EntangledObjectBase
    {
        protected object _sync = new object();

        public TimeSpan? DefaultExecutionTimeout { get; set; }

        public EntangledLocalObjectBase(IConnection host, Guid eid, InterfaceDescriptor desc)
        {
            Host = host;
            _Eid = eid;
            _Descriptor = desc;
            foreach (var evkv in _Descriptor.Events)
            {
                var ev = evkv.Value;
                if (ev.InvokerDelegate == null) _Descriptor.AddEventInvokerDelegate(ev);
            }
        }

        public IConnection Host { get; internal set; }

        public ExecuteMethod GetExecuteMethodDescriptor(string name, Type returnType, params object[] arg)
        {
            /*byte[][] arguments = null;
            if ((arg?.Length??0) > 0)
            {
                arguments = new byte[arg.Length][];
                using (var mm = MemoryManager.Instance.GetStream())
                {
                    int i = 0;
                    foreach (var a in arg)
                    {
                        mm.SetLength(0);
                        Host.Serializer.Serialize(a, mm, out _);
                        arguments[i++] = mm.ToArray();
                    }
                }

            }*/
            var exe = new ExecuteMethod
            {
                Eid = _Eid,
                //Arguments = arguments,
                Method = name,
                //_ReturnType = returnType
                Objects = arg
            };
            return exe;
        }

        public async Task<T> ExecuteMethod<T>(int overloadId, string name, params object[] arg)
        {
            var descriptor = _Descriptor.Methods[name][overloadId];
            var desc = GetExecuteMethodDescriptor(name, typeof(T), arg);
            CancellationToken? token = null;

            if (descriptor.HasCancellationToken)
            {
                if (arg.Last() is CancellationToken ct)
                    token = ct;
            }

            if (token == null && DefaultExecutionTimeout.HasValue)
            {
                token = DefaultExecutionTimeout.Value.GetCancellationToken();
            }

            if (arg != null)
            {
                for (int i = 0; i < arg.Length; i++)
                {
                    if (descriptor.Parameters[i].IsDummy)
                        desc.Objects[i] = null;
                }
            }

            var res = await Host.SendRequest<ExecuteMethod, ExecuteMethodResult>(desc, token).ConfigureAwait(false);
            if (res.ExceptionAdapter != null)
                throw new RemoteException(res.ExceptionAdapter);
            if (res.Data == null) return default;
            return (T)res.Data;
        }

        public T ExecuteMethodSync<T>(int overloadId, string name, params object[] arg)
        {
            return ExecuteMethod<T>(overloadId, name, arg).GetAwaiter().GetResult();
        }

        public void ExecuteMethodVoidSync(int overloadId, string name, params object[] arg)
        {
            ExecuteMethodVoid(overloadId, name, arg).GetAwaiter().GetResult();
        }

        public async Task ExecuteMethodVoid(int overloadId, string name, params object[] arg)
        {
            var descriptor = _Descriptor.Methods[name][overloadId];
            var desc = GetExecuteMethodDescriptor(name, typeof(void), arg);
            CancellationToken? token = null;

            if (descriptor.HasCancellationToken)
            {
                if (arg.Last() is CancellationToken ct)
                    token = ct;
            }

            if(token == null && DefaultExecutionTimeout.HasValue)
            {
                token = DefaultExecutionTimeout.Value.GetCancellationToken();
            }

            if (arg != null)
            {
                for (int i = 0; i < arg.Length; i++)
                {
                    if (descriptor.Parameters[i].IsDummy)
                        desc.Objects[i] = null;
                }
            }

            var res = await Host.SendRequest<ExecuteMethod, ExecuteMethodResult>(desc, token).ConfigureAwait(false);
            if (res.ExceptionAdapter != null)
                throw new RemoteException(res.ExceptionAdapter);
        }

        public void UpdateProperties(IConnection host, UpdateProperties updates)
        {
            if ((updates?.Updates?.Count ?? 0) == 0) return;

            lock (_sync)
            {
                foreach (var update in updates.Updates)
                    if (_Descriptor.Properties.TryGetValue(update.PropertyName, out var prop))
                        using (var ms = new MemoryStream(update.SerializedData))
                        {
                            prop.BackingField.SetValue(this,
                                host.Serializer.Deserialize(Host.Serializer.SupportedContentType, ms, out _));
                        }
            }

            foreach (var update in updates.Updates) OnPropertyChanged(update.PropertyName);
        }


        public void RaiseEvent(IConnection host, RaiseEvent data)
        {
            if (_Descriptor.Events.TryGetValue(data.Event, out var ev))
            {
                for (int i = 0; i < (data?.Objects?.Length); i++)
                {
                    if (data.Types[i] == typeof(SelfPlaceholder))
                        data.Objects[i] = this;
                }
                //todo optimize property lookup
                ev.InvokerDelegate.Invoke(this, data.Objects);
            }
        }
    }
}