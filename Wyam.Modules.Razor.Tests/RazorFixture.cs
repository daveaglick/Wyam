﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using Wyam.Common;
using Wyam.Core;
using Wyam.Core.Modules;

namespace Wyam.Modules.Razor.Tests
{
    [TestFixture]
    public class RazorFixture
    {
        [Test]
        public void SimpleTemplate()
        {
            // Given
            string inputFolder = Path.Combine(Environment.CurrentDirectory, @".\Input");
            if (!Directory.Exists(inputFolder))
            {
                Directory.CreateDirectory(inputFolder);
            }
            IExecutionContext context = Substitute.For<IExecutionContext>();
            context.RootFolder.Returns(Environment.CurrentDirectory);
            context.InputFolder.Returns(inputFolder);
            Engine engine = new Engine();
            engine.Configure();
            context.Assemblies.Returns(engine.Assemblies);
            IDocument document = Substitute.For<IDocument>();
            List<string> items = new List<string>();
            document
                .When(x => x.Clone(Arg.Any<string>()))
                .Do(x => items.Add(x.Arg<string>()));
            document.Stream.Returns(new MemoryStream(Encoding.UTF8.GetBytes(@"@for(int c = 0 ; c < 5 ; c++) { <p>@c</p> }")));
            Razor razor = new Razor();

            // When
            razor.Execute(new[] { document }, context).ToList();  // Make sure to materialize the result list

            // Then
            document.Received().Clone(Arg.Any<string>());
            Assert.AreEqual(1, items.Count());
            Assert.AreEqual(" <p>0</p>  <p>1</p>  <p>2</p>  <p>3</p>  <p>4</p> ", items.First());
        }

        [Test]
        public void Metadata()
        {
            // Given
            string inputFolder = Path.Combine(Environment.CurrentDirectory, @".\Input");
            if (!Directory.Exists(inputFolder))
            {
                Directory.CreateDirectory(inputFolder);
            }
            IExecutionContext context = Substitute.For<IExecutionContext>();
            context.RootFolder.Returns(Environment.CurrentDirectory);
            context.InputFolder.Returns(inputFolder);
            Engine engine = new Engine();
            engine.Configure();
            context.Assemblies.Returns(engine.Assemblies);
            IDocument document = Substitute.For<IDocument>();
            document.Metadata["MyKey"].Returns("MyValue");
            List<string> items = new List<string>();
            document
                .When(x => x.Clone(Arg.Any<string>()))
                .Do(x => items.Add(x.Arg<string>()));
            document.Stream.Returns(new MemoryStream(Encoding.UTF8.GetBytes(@"<p>@Metadata[""MyKey""]</p>")));
            Razor razor = new Razor();

            // When
            razor.Execute(new[] { document }, context).ToList();

            // Then
            document.Received().Clone(Arg.Any<string>());
            Assert.AreEqual(1, items.Count());
            Assert.AreEqual("<p>MyValue</p>", items.First());
        }

        [Test]
        public void LoadSimpleTemplateFile()
        {
            // Given
            Engine engine = new Engine();
            engine.InputFolder = @"TestFiles\Input\";
            ReadFiles readFiles = new ReadFiles(@"SimpleTemplate\Test.cshtml");
            Razor razor = new Razor();
            Meta meta = new Meta("Content", x => x.Content);
            engine.Pipelines.Add("Pipeline", readFiles, razor, meta);

            // When
            engine.Execute();

            // Then
            Assert.AreEqual(1, engine.Documents.FromPipeline("Pipeline").Count());
            Assert.AreEqual(@"<p>This is a test</p>", engine.Documents.FromPipeline("Pipeline").First().String("Content"));
        }

        [Test]
        public void LoadLayoutFile()
        {
            // Given
            Engine engine = new Engine();
            engine.InputFolder = @"TestFiles\Input\";
            ReadFiles readFiles = new ReadFiles(@"Layout\Test.cshtml");
            Razor razor = new Razor();
            Meta meta = new Meta("Content", x => x.Content);
            engine.Pipelines.Add("Pipeline", readFiles, razor, meta);

            // When
            engine.Execute();

            // Then
            Assert.AreEqual(1, engine.Documents.FromPipeline("Pipeline").Count());
            Assert.AreEqual("LAYOUT\r\n\r\n<p>This is a test</p>", engine.Documents.FromPipeline("Pipeline").First().String("Content"));
        }

        [Test]
        public void LoadViewStartAndLayoutFile()
        {
            // Given
            Engine engine = new Engine();
            engine.InputFolder = @"TestFiles\Input\";
            ReadFiles readFiles = new ReadFiles(@"ViewStartAndLayout\Test.cshtml");
            Razor razor = new Razor();
            Meta meta = new Meta("Content", x => x.Content);
            engine.Pipelines.Add("Pipeline", readFiles, razor, meta);

            // When
            engine.Execute();

            // Then
            Assert.AreEqual(1, engine.Documents.FromPipeline("Pipeline").Count());
            Assert.AreEqual("LAYOUT\r\n<p>This is a test</p>", engine.Documents.FromPipeline("Pipeline").First().String("Content"));
        }

        [Test]
        public void AlternateViewStartPath()
        {
            // Given
            Engine engine = new Engine();
            engine.InputFolder = @"TestFiles\Input\";
            ReadFiles readFiles = new ReadFiles(@"AlternateViewStartPath\Test.cshtml");
            Razor razor = new Razor().SetViewStart(@"AlternateViewStart\_ViewStart.cshtml");
            Meta meta = new Meta("Content", x => x.Content);
            engine.Pipelines.Add("Pipeline", readFiles, razor, meta);

            // When
            engine.Execute();

            // Then
            Assert.AreEqual(1, engine.Documents.FromPipeline("Pipeline").Count());
            Assert.AreEqual("LAYOUT\r\n<p>This is a test</p>", engine.Documents.FromPipeline("Pipeline").First().String("Content"));
        }

        [Test]
        public void IgnoresUnderscoresByDefault()
        {
            // Given
            Engine engine = new Engine();
            engine.InputFolder = @"TestFiles\Input\";
            ReadFiles readFiles = new ReadFiles(@"IgnoreUnderscores\*.cshtml");
            Razor razor = new Razor();
            Meta meta = new Meta("Content", x => x.Content);
            engine.Pipelines.Add("Pipeline", readFiles, razor, meta);

            // When
            engine.Execute();

            // Then
            Assert.AreEqual(1, engine.Documents.FromPipeline("Pipeline").Count());
            Assert.AreEqual("LAYOUT\r\n\r\n<p>This is a test</p>", engine.Documents.FromPipeline("Pipeline").First().String("Content"));
        }

        [Test]
        public void AlternateIgnorePrefix()
        {
            // Given
            Engine engine = new Engine();
            engine.InputFolder = @"TestFiles\Input\";
            ReadFiles readFiles = new ReadFiles(@"AlternateIgnorePrefix\*.cshtml");
            Razor razor = new Razor().IgnorePrefix("Ignore");
            Meta meta = new Meta("Content", x => x.Content);
            engine.Pipelines.Add("Pipeline", readFiles, razor, meta);

            // When
            engine.Execute();

            // Then
            Assert.AreEqual(1, engine.Documents.FromPipeline("Pipeline").Count());
            Assert.AreEqual(@"<p>This is a test</p>", engine.Documents.FromPipeline("Pipeline").First().String("Content"));
        }
    }
}
