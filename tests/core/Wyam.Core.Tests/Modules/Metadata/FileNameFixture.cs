﻿using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wyam.Common.Documents;
using Wyam.Common.IO;
using Wyam.Common.Meta;
using Wyam.Common.Execution;
using Wyam.Common.Modules;
using Wyam.Common.Util;
using Wyam.Core.Documents;
using Wyam.Core.Modules.Metadata;
using Wyam.Core.Execution;
using Wyam.Testing;

namespace Wyam.Core.Tests.Modules.Metadata
{
    [TestFixture]
    [NonParallelizable]
    public class FileNameFixture : BaseFixture
    {
        public class ExecuteTests : FileNameFixture
        {
            [TestCase(
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~:/?#[]@!$&'()*+,;=",
                "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz0123456789")]
            [TestCase("Děku.jemeविकीвики-движка", "děku.jemeविकीвикидвижка")]
            [TestCase(
                "this is my title - and some \t\t\t\t\n   clever; (piece) of text here: [ok].",
                "this-is-my-title-and-some-clever-piece-of-text-here-ok")]
            [TestCase(
                "this is my title?!! /r/science/ and #firstworldproblems :* :sadface=true",
                "this-is-my-title-rscience-and-firstworldproblems-sadfacetrue")]
            [TestCase(
                "one-two-three--four--five and a six--seven--eight-nine------ten",
                "onetwothreefourfive-and-a-sixseveneightnineten")]
            public void FileNameIsConvertedCorrectly(string input, string output)
            {
                // Given
                Engine engine = new Engine();
                ExecutionPipeline pipeline = new ExecutionPipeline("Pipeline", (IModuleList)null);
                IExecutionContext context = new ExecutionContext(engine, Guid.Empty, pipeline);
                IDocument[] inputs =
                {
                    context.GetDocument(new MetadataItems
                {
                    new MetadataItem(Keys.SourceFileName, input)
                })
                };
                FileName fileName = new FileName();

                // When
                IEnumerable<IDocument> documents = fileName.Execute(inputs, context);

                // Then
                Assert.AreEqual(output, documents.First().FilePath(Keys.WriteFileName).FullPath);
            }

            [Test]
            public void FileNameShouldBeLowercase()
            {
                // Given
                const string input = "FileName With MiXeD CapS";
                const string output = "filename-with-mixed-caps";

                Engine engine = new Engine();
                ExecutionPipeline pipeline = new ExecutionPipeline("Pipeline", (IModuleList)null);
                IExecutionContext context = new ExecutionContext(engine, Guid.Empty, pipeline);
                IDocument[] inputs =
                {
                    context.GetDocument(new MetadataItems
                {
                    new MetadataItem(Keys.SourceFileName, new FilePath(input))
                })
                };
                FileName fileName = new FileName();

                // When
                IEnumerable<IDocument> documents = fileName.Execute(inputs, context);

                // Then
                Assert.AreEqual(output, documents.First().FilePath(Keys.WriteFileName).FullPath);
            }

            [Test]
            public void WithAllowedCharactersDoesNotReplaceProvidedCharacters()
            {
                // Given
                const string input = "this-is-a-.net-tag";
                const string output = "this-is-a-.net-tag";

                Engine engine = new Engine();
                ExecutionPipeline pipeline = new ExecutionPipeline("Pipeline", (IModuleList)null);
                IExecutionContext context = new ExecutionContext(engine, Guid.Empty, pipeline);
                IDocument[] inputs =
                {
                    context.GetDocument(new MetadataItems
                {
                    new MetadataItem(Keys.SourceFileName, new FilePath(input))
                })
                };
                FileName fileName = new FileName();

                // When
                fileName = fileName.WithAllowedCharacters(new string[] { "-" });
                IEnumerable<IDocument> documents = fileName.Execute(inputs, context);

                // Then
                Assert.AreEqual(output, documents.First().FilePath(Keys.WriteFileName).FullPath);
            }

            [Test]
            public void WithAllowedCharactersDoesNotReplaceDotAtEnd()
            {
                // Given
                const string input = "this-is-a-.";
                const string output = "thisisa.";

                Engine engine = new Engine();
                ExecutionPipeline pipeline = new ExecutionPipeline("Pipeline", (IModuleList)null);
                IExecutionContext context = new ExecutionContext(engine, Guid.Empty, pipeline);
                IDocument[] inputs =
                {
                    context.GetDocument(new MetadataItems
                {
                    new MetadataItem(Keys.SourceFileName, new FilePath(input))
                })
                };
                FileName fileName = new FileName();

                // When
                fileName = fileName.WithAllowedCharacters(new string[] { "." });
                IEnumerable<IDocument> documents = fileName.Execute(inputs, context);

                // Then
                Assert.AreEqual(output, documents.First().FilePath(Keys.WriteFileName).FullPath);
            }

            public static string[] ReservedChars => FileName.ReservedChars;

            [Test]
            [TestCaseSource(nameof(ReservedChars))]
            public void FileNameIsConvertedCorrectlyWithReservedChar(string character)
            {
                // Given
                Engine engine = new Engine();
                ExecutionPipeline pipeline = new ExecutionPipeline("Pipeline", (IModuleList)null);
                IExecutionContext context = new ExecutionContext(engine, Guid.Empty, pipeline);
                string manyCharactersWow = new string(character[0], 10);
                IDocument[] inputs =
                {
                    context.GetDocument(new MetadataItems
                {
                    new MetadataItem(
                        Keys.SourceFileName,
                        string.Format("testing {0} some of {0} these {0}", manyCharactersWow))
                })
                };
                FileName fileName = new FileName();

                // When
                IEnumerable<IDocument> documents = fileName.Execute(inputs, context);

                // Then
                Assert.AreEqual("testing-some-of-these-", documents.First().FilePath(Keys.WriteFileName).FullPath);
            }

            [TestCase(null)]
            [TestCase("")]
            [TestCase(" ")]
            public void IgnoresNullOrWhiteSpaceStrings(string input)
            {
                // Given
                Engine engine = new Engine();
                ExecutionPipeline pipeline = new ExecutionPipeline("Pipeline", (IModuleList)null);
                IExecutionContext context = new ExecutionContext(engine, Guid.Empty, pipeline);
                IDocument[] inputs =
                {
                    context.GetDocument(new MetadataItems
                {
                    new MetadataItem(Keys.SourceFileName, input)
                })
                };
                FileName fileName = new FileName();

                // When
                IEnumerable<IDocument> documents = fileName.Execute(inputs, context);

                // Then
                Assert.IsFalse(documents.First().ContainsKey(Keys.WriteFileName));
            }

            [Test]
            public void PreservesExtension()
            {
                // Given
                const string input = "myfile.html";
                const string output = "myfile.html";

                Engine engine = new Engine();
                ExecutionPipeline pipeline = new ExecutionPipeline("Pipeline", (IModuleList)null);
                IExecutionContext context = new ExecutionContext(engine, Guid.Empty, pipeline);
                IDocument[] inputs =
                {
                    context.GetDocument(new MetadataItems
                    {
                        new MetadataItem("MyKey", input)
                    })
                };
                FileName fileName = new FileName("MyKey");

                // When
                IEnumerable<IDocument> documents = fileName.Execute(inputs, context);

                // Then
                Assert.AreEqual(output, documents.First().FilePath(Keys.WriteFileName).FullPath);
            }

            [Test]
            public void TrimWhitespace()
            {
                // Given
                const string input = "   myfile.html   ";
                const string output = "myfile.html";

                Engine engine = new Engine();
                ExecutionPipeline pipeline = new ExecutionPipeline("Pipeline", (IModuleList)null);
                IExecutionContext context = new ExecutionContext(engine, Guid.Empty, pipeline);
                IDocument[] inputs =
                {
                    context.GetDocument(new MetadataItems
                    {
                        new MetadataItem("MyKey", input)
                    })
                };
                FileName fileName = new FileName("MyKey");

                // When
                IEnumerable<IDocument> documents = fileName.Execute(inputs, context);

                // Then
                Assert.AreEqual(output, documents.First().FilePath(Keys.WriteFileName).FullPath);
            }
        }
    }
}
