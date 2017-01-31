﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wyam.Common.Configuration;
using Wyam.Common.IO;
using Wyam.Common.Modules;
using Wyam.Common.Tracing;
using Wyam.Configuration.Assemblies;
using Wyam.Configuration.ConfigScript;
using Wyam.Configuration.NuGet;
using Wyam.Configuration.Preprocessing;
using Wyam.Core.Execution;

namespace Wyam.Configuration
{
    /// <summary>
    /// Manages configuration of an engine and coordinates configuration script processing.
    /// </summary>
    public class Configurator : IDisposable
    {
        private readonly ScriptManager _scriptManager = new ScriptManager();
        private readonly Engine _engine;
        private readonly Preprocessor _preprocessor;
        private readonly AssemblyResolver _assemblyResolver;

        private bool _disposed;
        private bool _configured;

        public PackageInstaller PackageInstaller { get; }

        public AssemblyLoader AssemblyLoader { get; }

        public ClassCatalog ClassCatalog { get; }

        public bool OutputScript { get; set; }

        public FilePath OutputScriptPath { get; set; }

        public IReadOnlyDictionary<string, object> Settings { get; set; }

        public string RecipeName { get; set; }

        public IRecipe Recipe { get; set; }

        public string Theme { get; set; }

        public bool IgnoreKnownRecipePackages { get; set; }

        public bool IgnoreKnownThemePackages { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Configurator"/> class.
        /// </summary>
        /// <param name="engine">The engine to configure.</param>
        public Configurator(Engine engine)
            : this(engine, new Preprocessor())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Configurator"/> class. This overload
        /// allows passing in a <see cref="Preprocessor"/> that can be reused and pre-configured
        /// with directives not sourced from the script.
        /// </summary>
        /// <param name="engine">The engine to configure.</param>
        /// <param name="preprocessor">The preprocessor.</param>
        public Configurator(Engine engine, Preprocessor preprocessor)
        {
            _engine = engine;
            _preprocessor = preprocessor;
            _assemblyResolver = new AssemblyResolver(_scriptManager); 
            AssemblyLoader = new AssemblyLoader(engine.FileSystem, _assemblyResolver);
            PackageInstaller = new PackageInstaller(engine.FileSystem, AssemblyLoader);
            ClassCatalog = new ClassCatalog();
            engine.Namespaces.Add(typeof(ScriptBase).Namespace);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _assemblyResolver.Dispose();
            _disposed = true;
        }

        private void CheckDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Configurator));
            }
        }

        /// <summary>
        /// Configures the engine using the specified script.
        /// </summary>
        /// <param name="script">The script.</param>
        public void Configure(string script)
        {
            CheckDisposed();
            if (_configured)
            {
                throw new InvalidOperationException("Configuration has already been performed.");
            }
            _configured = true;

            // Parse the script (or use an empty result if no script)
            DirectiveParser directiveParser = new DirectiveParser(_preprocessor);
            if (script != null)
            {
                directiveParser.Parse(script);
            }

            // Process preprocessor directives
            _preprocessor.ProcessDirectives(this, directiveParser.DirectiveValues);
            
            // Initialize everything (order here is very important)
            AddRecipePackageAndSetTheme();
            AddThemePackagesAndPath();
            InstallPackages();
            LoadAssemblies();
            CatalogClasses();
            AddNamespaces();
            AddFileProviders();
            ApplyRecipe();
            SetMetadata();

            // Finally evaluate the script
            Evaluate(directiveParser.Code);
        }

        // Internal for testing
        internal void AddRecipePackageAndSetTheme()
        {
            if (Recipe == null && !string.IsNullOrEmpty(RecipeName))
            {
                KnownRecipe knownRecipe;
                if (KnownRecipe.Values.TryGetValue(RecipeName, out knownRecipe))
                {
                    Trace.Verbose($"Recipe {RecipeName} was in the lookup of known recipes");

                    // Make sure we're not ignoring packages
                    if (!IgnoreKnownRecipePackages)
                    {
                        // Add the package, but only if it wasn't added manually
                        if (!string.IsNullOrEmpty(knownRecipe.PackageId) && !PackageInstaller.ContainsPackage(knownRecipe.PackageId))
                        {
                            PackageInstaller.AddPackage(knownRecipe.PackageId);
                        }
                    }
                    else
                    {
                        Trace.Verbose("Ignoring known recipe packages");
                    }

                    // Set the theme if we don't already have one
                    if (string.IsNullOrEmpty(Theme))
                    {
                        Theme = knownRecipe.DefaultTheme;
                    }
                }
                else
                {
                    Trace.Verbose($"Recipe {RecipeName} is not in the lookup of known recipes");
                }
            }
        }

        // Internal for testing
        internal void AddThemePackagesAndPath()
        {
            string inputPath = Theme;
            if (!string.IsNullOrEmpty(Theme))
            {
                KnownTheme knownTheme;
                if(KnownTheme.Values.TryGetValue(Theme, out knownTheme))
                {
                    Trace.Verbose($"Theme {Theme} was in the lookup of known themes");
                    inputPath = knownTheme.InputPath;

                    // Do a sanity check against the recipe (but only if we didn't explicitly specify one)
                    if (Recipe == null && !string.IsNullOrEmpty(RecipeName) && !string.IsNullOrEmpty(knownTheme.Recipe)
                        && !string.Equals(RecipeName, knownTheme.Recipe, StringComparison.OrdinalIgnoreCase))
                    {
                        Trace.Warning($"Theme {Theme} is designed for recipe {knownTheme.Recipe} but is being used with recipe {RecipeName}, results may be unexpected");
                    }

                    // Make sure we're not ignoring theme packages
                    if(!IgnoreKnownThemePackages)
                    {
                        // Add any packages needed for the theme
                        if (knownTheme.PackageIds != null)
                        {
                            foreach (string themePackageId in knownTheme.PackageIds.Where(x => !PackageInstaller.ContainsPackage(x)))
                            {
                                PackageInstaller.AddPackage(themePackageId, allowPrereleaseVersions: true);
                            }
                        }
                    }
                    else
                    {
                        Trace.Verbose("Ignoring known theme packages");
                    }
                }
                else
                {
                    Trace.Verbose($"Theme {Theme} is not in the lookup of known themes, assuming it's an input path");
                }
            }

            // Insert the theme path
            if (!string.IsNullOrEmpty(inputPath))
            {
                _engine.FileSystem.InputPaths.Insert(0, new DirectoryPath(inputPath));
            }
        }

        private void InstallPackages()
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            using (Trace.WithIndent().Information("Installing NuGet packages"))
            {
                PackageInstaller.InstallPackages();
                stopwatch.Stop();
                Trace.Information($"NuGet packages installed in {stopwatch.ElapsedMilliseconds} ms");
            }
        }

        private void LoadAssemblies()
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            using (Trace.WithIndent().Information("Recursively loading assemblies"))
            {
                AssemblyLoader.Load();
                stopwatch.Stop();
                Trace.Information($"Assemblies loaded in {stopwatch.ElapsedMilliseconds} ms");
            }
        }

        private void CatalogClasses()
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            using (Trace.WithIndent().Information("Cataloging classes"))
            {
                ClassCatalog.CatalogTypes(AssemblyLoader.DirectAssemblies);
                stopwatch.Stop();
                Trace.Information($"Classes cataloged in {stopwatch.ElapsedMilliseconds} ms");
            }
        }

        private void AddNamespaces()
        {
            // Add all Wyam.Common namespaces
            _engine.Namespaces.AddRange(typeof(IModule).Assembly.GetTypes()
                .Where(x => !string.IsNullOrWhiteSpace(x.Namespace))
                .Select(x => x.Namespace)
                .Distinct());

            // Add all module namespaces
            _engine.Namespaces.AddRange(ClassCatalog.GetClasses<IModule>().Select(x => x.Namespace));
        }

        private void AddFileProviders()
        {
            foreach (IFileProvider fileProvider in ClassCatalog.GetInstances<IFileProvider>())
            {
                string scheme = fileProvider.GetType().Name.ToLowerInvariant();
                if (scheme.EndsWith("fileprovider"))
                {
                    scheme = scheme.Substring(0, scheme.Length - 12);
                }
                if (scheme.EndsWith("provider"))
                {
                    scheme = scheme.Substring(0, 8);
                }
                if (!string.IsNullOrEmpty(scheme))
                {
                    _engine.FileSystem.FileProviders.Add(scheme, fileProvider);
                }
            }
        }

        private void ApplyRecipe()
        {
            if (Recipe == null && !string.IsNullOrEmpty(RecipeName))
            {
                Recipe = ClassCatalog.GetInstance<IRecipe>(RecipeName, true);
                if (Recipe == null)
                {
                    throw new Exception($"The recipe \"{RecipeName}\" could not be found");
                }
            }
            if (Recipe != null)
            {
                _engine.Namespaces.Add(Recipe.GetType().Namespace);  // Add the recipe namespace so it's available to modules
                Recipe.Apply(_engine);
            }
        }

        private void SetMetadata()
        {
            // Set the global and initial metadata after applying the recipe in case the recipe sets default values
            if (Settings != null)
            {
                foreach (KeyValuePair<string, object> kvp in Settings)
                {
                    _engine.Settings[kvp.Key] = kvp.Value;
                }
            }
        }

        private void Evaluate(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return;
            }

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            using (Trace.WithIndent().Information("Evaluating configuration script"))
            {
                _scriptManager.Create(code, ClassCatalog.GetClasses<IModule>().ToList(), _engine.Namespaces);
                WriteScript(_scriptManager.Code);
                _scriptManager.Compile(AppDomain.CurrentDomain.GetAssemblies());
                _engine.DynamicAssemblies.Add(_scriptManager.RawAssembly);
                _scriptManager.Evaluate(_engine);
                stopwatch.Stop();
                Trace.Information($"Evaluated configuration script in {stopwatch.ElapsedMilliseconds} ms");
            }
        }

        private void WriteScript(string code)
        {
            // Output only if requested
            if (OutputScript)
            {
                FilePath outputPath = _engine.FileSystem.RootPath.CombineFile(OutputScriptPath ?? new FilePath($"{ScriptManager.AssemblyName}.cs"));
                _engine.FileSystem.GetFile(outputPath)?.WriteAllText(code);
            }
        }
    }
}
