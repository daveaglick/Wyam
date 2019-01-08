﻿using System;
using System.IO;
using Wyam.Common.IO;

namespace Wyam.Core.IO.FileProviders.Local
{
    // Initially based on code from Cake (http://cakebuild.net/)
    internal class LocalFile : IFile
    {
        private readonly FileInfo _file;
        private readonly FilePath _path;

        public FilePath Path => _path;

        NormalizedPath IFileSystemEntry.Path => _path;

        public IDirectory Directory => new LocalDirectory(_path.Directory);

        public bool Exists => _file.Exists;

        public long Length => _file.Length;

        public LocalFile(FilePath path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }
            if (path.IsRelative)
            {
                throw new ArgumentException("Path must be absolute", nameof(path));
            }

            _path = path.Collapse();
            _file = new FileInfo(_path.FullPath);
        }

        public void CopyTo(IFile destination, bool overwrite = true, bool createDirectory = true)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            // Create the directory
            if (createDirectory)
            {
                destination.Directory.Create();
            }

            // Use the file system APIs if destination is also in the file system
            if (destination is LocalFile)
            {
                RetryHelper.Retry(() => _file.CopyTo(destination.Path.FullPath, overwrite));
            }
            else
            {
                // Otherwise use streams to perform the copy
                using (Stream sourceStream = OpenRead())
                {
                    using (Stream destinationStream = destination.OpenWrite())
                    {
                        sourceStream.CopyTo(destinationStream);
                    }
                }
            }
        }

        public void MoveTo(IFile destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            // Use the file system APIs if destination is also in the file system
            if (destination is LocalFile)
            {
                RetryHelper.Retry(() => _file.MoveTo(destination.Path.FullPath));
            }
            else
            {
                // Otherwise use streams to perform the move
                using (Stream sourceStream = OpenRead())
                {
                    using (Stream destinationStream = destination.OpenWrite())
                    {
                        sourceStream.CopyTo(destinationStream);
                    }
                }
                Delete();
            }
        }

        public void Delete() => RetryHelper.Retry(() => _file.Delete());

        public string ReadAllText() =>
            RetryHelper.Retry(() => System.IO.File.ReadAllText(_file.FullName));

        public void WriteAllText(string contents, bool createDirectory = true)
        {
            if (createDirectory)
            {
                Directory.Create();
            }
            RetryHelper.Retry(() => System.IO.File.WriteAllText(_file.FullName, contents));
        }

        public Stream OpenRead() =>
            RetryHelper.Retry(() => _file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

        public Stream OpenWrite(bool createDirectory = true)
        {
            if (createDirectory)
            {
                Directory.Create();
            }
            return RetryHelper.Retry(() => _file.Open(FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite));
        }

        public Stream OpenAppend(bool createDirectory = true)
        {
            if (createDirectory)
            {
                Directory.Create();
            }
            return RetryHelper.Retry(() => _file.Open(FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
        }

        public Stream Open(bool createDirectory = true)
        {
            if (createDirectory)
            {
                Directory.Create();
            }
            return RetryHelper.Retry(() => _file.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite));
        }
    }
}
