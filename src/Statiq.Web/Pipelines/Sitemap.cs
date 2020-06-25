﻿using Statiq.Common;
using Statiq.Core;

namespace Statiq.Web.Pipelines
{
    public class Sitemap : Pipeline
    {
        public Sitemap()
        {
            Dependencies.AddRange(
                nameof(Content),
                nameof(Data),
                nameof(Archives));

            PostProcessModules = new ModuleList
            {
                new ExecuteIf(Config.FromSetting(WebKeys.GenerateSitemap, true))
                {
                    new ConcatDocuments(nameof(Content))
                    {
                        new FilterDocuments(Config.FromDocument(WebKeys.ShouldOutput, true))
                    },
                    new ConcatDocuments(nameof(Data))
                    {
                        new FilterDocuments(Config.FromDocument(WebKeys.ShouldOutput, true))
                    },
                    new ConcatDocuments(nameof(Archives)),
                    new FlattenTree(),
                    new FilterDocuments(
                        Config.FromDocument(doc => !doc.GetBool(Keys.TreePlaceholder)).CombineWith(
                        Config.FromDocument(WebKeys.IncludeInSitemap, true))),
                    new GenerateSitemap()
                }
            };

            OutputModules = new ModuleList
            {
                new WriteFiles()
            };
        }
    }
}
