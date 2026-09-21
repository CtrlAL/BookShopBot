using System.Text.RegularExpressions;
using Xunit;

namespace BookShopBot.Tests;

public sealed class SolutionStructureTests
{
    private static readonly string[] RequiredProjectFiles =
    [
        @"BookCatalogService\BookCatalogService.csproj",
        @"BookRecognitionService\BookRecognitionService.csproj",
        @"BookAiClient\BookAi.csproj",
        @"ChatFSM\Fsm.csproj",
        @"tests\BookShopBot.Tests\BookShopBot.Tests.csproj",
    ];

    [Fact]
    public void Solution_contains_core_projects()
    {
        string solutionPath = LocateSolutionFile();

        Assert.True(File.Exists(solutionPath), $"Solution file not found: {solutionPath}");

        IEnumerable<string> projectFiles = ReadProjectFiles(solutionPath);

        foreach (string required in RequiredProjectFiles)
        {
            bool found = projectFiles.Any(projectFile => projectFile.EndsWith(required, StringComparison.OrdinalIgnoreCase));

            Assert.True(found, $"Solution file '{solutionPath}' does not contain required project: {required}");
        }
    }

    private static string LocateSolutionFile()
    {
        DirectoryInfo? candidate = new DirectoryInfo(AppContext.BaseDirectory);

        while (candidate is not null)
        {
            string solutionFile = Path.Combine(candidate.FullName, "BookShop", "BookShop.sln");

            if (File.Exists(solutionFile))
            {
                return solutionFile;
            }

            candidate = candidate.Parent;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "BookShop", "BookShop.sln");
    }

    private static IEnumerable<string> ReadProjectFiles(string solutionPath)
    {
        const string projectPattern = "Project\\(\"\\{[0-9A-Fa-f-]+\\}\"\\) = \"[^\"]+\", \"([^\"]+)\"";

        foreach (string line in File.ReadLines(solutionPath))
        {
            Match match = Regex.Match(line, projectPattern);

            if (match.Success)
            {
                yield return match.Groups[1].Value;
            }
        }
    }
}