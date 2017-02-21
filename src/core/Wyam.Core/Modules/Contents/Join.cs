﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wyam.Common.Documents;
using Wyam.Common.Execution;
using Wyam.Common.Modules;

namespace Wyam.Core.Modules.Contents
{
    /// <summary>
    /// Joins documents together with an optional deliminator to form one document
    /// </summary>
    /// <category>Content</category>
    public class Join : IModule
    {
        private readonly string _delimiter;

        /// <summary>
        /// Concatanates multiple documents together to form a single document without a delimiter and with the default metadata only
        /// </summary>        
        public Join() : this("")
        {
            
        }

        /// <summary>
        /// Concatanates multiple documents together to form a single document with a specified delimiter and with the default metadata only
        /// </summary>
        /// <param name="delimiter">The string to use as a seperator between documents</param>
        public Join(string delimiter)
        {
            _delimiter = delimiter;
        }


        /// <summary>
        /// Returns a single document containing the concatenated content of all input documents
        /// </summary>
        /// <returns>A single document in a list</returns>
        public IEnumerable<IDocument> Execute(IReadOnlyList<IDocument> inputs, IExecutionContext context)
        {
            StringBuilder contentBuilder = new StringBuilder();

            if (inputs == null)
            {
                return new List<IDocument>() { context.GetDocument() };
            }

            foreach(var document in inputs)
            {
                if (document == null) continue;

                contentBuilder.Append(document.Content);
                contentBuilder.Append(_delimiter);
            }

            contentBuilder.Remove(contentBuilder.Length - _delimiter.Length, _delimiter.Length);
            
            return new List<IDocument>() { context.GetDocument(contentBuilder.ToString(), new KeyValuePair<string, object>[0]) };
        }
    }

    //TODO - update comments - meta data
    //TODO - Write tests regarding the meta data options
}
