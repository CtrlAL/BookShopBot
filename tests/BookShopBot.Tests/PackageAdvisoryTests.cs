using System.Diagnostics;
using System.Text;
using Xunit;

namespace BookShopBot.Tests;

public sealed class PackageAdvisoryTests
{
    private static readonly string[] AffectedProjects =
    [
        @"BookShop\BookShop.ServiceDefaults\BookShop.ServiceDefaults.csproj",
        @"BookShop\BookCatalogService\BookCatalogService.csproj",
        @"BookShop\BookRecognitionService\BookRecognitionService.csproj",
    ];

    [Fact]
    public async Task BookShopTests_PackageAdvisoriesWereFixed()
    {
        string repoRoot = LocateRepoRoot();

        (int ExitCode, string Output) result = await RunDotnetBuildAsync(repoRoot);

        Assert.True(result.ExitCode == 0, $"dotnet build exited with {result.ExitCode}.{Environment.NewLine}{result.Output}");
        Assert.DoesNotContain("NU1902", result.Output);
        Assert.DoesNotContain("Grpc.Net.ClientFactory 2.63.0 or earlier", result.Output);
    }

    private static async Task<(int ExitCode, string Output)> RunDotnetBuildAsync(string repoRoot)
    {
        var output = new StringBuilder();
        int exitCode = 0;

        foreach (string project in AffectedProjects)
        {
            string projectPath = Path.Combine(repoRoot, project);

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            startInfo.ArgumentList.Add("build");
            startInfo.ArgumentList.Add(projectPath);
            startInfo.ArgumentList.Add("--no-restore");
            startInfo.ArgumentList.Add("-p:WarningsAsErrors=CS*");

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start 'dotnet build'.");

            string stdout = await process.StandardOutput.ReadToEndAsync();
            string stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            output.AppendLine(stdout);
            output.AppendLine(stderr);

            if (process.ExitCode != 0)
            {
                exitCode = process.ExitCode;
            }
        }

        return (exitCode, output.ToString());
    }

    private static string LocateRepoRoot()
    {
        DirectoryInfo? candidate = new DirectoryInfo(AppContext.BaseDirectory);

        while (candidate is not null)
        {
            string solutionFile = Path.Combine(candidate.FullName, "BookShop", "BookShop.sln");

            if (File.Exists(solutionFile))
            {
                return candidate.FullName;
            }

            candidate = candidate.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}