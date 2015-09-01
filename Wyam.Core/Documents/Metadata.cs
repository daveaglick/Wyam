﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Wyam.Common;

namespace Wyam.Core.Documents
{
    // This class contains a stack of all the metadata generated at a particular pipeline stage
    // Getting a value checks each of the stacks and returns the first hit
    // This class is immutable, use IDocument.Clone() to get a new one with additional values
    internal class Metadata : IMetadata
    {
        private readonly Engine _engine;
        private readonly Stack<IDictionary<string, object>> _metadataStack;
        
        internal Metadata(Engine engine)
        {
            _engine = engine;
            _metadataStack = new Stack<IDictionary<string, object>>();
            Dictionary<string, object> dictionary = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> item in engine.Metadata)
            {
                dictionary[item.Key] = item.Value;
            }
            _metadataStack.Push(dictionary);
        }

        private Metadata(Metadata original, IEnumerable<KeyValuePair<string, object>> metadata)
        {
            _engine = original._engine;
            _metadataStack = new Stack<IDictionary<string, object>>(original._metadataStack.Reverse());
            _metadataStack.Push(new Dictionary<string, object>());

            // Set new items
            if (metadata != null)
            {
                foreach (KeyValuePair<string, object> item in metadata)
                {
                    _metadataStack.Peek()[item.Key] = item.Value;
                }
            }
        }

        public IMetadata<T> MetadataAs<T>()
        {
            return new MetadataAs<T>(this);
        }

        // This clones the stack and pushes a new dictionary on to the cloned stack
        internal Metadata Clone(IEnumerable<KeyValuePair<string, object>> metadata)
        {
            return new Metadata(this, metadata);
        }

        public bool ContainsKey(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            return _metadataStack.FirstOrDefault(x => x.ContainsKey(key)) != null;
        }

        public bool TryGetValue(string key, out object value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            value = null;
            IDictionary<string, object> meta = _metadataStack.FirstOrDefault(x => x.ContainsKey(key));
            if (meta == null)
            {
                return false;
            }
            value = meta[key];
            return true;
        }

        public object Get(string key, object defaultValue = null)
        {
            object value;
            return TryGetValue(key, out value) ? value : defaultValue;
        }

        public T Get<T>(string key)
        {
            return MetadataAs<T>().Get(key);
        }

        public T Get<T>(string key, T defaultValue)
        {
            return MetadataAs<T>().Get(key, defaultValue);
        }

        public string String(string key, string defaultValue = null)
        {
            return Get<string>(key, defaultValue);
        }

        public string Link(string key, string defaultValue = null)
        {
            string value = Get<string>(key, defaultValue);
            return value?.Replace('\\', '/');
        }

        public object this[string key]
        {
            get
            {
                if (key == null)
                {
                    throw new ArgumentNullException(nameof(key));
                }
                object value;
                if (!TryGetValue(key, out value))
                {
                    throw new KeyNotFoundException();
                }
                return value;
            }
        }

        public IEnumerable<string> Keys
        {
            get { return _metadataStack.SelectMany(x => x.Keys); }
        }

        public IEnumerable<object> Values
        {
            get { return _metadataStack.SelectMany(x => x.Values); }
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return _metadataStack.SelectMany(x => x).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count
        {
            get { return _metadataStack.Sum(x => x.Count); }
        }
    }
}
