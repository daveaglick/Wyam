﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wyam.Common;

namespace Wyam.Core.Modules
{
    // Prepends the specified content to the existing content
    public class Prepend : ContentModule
    {
        public Prepend(object content)
            : base(content)
        {
        }

        public Prepend(Func<IExecutionContext, object> content)
            : base(content)
        {
        }

        public Prepend(Func<IDocument, IExecutionContext, object> content) 
            : base(content)
        {
        }

        public Prepend(params IModule[] modules)
            : base(modules)
        {
        }

        protected override IEnumerable<IDocument> Execute(object content, IDocument input, IExecutionContext context)
        {
            return new[] { content == null ? input : input.Clone(content + input.Content) };
        }
    }
}
