﻿using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Stryker.Core.Logging;
using Stryker.Core.Testing;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Xml.Linq;

namespace Stryker.Core.Initialisation
{
    /// <summary>
    /// Resolving the MetadataReferences for compiling later
    /// This has to be done using msbuild because currently msbuild is the only reliable way of resolving all referenced assembly locations
    /// </summary>
    public class AssemblyReferenceResolver : IAssemblyReferenceResolver
    {
        private IProcessExecutor _processExecutor { get; set; }
        private IMetadataReferenceProvider _metadataReference { get; set; }
        private ILogger _logger { get; set; }

        public AssemblyReferenceResolver(IProcessExecutor processExecutor, IMetadataReferenceProvider metadataReference)
        {
            _processExecutor = processExecutor;
            _metadataReference = metadataReference;
            _logger = ApplicationLogging.LoggerFactory.CreateLogger<AssemblyReferenceResolver>();
        }

        public AssemblyReferenceResolver() : this(new ProcessExecutor(), new MetadataReferenceProvider()) { }
        
        /// <summary>
        /// Uses msbuild to resolve all references for the given test project
        /// </summary>
        /// <param name="projectFile">The test project file location</param>
        /// <returns>References</returns>
        public IEnumerable<PortableExecutableReference> ResolveReferences(string projectPath, string projectFileName, string projectUnderTestAssemblyName, IEnumerable<string> csprojes)
        {
            // Execute dotnet msbuild with the task PrintReferences
            var references = new List<PortableExecutableReference>();
            //_logger.LogWarning("Resolving mscorelib");
            //string mscorelibLocation = typeof(string).Assembly.Location;
            //_logger.LogWarning($"Found mscorelib at {mscorelibLocation}");
            //references.Add(_metadataReference.CreateFromFile(mscorelibLocation));

            _logger.LogWarning("Resolving mscorlib");

            string mscorlibLocation = Directory.GetFiles(@"C:\Windows\Microsoft.NET\assembly\GAC_64", "mscorlib.dll", SearchOption.AllDirectories).Single();
            _logger.LogWarning($"Found mscorlib at {mscorlibLocation}");
            references.Add(_metadataReference.CreateFromFile(mscorlibLocation));
            foreach (var csproj in csprojes)
            {
                _logger.LogWarning(csproj);
                _logger.LogWarning(Path.GetFullPath(@"..\", csproj));
                _logger.LogWarning(csproj.Split(@"/").Last());
                
                var result = _processExecutor.Start(
                    Path.GetFullPath(@"..\", csproj),
                    "dotnet",
                    $"msbuild {csproj.Split(@"/").Last()} /nologo");

                _logger.LogTrace(@"""{0} dotnet msbuild {1} /nologo "" resulted in {2}", projectPath, projectFileName, result.Output);

                if (result.ExitCode != 0)
                {
                    _logger.LogError(@"The task PrintReferences was not found in your project file. Please add the task to {0}", projectFileName);
                    throw new ApplicationException($"The task PrintReferences was not found in your project file. Please add the task to {projectFileName}");
                }
                           
                _logger.LogWarning("Resolving references");
                var files = Directory.GetFiles($"{Path.GetFullPath(@"..\", csproj)}\\bin\\Debug").Where(a => Path.GetExtension(a) == ".dll");
                _logger.LogWarning($"Found {files.Count()} refernces");

                foreach (var file in files)
                {
                    _logger.LogWarning($"Resolved reference {file.Trim()}");
                    references.Add(_metadataReference.CreateFromFile(file.Trim()));
                }                
            }
            return references;
            //var rows = result.Output.Split(new string[] { Environment.NewLine.ToString() }, StringSplitOptions.None).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

            // All rows except the last contain the project dependencies
            //foreach (var reference in GetReferencePathsFromOutput(rows.Reverse().Skip(1))
            //    .Distinct()
            //    .Where(x => Path.GetFileNameWithoutExtension(x) != projectUnderTestAssemblyName))
            //{
            //    _logger.LogDebug(@"Resolved reference {0}", reference.Trim());
            //    yield return _metadataReference.CreateFromFile(reference.Trim());
            //}

            //// the last part contains the package dependencies, seperated by the path seperator char
            //foreach (var reference in GetAssemblyPathsFromOutput(rows.Last())
            //    .Distinct()
            //    .Where(x => Path.GetFileNameWithoutExtension(x) != projectUnderTestAssemblyName))
            //{
            //    _logger.LogDebug(@"Resolved reference {0}", reference.Trim());
            //    yield return _metadataReference.CreateFromFile(reference.Trim());
            //}
        }


        /// <summary>
        /// Subtracts all paths from PathSeperator seperated string
        /// </summary>
        /// <returns>All references this project has</returns>
        public IEnumerable<string> GetAssemblyPathsFromOutput(string paths)
        {
            foreach (var path in paths.Split(';'))
            {
                if (Path.GetExtension(path) == ".dll")
                {
                    yield return path;
                }
            }
        }

        /// <summary>
        /// Subtracts all paths from PathSeperator seperated string
        /// </summary>
        /// <returns>All references this project has</returns>
        public IEnumerable<string> GetReferencePathsFromOutput(IEnumerable<string> paths)
        {
            foreach (var pathPrintOutput in paths)
            {
                var path = pathPrintOutput.Split(new string[] { " -> " }, StringSplitOptions.None).Last();

                if (Path.GetExtension(path) == ".dll")
                {
                    yield return path;
                }
            }
        }
    }
}
