﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Text;
using Ace.Networking.MicroProtocol.Interfaces;
using ProtoBuf;

namespace Ace.Networking.Serializers.TypeResolvers
{
    public abstract class TypeResolverBase<TKey> : ITypeResolver
    {
        protected readonly ConcurrentDictionary<TKey, Type> Types;
        protected readonly ConcurrentDictionary<Type, TKey> TypesLookup;


        public abstract byte Signature { get; }

        public virtual void RegisterAssembly(Assembly assembly, params Type[] attributes)
        {
            foreach (var type in assembly.GetTypes().Where(t => attributes.Any(a => t.GetCustomAttribute(a) != null)))
                RegisterType(type);
        }

        public virtual void RegisterAssembly(Assembly assembly)
        {
            RegisterAssembly(assembly, typeof(ProtoContractAttribute));
        }

        public abstract bool TryResolve(Stream stream, out Type type);
        public abstract bool TryWrite(Stream stream, Type type);

        public virtual void RegisterType(Type type)
        {
            var guid = GetRepresentation(type);
            if ((Types.TryGetValue(guid, out var t1) && t1 != type) ||
                (TypesLookup.TryGetValue(type, out var t2) && !EqualityComparer<TKey>.Default.Equals(t2, guid)))
                throw new ArgumentException(nameof(type));
            Types[guid] = type;
            TypesLookup[type] = guid;
        }

        public abstract TKey GetRepresentation(Type type);

        public TypeResolverBase()
        {
            Types = Types ?? new ConcurrentDictionary<TKey, Type>();
            TypesLookup = TypesLookup ?? new ConcurrentDictionary<Type, TKey>();
        }
    }
}
