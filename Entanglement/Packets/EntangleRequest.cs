﻿using System;
using MessagePack;

namespace Ace.Networking.Entanglement.Packets
{
    [MessagePackObject]
    public class EntangleRequest
    {
        // Optional
        [Key(0)] public Guid? Eid { get; set; }

        [Key(1)] public Guid InterfaceId { get; set; }
    }
}