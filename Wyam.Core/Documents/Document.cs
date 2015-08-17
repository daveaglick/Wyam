﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Wyam.Common;
using Wyam.Core.Pipelines;

namespace Wyam.Core.Documents
{
    // Because it's immutable, document metadata can still be accessed after disposal
    internal class Document : IDocument, IDisposable
    {
        private readonly Pipeline _pipeline; 
        private readonly Metadata _metadata;
        private string _content;
        private Stream _stream;
        private bool _disposeStream;
        private bool _disposed;

        internal Document(Metadata metadata, Pipeline pipeline)
            : this("Initial Document", metadata, null, null, pipeline, null, true)
        {
            _metadata = metadata;
        }
        
        private Document(string source, Metadata metadata, string content, Pipeline pipeline, IEnumerable<KeyValuePair<string, object>> items)
            : this(source, metadata, null, content, pipeline, items, true)
        {
        }

        private Document(string source, Metadata metadata, Stream stream, Pipeline pipeline, IEnumerable<KeyValuePair<string, object>> items, bool disposeStream)
            : this(source, metadata, stream, null, pipeline, items, disposeStream)
        {
        }

        private Document(string source, Metadata metadata, Stream stream, string content, Pipeline pipeline, IEnumerable<KeyValuePair<string, object>> items, bool disposeStream)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException(nameof(source));
            }

            Source = source;
            _metadata = metadata.Clone(items);
            _content = content;

            _pipeline = pipeline;
            _pipeline.AddClonedDocument(this);

            if (stream != null)
            {
                if (!stream.CanRead)
                {
                    throw new ArgumentException("Document stream must support reading.", nameof(stream));
                }

                if (!stream.CanSeek)
                {
                    _stream = new SeekableStream(stream, disposeStream);
                    _disposeStream = true;
                }
                else
                {
                    _stream = stream;
                    _disposeStream = disposeStream;
                }
            }
        }

        public string Source { get; }

        public IMetadata Metadata => _metadata;

        public string Content
        {
            get
            {
                CheckDisposed();

                if (_content == null)
                {
                    if (_stream != null)
                    {
                        _stream.Position = 0;
                        using (StreamReader reader = new StreamReader(_stream, Encoding.UTF8, true, 4096, true))
                        {
                            _content = reader.ReadToEnd();
                        }
                    }
                    else
                    {
                        _content = string.Empty;
                    }
                }

                return _content;
            }
        }

        public Stream Stream
        {
            get
            {
                CheckDisposed();

                if (_stream == null)
                {
                    if (_content != null)
                    {
                        _stream = new MemoryStream(Encoding.UTF8.GetBytes(_content));
                        _disposeStream = true;
                    }
                    else
                    {
                        _stream = Stream.Null;
                    }
                }

                return _stream;
            }
        }

        public void ResetStream()
        {
            if (_stream != null)
            {
                _stream.Position = 0;
            }
        }

        public override string ToString()
        {
            if (_disposed)
            {
                return string.Empty;
            }

            // Return from the buffered string content if available
            if (_content != null)
            {
                return _content.Length < 128 ? _content : _content.Substring(0, 128);
            }

            // Otherwise, use the stream
            _stream.Position = 0;
            using (StreamReader reader = new StreamReader(_stream, Encoding.UTF8, true, 4096, true))
            {
                char[] buffer = new char[128];
                int count = reader.Read(buffer, 0, 128);
                return new string(buffer, 0, count);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_disposeStream)
            {
                _stream?.Dispose();
            }
            _disposed = true;
        }

        private void CheckDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Document));
            }
        }

        public IDocument Clone(string source, string content, IEnumerable<KeyValuePair<string, object>> items = null)
        {
            CheckDisposed();
            return new Document(source, _metadata, content, _pipeline, items);
        }

        public IDocument Clone(string content, IEnumerable<KeyValuePair<string, object>> items = null)
        {
            CheckDisposed();
            return new Document(Source, _metadata, content, _pipeline, items);
        }

        public IDocument Clone(string source, Stream stream, IEnumerable<KeyValuePair<string, object>> items = null, bool disposeStream = true)
        {
            CheckDisposed();
            return new Document(source, _metadata, stream, _pipeline, items, disposeStream);
        }

        public IDocument Clone(Stream stream, IEnumerable<KeyValuePair<string, object>> items = null, bool disposeStream = true)
        {
            CheckDisposed();
            return new Document(Source, _metadata, stream, _pipeline, items, disposeStream);
        }

        public IDocument Clone(IEnumerable<KeyValuePair<string, object>> items = null)
        {
            CheckDisposed();

            // Don't dispose the stream since the cloned document might be final and get passed to another pipeline, it'll take care of final disposal
            _disposeStream = false;
            return new Document(Source, _metadata, _stream, _content, _pipeline, items, _disposeStream);
        }

        // IMetadata

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return _metadata.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool ContainsKey(string key)
        {
            return _metadata.ContainsKey(key);
        }

        public bool TryGetValue(string key, out object value)
        {
            return _metadata.TryGetValue(key, out value);
        }

        public object this[string key] => _metadata[key];

        public IEnumerable<string> Keys => _metadata.Keys;

        public IEnumerable<object> Values => _metadata.Values;

        public IMetadata<T> MetadataAs<T>()
        {
            return _metadata.MetadataAs<T>();
        }

        public object Get(string key, object defaultValue)
        {
            return _metadata.Get(key, defaultValue);
        }

        public T Get<T>(string key)
        {
            return _metadata.Get<T>(key);
        }

        public T Get<T>(string key, T defaultValue)
        {
            return _metadata.Get<T>(key, defaultValue);
        }

        public string String(string key, string defaultValue = null)
        {
            return _metadata.String(key, defaultValue);
        }

        public string Link(string key, string defaultValue = null)
        {
            return _metadata.Link(key, defaultValue);
        }

        public int Count => _metadata.Count;
    }
}
