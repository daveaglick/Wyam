﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wyam.Common.Configuration;
using Wyam.Common.Documents;
using Wyam.Common.Execution;
using Wyam.Common.IO;
using Wyam.Common.Meta;
using Wyam.Common.Modules;
using Wyam.Core.Modules.Contents;
using Wyam.Core.Modules.Control;
using Wyam.Core.Modules.Extensibility;
using Wyam.Core.Modules.IO;
using Wyam.Core.Modules.Metadata;
using Wyam.Feeds;
using Wyam.Html;

namespace Wyam.Blog
{
    public class Blog : IRecipe
    {
        public void Apply(IEngine engine)
        {
            // Global metadata defaults
            engine.Settings[BlogKeys.Title] = "My Blog";
            engine.Settings[BlogKeys.Description] = "Welcome!";
            engine.Settings[BlogKeys.MarkdownExtensions] = "advanced+bootstrap";
            engine.Settings[BlogKeys.IncludeDateInPostPath] = false;
            engine.Settings[BlogKeys.PostsPath] = new DirectoryPath("posts");
            engine.Settings[BlogKeys.MetaRefreshRedirects] = true;
            engine.Settings[BlogKeys.RssPath] = GenerateFeeds.DefaultRssPath;
            engine.Settings[BlogKeys.AtomPath] = GenerateFeeds.DefaultAtomPath;
            engine.Settings[BlogKeys.RdfPath] = GenerateFeeds.DefaultRdfPath;

            // Get the pages first so they're available in the navbar, but don't render until last
            engine.Pipelines.Add(BlogPipelines.Pages,
                new ReadFiles(ctx => $"{{!{ctx.DirectoryPath(BlogKeys.PostsPath).FullPath},**}}/*.md"),
                new FrontMatter(new Yaml.Yaml()),
                new Execute(ctx => new Markdown.Markdown().UseConfiguration(ctx.String(BlogKeys.MarkdownExtensions))),
                new Concat(
                    // Add any additional Razor pages
                    new ReadFiles(ctx => $"{{!{ctx.DirectoryPath(BlogKeys.PostsPath).FullPath},!tags,**}}/*.cshtml"),
                    new FrontMatter(new Yaml.Yaml())
                ),
                new Concat(
                    // Add the posts index page
                    new ReadFiles("posts/index.cshtml"),
                    new FrontMatter(new Yaml.Yaml()),
                    new Meta(Keys.RelativeFilePath, ctx => ctx.DirectoryPath(BlogKeys.PostsPath).CombineFile("index.cshtml"))
                ),
                new Concat(
                    // Add the tags index page
                    new ReadFiles("tags/index.cshtml"),
                    new FrontMatter(new Yaml.Yaml())
                ),
                // Copy the index page image and header text color from global metadata (if there is one)
                new If((doc, ctx) => doc.FilePath(Keys.RelativeFilePath).Equals(new FilePath("index.cshtml")) && ctx.ContainsKey(BlogKeys.Image),
                    new Meta(BlogKeys.Image, ctx => ctx[BlogKeys.Image])),
                new If((doc, ctx) => doc.FilePath(Keys.RelativeFilePath).Equals(new FilePath("index.cshtml")) && ctx.ContainsKey(BlogKeys.HeaderTextColor),
                    new Meta(BlogKeys.HeaderTextColor, ctx => ctx[BlogKeys.HeaderTextColor])),
                new WriteFiles(".html")
                    .OnlyMetadata()
            );

            engine.Pipelines.Add(BlogPipelines.RawPosts,
                new ReadFiles(ctx => $"{ctx.DirectoryPath(BlogKeys.PostsPath).FullPath}/*.md"),
                new FrontMatter(new Yaml.Yaml()),
                new Execute(ctx => new Markdown.Markdown().UseConfiguration(ctx.String(BlogKeys.MarkdownExtensions))),
                new Concat(
                    // Add any posts written in Razor
                    new ReadFiles(ctx => $"{ctx.DirectoryPath(BlogKeys.PostsPath).FullPath}/{{!_,!index,}}*.cshtml"),
                    new FrontMatter(new Yaml.Yaml())),
                new Meta("FrontMatterPublished", (doc, ctx) => doc.ContainsKey(BlogKeys.Published)),  // Record whether the publish date came from front matter
                new Meta(BlogKeys.Published, (doc, ctx) =>
                {
                    DateTime published;
                    if (!DateTime.TryParse(doc.String(Keys.SourceFileName).Substring(0, 10), out published))
                    {
                        Wyam.Common.Tracing.Trace.Warning($"Could not parse published date for {doc.Source?.FullPath ?? "[unknown]"}.");
                        return null;
                    }
                    return published;
                }).OnlyIfNonExisting(),
                new Where((doc, ctx) => 
                {
                    if (!doc.ContainsKey(BlogKeys.Published) || doc.Get(BlogKeys.Published) == null)
                    {
                        Common.Tracing.Trace.Warning($"Skipping {doc.Source} due to not having {BlogKeys.Published} metadata");
                        return false;
                    }
                    if (doc.Get<DateTime>(BlogKeys.Published) > DateTime.Now)
                    {
                        Common.Tracing.Trace.Warning($"Skipping {doc.Source} due to having {BlogKeys.Published} metadata of {doc.Get<DateTime>(BlogKeys.Published)} in the future (current date and time is {DateTime.Now})");
                        return false;
                    }
                    return true;
                }),
                new Meta(Keys.RelativeFilePath, (doc, ctx) =>
                {
                    DateTime published = doc.Get<DateTime>(BlogKeys.Published);
                    string fileName = doc.Bool("FrontMatterPublished")
                        ? doc.FilePath(Keys.SourceFileName).ChangeExtension("html").FullPath
                        : doc.FilePath(Keys.SourceFileName).ChangeExtension("html").FullPath.Substring(11);
                    return ctx.Bool(BlogKeys.IncludeDateInPostPath) 
                        ? $"{ctx.DirectoryPath(BlogKeys.PostsPath).FullPath}/{published:yyyy}/{published:MM}/{fileName}" 
                        : $"{ctx.DirectoryPath(BlogKeys.PostsPath).FullPath}/{fileName}";
                }),
                new OrderBy((doc, ctx) => doc.Get<DateTime>(BlogKeys.Published)).Descending()
            );

            engine.Pipelines.Add(BlogPipelines.Tags,
                new ReadFiles("tags/tag.cshtml"),
                new FrontMatter(new Yaml.Yaml()),
                new Execute(ctx => 
                    new GroupByMany(BlogKeys.Tags, new Documents(BlogPipelines.RawPosts))
                        .WithComparer(ctx.Bool(BlogKeys.CaseInsensitiveTags) ? StringComparer.OrdinalIgnoreCase : null)),
                new Where((doc, ctx) => !string.IsNullOrEmpty(doc.String(Keys.GroupKey))),
                new Meta(BlogKeys.Tag, (doc, ctx) => doc.String(Keys.GroupKey)),
                new Meta(BlogKeys.Title, (doc, ctx) => doc.String(Keys.GroupKey)),
                new Meta(BlogKeys.Posts, (doc, ctx) => doc.List<IDocument>(Keys.GroupDocuments)),
                new Meta(Keys.RelativeFilePath, (doc, ctx) =>
                {
                    string tag = doc.String(Keys.GroupKey);
                    return $"tags/{(tag.StartsWith(".") ? tag.Substring(1) : tag).ToLowerInvariant().Replace(' ', '-')}.html";
                }),
                new Razor.Razor()
                    .WithLayout("/_Layout.cshtml"),
                new WriteFiles()
            );

            // Defer rendering the posts until after the tags have been generated
            engine.Pipelines.Add(BlogPipelines.Posts,
                new Documents(BlogPipelines.RawPosts),
                new Razor.Razor()
                    .WithLayout("/_PostLayout.cshtml"),
                new Excerpt()
                    .WithMetadataKey(BlogKeys.Excerpt),
                new Excerpt("div#content")
                    .WithMetadataKey(BlogKeys.Content)
                    .WithOuterHtml(false),
                new WriteFiles(".html"),
                // Order them again since the order would have gotten messed up by the concurrent Razor rendering
                new OrderBy((doc, ctx) => doc.Get<DateTime>(BlogKeys.Published)).Descending());

            engine.Pipelines.Add(BlogPipelines.Feed,
                new Documents(BlogPipelines.Posts),
                new GenerateFeeds()
                    .WithRssPath(ctx => ctx.FilePath(BlogKeys.RssPath))
                    .WithAtomPath(ctx => ctx.FilePath(BlogKeys.AtomPath))
                    .WithRdfPath(ctx => ctx.FilePath(BlogKeys.RdfPath)),
                new WriteFiles());

            engine.Pipelines.Add(BlogPipelines.RenderPages,
                new Documents(BlogPipelines.Pages),
                new Razor.Razor()
                    .WithLayout("/_Layout.cshtml"),
                new WriteFiles()
            );

            engine.Pipelines.Add(BlogPipelines.Redirects,
                new Documents(BlogPipelines.RenderPages),
                new Concat(
                    new Documents(BlogPipelines.Posts)
                ),
                new Execute(ctx =>
                {
                    Redirect redirect = new Redirect()
                        .WithMetaRefreshPages(ctx.Bool(BlogKeys.MetaRefreshRedirects));
                    if (ctx.Bool(BlogKeys.NetlifyRedirects))
                    {
                        redirect.WithAdditionalOutput("_redirects", redirects =>
                            string.Join(Environment.NewLine, redirects.Select(r => $"/{r.Key} {r.Value}")));
                    }
                    return redirect;
                }),
                new WriteFiles()
            );

            engine.Pipelines.Add(BlogPipelines.Resources,
                new CopyFiles("**/*{!.cshtml,!.md,}")
            );

            engine.Pipelines.Add(BlogPipelines.ValidateLinks,
                new If(ctx => ctx.Bool(BlogKeys.ValidateAbsoluteLinks) || ctx.Bool(BlogKeys.ValidateRelativeLinks),
                    new Documents(BlogPipelines.RenderPages),
                    new Concat(
                        new Documents(BlogPipelines.Posts)
                    ),
                    new Concat(
                        new Documents(BlogPipelines.Resources)
                    ),
                    new Where((doc, ctx) =>
                    {
                        FilePath destinationPath = doc.FilePath(Keys.DestinationFilePath);
                        return destinationPath != null 
                            && (destinationPath.Extension == ".html" || destinationPath.Extension == ".htm");
                    }),
                    new Execute(ctx =>
                        new ValidateLinks()
                            .ValidateAbsoluteLinks(ctx.Bool(BlogKeys.ValidateAbsoluteLinks))
                            .ValidateRelativeLinks(ctx.Bool(BlogKeys.ValidateRelativeLinks))
                            .AsError(ctx.Bool(BlogKeys.ValidateLinksAsError)
                        )
                    )
                )
            );
        }

        public void Scaffold(IFile configFile, IDirectory inputDirectory)
        {
            // Config file
            configFile?.WriteAllText(@"#recipe Blog");

            // Add info page
            inputDirectory.GetFile("about.md").WriteAllText(
@"Title: About Me
---
I'm awesome!");

            // Add post page
            inputDirectory.GetFile("posts/first-post.md").WriteAllText(
@"Title: First Post
Published: 1/1/2016
Tags: Introduction
---
This is my first post!");
        }
    }
}
