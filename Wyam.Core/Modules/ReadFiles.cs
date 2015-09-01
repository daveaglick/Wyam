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
    public class ReadFiles : IModule
    {
        private readonly Func<IDocument, IExecutionContext, string> _path;
        private SearchOption _searchOption = System.IO.SearchOption.AllDirectories;
        private Func<string, bool> _where = null; 

        public ReadFiles(Func<IDocument, IExecutionContext, string> path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            _path = path;
        }

        public ReadFiles(string searchPattern)
        {
            if (searchPattern == null)
            {
                throw new ArgumentNullException(nameof(searchPattern));
            }

            _path = (x, y) => searchPattern;
        }

        public ReadFiles SearchOption(SearchOption searchOption)
        {
            _searchOption = searchOption;
            return this;
        }

        public ReadFiles AllDirectories()
        {
            _searchOption = System.IO.SearchOption.AllDirectories;
            return this;
        }

        public ReadFiles TopDirectoryOnly()
        {
            _searchOption = System.IO.SearchOption.TopDirectoryOnly;
            return this;
        }

        public ReadFiles Where(Func<string, bool> predicate)
        {
            _where = predicate;
            return this;
        }

        public IEnumerable<IDocument> Execute(IReadOnlyList<IDocument> inputs, IExecutionContext context)
        {
            return inputs.AsParallel().SelectMany(input =>
            {
                string path = _path(input, context);
                if (path != null)
                {
                    path = Path.Combine(context.InputFolder, PathHelper.NormalizePath(path));
                    path = Path.Combine(Path.GetFullPath(Path.GetDirectoryName(path)), Path.GetFileName(path));
                    string fileRoot = Path.GetDirectoryName(path);
                    if (fileRoot != null && Directory.Exists(fileRoot))
                    {
                        return Directory.EnumerateFiles(fileRoot, Path.GetFileName(path), _searchOption)
                            .AsParallel()
                            .Where(x => _where == null || _where(x))
                            .Select(file =>
                            {
                                context.Trace.Verbose("Read file {0}", file);
                                return input.Clone(file, File.OpenRead(file), new Dictionary<string, object>
                                {
                                    {MetadataKeys.SourceFileRoot, fileRoot},
                                    {MetadataKeys.SourceFileBase, Path.GetFileNameWithoutExtension(file)},
                                    {MetadataKeys.SourceFileExt, Path.GetExtension(file)},
                                    {MetadataKeys.SourceFileName, Path.GetFileName(file)},
                                    {MetadataKeys.SourceFileDir, Path.GetDirectoryName(file)},
                                    {MetadataKeys.SourceFilePath, file},
                                    {MetadataKeys.SourceFilePathBase, PathHelper.RemoveExtension(file)},
                                    {MetadataKeys.RelativeFilePath, PathHelper.GetRelativePath(context.InputFolder, file)},
                                    {MetadataKeys.RelativeFilePathBase, PathHelper.RemoveExtension(PathHelper.GetRelativePath(context.InputFolder, file))},
                                    {MetadataKeys.RelativeFileDir, Path.GetDirectoryName(PathHelper.GetRelativePath(context.InputFolder, file))}
                                });
                            });
                    }
                }
                return null;
            });
        }
    }
}
