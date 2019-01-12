﻿using System;
using System.IO;
using System.Runtime.CompilerServices;
using Ace.Networking.Memory;
using Ace.Networking.MicroProtocol.Enums;
using Ace.Networking.MicroProtocol.Headers;
using Ace.Networking.MicroProtocol.Interfaces;
using Ace.Networking.MicroProtocol.PacketTypes;
using Ace.Networking.MicroProtocol.Structures;

namespace Ace.Networking.MicroProtocol
{
    public class MicroDecoder : IPayloadDecoder
    {
        /// <summary>
        ///     Protocol version
        /// </summary>
        public const byte Version = 2;

        private RecyclableMemoryStream _contentStream;// = new MemoryStream();
        private readonly byte[] _header = new byte[short.MaxValue];

        private int _bytesLeftForCurrentState;
        private int _bytesLeftInSocketBuffer;
        private BasicHeader _headerObject;
        private int _headerOffset;
        private ushort _headerSize;

        private Action<BasicHeader, object, Type> _messageReceived;
        private byte _protocolVersion;
        private RawDataHeader.RawDataHandler _rawDataReceived;
        private int _socketBufferOffset;
        private Func<SocketBuffer, bool> _stateMethod;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MicroDecoder" /> class.
        /// </summary>
        /// <param name="serializer">The serializer used to decode the payload</param>
        /// <exception cref="System.ArgumentNullException">serializer</exception>
        public MicroDecoder(IPayloadSerializer serializer)
        {
            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _bytesLeftForCurrentState = sizeof(short);
            _stateMethod = ReadHeaderLength;
            _contentStream = MemoryManager.Instance.GetStream();
        }

        public IPayloadSerializer Serializer { get; }

        /// <summary>
        ///     Reset the decoder so that we can parse a new message
        /// </summary>
        public void Clear()
        {
            _bytesLeftForCurrentState = sizeof(short);
            _bytesLeftInSocketBuffer = 0;
            _contentStream?.Dispose();
            _headerOffset = 0;
            _socketBufferOffset = 0;
            _stateMethod = ReadHeaderLength;
        }

        /// <summary>
        ///     A new message have been received.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The message will be a deserialized message or a <c>Stream</c> derived object (if the sender sent a
        ///         <c>Stream</c> or a <c>byte[]</c> array).
        ///     </para>
        /// </remarks>
        public Action<BasicHeader, object, Type> PacketReceived
        {
            get => _messageReceived;
            set
            {
                if (value == null) value = (h, o, t) => { };

                _messageReceived = value;
            }
        }

        public RawDataHeader.RawDataHandler RawDataReceived
        {
            get => _rawDataReceived;
            set
            {
                if (value == null) value = (i, s, stream) => null;
                _rawDataReceived = value;
            }
        }

        /// <summary>
        ///     Process bytes that we've received on the socket.
        /// </summary>
        /// <param name="buffer">Buffer to process.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProcessReadBytes(SocketBuffer buffer)
        {
            _bytesLeftInSocketBuffer = buffer.BytesTransferred;
            _socketBufferOffset = buffer.Offset;


            while (_stateMethod(buffer))
            {
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IPayloadDecoder Clone()
        {
            return new MicroDecoder(Serializer.Clone());
        }

        private bool ReadHeaderLength(SocketBuffer e)
        {
            if (!CopyBytes(e)) return false;

            _headerSize = BitConverter.ToUInt16(_header, 0);
            _bytesLeftForCurrentState = _headerSize - sizeof(ushort);
            _stateMethod = ProcessFixedHeader;
            _headerOffset = 0;
            return true;
        }

        private bool CopyBytes(SocketBuffer e)
        {
            if (_bytesLeftInSocketBuffer == 0) return false;

            if (_bytesLeftForCurrentState > 0)
            {
                var toCopy = Math.Min(_bytesLeftForCurrentState, _bytesLeftInSocketBuffer);
                Buffer.BlockCopy(e.Buffer, _socketBufferOffset, _header, _headerOffset, toCopy);
                _headerOffset += toCopy;
                _bytesLeftForCurrentState -= toCopy;
                _bytesLeftInSocketBuffer -= toCopy;
                _socketBufferOffset += toCopy;
            }

            return _bytesLeftForCurrentState == 0;
        }

        private bool ProcessFixedHeader(SocketBuffer e)
        {
            if (!CopyBytes(e)) return false;

            _protocolVersion = _header[0];

            _headerObject = BasicHeader.Upgrade(_header, 1);

            _stateMethod = ProcessContent;
            _bytesLeftForCurrentState = (_headerObject as ContentHeader)?.ContentLength ?? -1;
            if (_headerObject.PacketType == PacketType.RawData)
                _bytesLeftForCurrentState = (_headerObject as RawDataHeader)?.ContentLength ?? -1;



            if (_headerObject.PacketFlag.HasFlag(PacketFlag.NoContent) || _bytesLeftForCurrentState == 0)
            {
                _bytesLeftForCurrentState = -1;
                _contentStream.SetLength(0);
                //_contentStream = null;
            }
            else
            {
                //_contentStream = MemoryManager.Instance.GetStream(_bytesLeftForCurrentState);
                _contentStream.ReserveSingleBlock(_bytesLeftForCurrentState);
            }
            _headerOffset = 0;
            return true;
        }

        private bool ProcessContent(SocketBuffer arg)
        {
            if (_bytesLeftForCurrentState == -1) goto SKIP_CHECKS;
            if (_bytesLeftForCurrentState == 0 || _bytesLeftInSocketBuffer == 0) return false;

            var bytesToCopy = Math.Min(_bytesLeftForCurrentState, _bytesLeftInSocketBuffer);
            _contentStream.Write(arg.Buffer, _socketBufferOffset, bytesToCopy);
            _bytesLeftInSocketBuffer -= bytesToCopy;
            _bytesLeftForCurrentState -= bytesToCopy;
            _socketBufferOffset += bytesToCopy;

            if (_bytesLeftForCurrentState > 0) return false;
            SKIP_CHECKS:

            _bytesLeftForCurrentState = sizeof(ushort);
            _headerOffset = 0;
            _stateMethod = ReadHeaderLength;
            _contentStream?.Seek(0, SeekOrigin.Begin);

            bool isProcessed = false;
            if (_headerObject.PacketType == PacketType.RawData)
            {
                if (!(_headerObject is RawDataHeader rawData)) return false;
                RawDataReceived(rawData.RawDataBufferId, rawData.RawDataSeq, _contentStream);
                isProcessed = true;
            }


            if (!isProcessed && _headerObject is ContentHeader content)
            {
                var contentType = content.ContentType;
                var packet = new DefaultContentPacket(content, null);
                if (_contentStream == null)
                {
                    packet.Payload = null;
                    packet.Type = typeof(object);
                }
                else
                {
                    var message = Serializer.Deserialize(contentType, _contentStream, out var resolvedType);
                    packet.Payload = message;
                    packet.Type = resolvedType;
                }
                PacketReceived(packet.Header, packet.Payload, packet.Type);
                isProcessed = true;
            }

            if (isProcessed)
            {
                //_contentStream?.Dispose();
                _contentStream?.SetLength(0);
                return true;
            }

            return false;
        }
    }
}