﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wyam.Common;

namespace Wyam.Core.Modules
{
    // Outputs the supplied content to the trace without manipulating the inputs
    // This is useful for debugging or reporting custom status during build
    public class Trace : ContentModule
    {
        private TraceEventType _traceEventType = TraceEventType.Information;

        public Trace(object content)
            : base(content)
        {
        }

        public Trace(Func<IExecutionContext, object> content)
            : base(content)
        {
        }

        public Trace(Func<IDocument, IExecutionContext, object> content) 
            : base(content)
        {
        }

        public Trace(params IModule[] modules)
            : base(modules)
        {
        }

        public Trace EventType(TraceEventType traceEventType)
        {
            _traceEventType = traceEventType;
            return this;
        }

        protected override IEnumerable<IDocument> Execute(object content, IDocument input, IExecutionContext context)
        {
            context.Trace.TraceEvent(_traceEventType, content.ToString());
            return new [] { input };
        }
    }
}
