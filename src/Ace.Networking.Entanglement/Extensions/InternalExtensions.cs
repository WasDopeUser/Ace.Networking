﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ace.Networking.Extensions;
using Ace.Networking.Memory;
using Ace.Networking.MicroProtocol.Interfaces;
using Ace.Networking.Serializers;

namespace Ace.Networking.Entanglement.Extensions
{
    public static class InternalExtensions
    {
        public static T Deserialize<T>(this byte[] data, IPayloadSerializer serializer)
        {
            using (var ms = new MemoryStream(data))
            {
                return (T)serializer.Deserialize(serializer.SupportedContentType, ms, out var type);
            }
        }

        public static byte[] Serialize(object obj, IPayloadSerializer serializer)
        {
            using (var ms = MemoryManager.Instance.GetStream())
            {
                serializer.Serialize(obj, ms, out _);
                return ms.ToArray();
            }
        }

        public static byte[] SerializeContent(object obj, IPayloadSerializer serializer)
        {
            using (var ms = MemoryManager.Instance.GetStream())
            {
                serializer.SerializeContent(obj, ms);
                return ms.ToArray();
            }
        }
    }
}
