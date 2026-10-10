using DotnetSlnxItems.Cli.Tests.TestHelpers;

namespace DotnetSlnxItems.Cli.Tests;

/// <summary>
/// Smoke test that exercises the CliRunner apphost discovery path
/// before any of the spec scenarios run. If this test fails with an
/// <see cref="InvalidOperationException"/>, the message lists every
/// path searched; the fix is a single <c>dotnet build src/DotnetSlnxItems.Cli</c>.
/// </summary>
public class CliRunnerSanityTests
{
    [Fact]
    public void Sanity_Run_WithVersionFlag_ReturnsZeroAndPrintsVersion()
    {
        CliResult result;
        try
        {
            result = CliRunner.Run("--version");
        }
        catch (InvalidOperationException ex)
        {
            Assert.Fail(ex.Message);
            return;
        }

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("slnx-items", result.StdOut);
    }
}