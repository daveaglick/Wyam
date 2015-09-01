﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wyam.Core;
using Wyam.Common;
using Wyam.Core.Documents;

namespace Wyam.Core.Modules
{
    public class WriteFiles : IModule
    {
        private readonly Func<IDocument, IExecutionContext, string> _path;
        private Func<IDocument, IExecutionContext, bool> _where = null; 

        public WriteFiles(Func<IDocument, IExecutionContext, string> path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            _path = path;
        }

        public WriteFiles(string extension)
        {
            if (extension == null)
            {
                throw new ArgumentNullException(nameof(extension));
            }

            _path = (x, y) =>
            {
                string fileRelative = x.String(MetadataKeys.RelativeFilePath);
                if (!string.IsNullOrWhiteSpace(fileRelative))
                {
                    return Path.ChangeExtension(fileRelative, extension);
                }
                return null;
            };
        }

        public WriteFiles()
        {
            _path = (x, y) => x.String(MetadataKeys.RelativeFilePath);
        }

        public WriteFiles Where(Func<IDocument, IExecutionContext, bool> predicate)
        {
            _where = predicate;
            return this;
        }

        public IEnumerable<IDocument> Execute(IReadOnlyList<IDocument> inputs, IExecutionContext context)
        {
            return inputs.AsParallel().Select(input =>
            {
                if (_where == null || _where(input, context))
                {
                    // WritePath
                    string path = input.String(MetadataKeys.WritePath);
                    if (path != null)
                    {
                        path = PathHelper.NormalizePath(path);
                    }

                    // WriteFileName
                    if (path == null && input.ContainsKey(MetadataKeys.WriteFileName)
                        && input.ContainsKey(MetadataKeys.RelativeFileDir))
                    {
                        path = Path.Combine(input.String(MetadataKeys.RelativeFileDir),
                            PathHelper.NormalizePath(input.String(MetadataKeys.WriteFileName)));
                    }

                    // WriteExtension
                    if (path == null && input.ContainsKey(MetadataKeys.WriteExtension)
                        && input.ContainsKey(MetadataKeys.RelativeFilePath))
                    {
                        path = Path.ChangeExtension(input.String(MetadataKeys.RelativeFilePath),
                            input.String(MetadataKeys.WriteExtension));
                    }

                    // Func
                    if (path == null)
                    {
                        path = _path(input, context);
                    }

                    if (path != null)
                    {
                        path = Path.GetFullPath(Path.Combine(context.OutputFolder, path));
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            string pathDirectory = Path.GetDirectoryName(path);
                            if (!Directory.Exists(pathDirectory))
                            {
                                Directory.CreateDirectory(pathDirectory);
                            }
                            FileStream stream = File.Open(path, FileMode.Create);
                            input.Stream.CopyTo(stream);
                            context.Trace.Verbose("Wrote file {0}", path);
                            return input.Clone(stream, new Dictionary<string, object>
                            {
                                {MetadataKeys.DestinationFileBase, Path.GetFileNameWithoutExtension(path)},
                                {MetadataKeys.DestinationFileExt, Path.GetExtension(path)},
                                {MetadataKeys.DestinationFileName, Path.GetFileName(path)},
                                {MetadataKeys.DestinationFileDir, Path.GetDirectoryName(path)},
                                {MetadataKeys.DestinationFilePath, path},
                                {MetadataKeys.DestinationFilePathBase, PathHelper.RemoveExtension(path)}
                            });
                        }
                    }
                }
                return input;
            });
        }
    }
}
