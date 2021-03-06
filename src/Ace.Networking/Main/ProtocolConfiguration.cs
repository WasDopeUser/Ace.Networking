﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Ace.Networking.MicroProtocol;
using Ace.Networking.MicroProtocol.SSL;
using Ace.Networking.Serializers;
using Ace.Networking.Threading;
using Ace.Networking.Threading.Workers;
using Ace.Networking.TypeResolvers;

namespace Ace.Networking
{
    public class ProtocolConfiguration
    {



        public static Lazy<ProtocolConfiguration> Instance =
            new Lazy<ProtocolConfiguration>(() => new ProtocolConfiguration());


        protected volatile bool IsInitialized;

        public ProtocolConfiguration(IPayloadEncoder encoder, IPayloadDecoder decoder,
            ThreadedQueueProcessor<SendMessageQueueItem> customOutQueue = null,
            ThreadedQueueProcessor<ReceiveMessageQueueItem> customInQueue = null, bool readAsync = false)
        {
            PayloadEncoder = encoder;
            PayloadDecoder = decoder;
            Serializer = encoder.Serializer;
            CustomOutcomingMessageQueue = customOutQueue;
            CustomIncomingMessageQueue = customInQueue;
            ReadAsync = readAsync;
            Initialize();
        }

        public ProtocolConfiguration()
        {
            Serializer = new ProtobufSerializer(NetworkingSettings.DefaultTypeResolver);

            PayloadEncoder = new MicroEncoder(Serializer.Clone());
            PayloadDecoder = new MicroDecoder(Serializer.Clone());
            CustomIncomingMessageQueue = GlobalIncomingMessageQueue.Instance;
            CustomOutcomingMessageQueue = GlobalOutcomingMessageQueue.Instance;
            Initialize();
        }

        public IPayloadSerializer Serializer { get; protected set; }
        public ITypeResolver TypeResolver => Serializer?.TypeResolver;
        public IPayloadEncoder PayloadEncoder { get; protected set; }
        public IPayloadDecoder PayloadDecoder { get; protected set; }

        public SslMode SslMode { get; set; }

        public ThreadedQueueProcessor<SendMessageQueueItem> CustomOutcomingMessageQueue { get; protected set; }
        public ThreadedQueueProcessor<ReceiveMessageQueueItem> CustomIncomingMessageQueue { get; protected set; }

        public bool ReadAsync { get; protected set; }

        protected virtual void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;

            int i = 0;
            foreach (var primitive in NetworkingSettings.Primitives)
            {
                TypeResolver.RegisterTypeBy(primitive, NetworkingSettings.GetPrimitiveGuid(i++));
            }
            TypeResolver.RegisterAssembly(GetType().GetTypeInfo().Assembly);
            TypeResolver.RegisterAssembly(typeof(Connection).GetTypeInfo().Assembly);
            foreach (var assembly in NetworkingSettings.PacketAssemblies)
                TypeResolver.RegisterAssembly(assembly);


        }

        public virtual ClientSslStreamFactory GetClientSslFactory(string targetCommonName = "",
            X509Certificate2 certificate = null, SslProtocols protocols = SslProtocols.Tls12)
        {
            return new ClientSslStreamFactory(targetCommonName, certificate, protocols);
        }

        public virtual ServerSslStreamFactory GetServerSslFactory(X509Certificate2 certificate = null,
            bool useClient = true, SslProtocols protocols = SslProtocols.Tls12)
        {
            return new ServerSslStreamFactory(certificate, useClient, protocols);
        }
    }
}