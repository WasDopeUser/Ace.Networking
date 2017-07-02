﻿using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Ace.Networking.Interfaces;

namespace Ace.Networking.Structures
{
    /// <summary>
    ///     Uses a concurrent dictionary to store all items.
    /// </summary>
    public class ConnectionData : IConnectionData
    {
        private readonly ConcurrentDictionary<string, object> _items = new ConcurrentDictionary<string, object>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get<T>(string key, T fallback)
        {
            return _items.TryGetValue(key, out var obj) ? (T) obj : fallback;
        }

        /// <summary>
        ///     Get or add a value
        /// </summary>
        /// <param name="key">key to get</param>
        /// <param name="addCallback">Should return value to add if the key is not found</param>
        /// <returns>Item that was added or found</returns>
        public object GetOrAdd(string key, Func<string, object> addCallback)
        {
            return _items.GetOrAdd(key, addCallback);
        }

        /// <summary>
        ///     Try updating a value
        /// </summary>
        /// <param name="key">Key for the value to update</param>
        /// <param name="newValue">Value to set</param>
        /// <param name="existingValue">Value that we've previously retrieved</param>
        /// <returns><c>true</c> if the existing value is the same as the one in the dictionary</returns>
        public bool TryUpdate(string key, object newValue, object existingValue)
        {
            return _items.TryUpdate(key, newValue, existingValue);
        }

        /// <summary>
        ///     Get or set data
        /// </summary>
        /// <param name="key">Identifier (note that everyone with access to the channel can access the data, use careful naming)</param>
        /// <returns>Data if found; otherwise <c>null</c>.</returns>
        public object this[string key]
        {
            get
            {
                object item;
                return _items.TryGetValue(key, out item) ? item : null;
            }
            set => _items[key] = value;
        }

        /// <summary>
        ///     Remove all existing data.
        /// </summary>
        public void Clear()
        {
            _items.Clear();
        }
    }
}