﻿using Microsoft.Extensions.Logging;
using Stryker.Core.Exceptions;
using Stryker.Core.Logging;
using Stryker.Core.Testing;
using Stryker.Core.ToolHelpers;
using System.IO;

namespace Stryker.Core.Initialisation
{
    public interface IInitialBuildProcess
    {
        void InitialBuild(bool fullFramework, string path, string solutionPath, string projectName);
    }

    public class InitialBuildProcess : IInitialBuildProcess
    {
        private IProcessExecutor _processExecutor { get; set; }
        private ILogger _logger { get; set; }

        public InitialBuildProcess(IProcessExecutor processExecutor = null)
        {
            _processExecutor = processExecutor ?? new ProcessExecutor();
            _logger = ApplicationLogging.LoggerFactory.CreateLogger<InitialBuildProcess>();
        }

        public void InitialBuild(bool fullFramework, string path, string solutionPath, string projectName)
        {
            _logger.LogInformation("Started initial build using {0}", fullFramework ? "msbuild.exe" : "dotnet build");
            ProcessResult result = null;
            if (fullFramework)
            {
                if (string.IsNullOrWhiteSpace(solutionPath))
                {
                    throw new StrykerInputException("Solution path is required on .net framework projects. Please provide your solution path using --solution-path ...");
                }
                solutionPath = Path.GetFullPath(solutionPath);
                string solutionDir = Path.GetDirectoryName(solutionPath);

                // Validate nuget.exe is installed and included in path
                var nugetWhereExeResult = _processExecutor.Start(solutionDir, "where.exe", "nuget.exe");
                if (!nugetWhereExeResult.Output.Contains("nuget.exe"))
                {
                    throw new StrykerInputException("Nuget.exe should be installed to restore .net framework nuget packages. Install nuget.exe and make sure it's included in your path.");
                }

                // Restore packages using nuget.exe
                var nugetRestoreResult = _processExecutor.Start(solutionDir, "powershell.exe", $"nuget restore {solutionPath}");
                if (nugetRestoreResult.ExitCode != 0)
                {
                    throw new StrykerInputException("Nuget.exe failed to restore packages for your solution. Please review your nuget setup.");
                }

                // Build project with MSBuild.exe
                var msBuildPath = new MsBuildHelper().GetMsBuildPath();
                _logger.LogDebug("Located MSBuild.exe at: {0}", msBuildPath);

                result = _processExecutor.Start(solutionDir, msBuildPath, solutionPath);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(solutionPath))
                {
                    _logger.LogWarning("Stryker is running on a .net core project but a solution path was provided. The solution path option is only needed on .net framework projects and can be removed. Please update your stryker options.");
                }
                // Build with dotnet build
                result = _processExecutor.Start(path, "dotnet", $"build {projectName}");
            }

            _logger.LogDebug("Initial build output {0}", result.Output);
            if (result.ExitCode != 0)
            {
                // Initial build failed
                throw new StrykerInputException("Initial build of targeted project failed. Please make targeted project buildable.", result.Output);
            }
            _logger.LogInformation("Initial build successful");
        }
    }
}
