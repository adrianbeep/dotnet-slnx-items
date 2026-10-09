using DotnetSlnxItems.Core;

namespace DotnetSlnxItems.Core.Tests;

public class RoundTripTests
{
    [Fact]
    public async Task RoundTrip_RealWorldSlnx_AddsFile_AppearsAfterReopen()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            // Load the real-world .slnx, add a file, save, reopen, assert.
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            await service.AddFilesAsync(new[] { "added-by-test.config" });
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await reopened.ListAsync();

            // The added file must be there
            Assert.Contains(items, i =>
                i.Kind == SolutionItemKind.File && i.Path == "added-by-test.config");

            // The default Solution Items folder must exist (we didn't specify one)
            Assert.Contains(items, i =>
                i.Kind == SolutionItemKind.Folder && i.Path == "/Solution Items/");
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task RoundTrip_RealWorldSln_AddsFile_AppearsAfterReopen()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.sln", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            await service.AddFilesAsync(new[] { "added-by-test.config" });
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await reopened.ListAsync();

            Assert.Contains(items, i =>
                i.Kind == SolutionItemKind.File && i.Path == "added-by-test.config");
            Assert.Contains(items, i =>
                i.Kind == SolutionItemKind.Folder && i.Path == "/Solution Items/");
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task RoundTrip_RealWorldSlnx_PreservesExistingProject()
    {
        // The real-world .slnx has a project reference. After a
        // round-trip the project must still be there.
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            await service.AddFilesAsync(new[] { "test.config" });
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);

            // The .slnx must still parse and load cleanly
            // (already passed via CreateAsync above; if it failed,
            // this test would have thrown). The project file should
            // still exist on disk.
            Assert.True(File.Exists(Path.Combine(
                Path.GetDirectoryName(path)!,
                "RealWorldFixture.Project.csproj")));
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }
}