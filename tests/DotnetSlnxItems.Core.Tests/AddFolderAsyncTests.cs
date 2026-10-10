using DotnetSlnxItems.Core;

namespace DotnetSlnxItems.Core.Tests;

/// <summary>
/// Tests for the per-folder dedupe contract of
/// <see cref="SolutionItemsService.AddFolderAsync"/>. The service
/// must walk every ancestor of the input path and emit one
/// <see cref="FolderAddResult"/> per ancestor; the returned
/// <see cref="FolderAddResult.Created"/> flag distinguishes a
/// freshly-created folder from an idempotent no-op.
/// </summary>
public class AddFolderAsyncTests
{
    [Fact]
    public async Task AddFolderAsync_NestedPath_FreshSolution_Returns3Created()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);

            IReadOnlyList<FolderAddResult> results = await service.AddFolderAsync("a/b/c");

            Assert.Equal(3, results.Count);
            Assert.All(results, r => Assert.True(r.Created));
            Assert.Equal("/a/", results[0].Path);
            Assert.Equal("/a/b/", results[1].Path);
            Assert.Equal("/a/b/c/", results[2].Path);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task AddFolderAsync_NestedPath_ReRun_Returns3NotCreated()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            await service.AddFolderAsync("a/b/c");
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<FolderAddResult> results = await reopened.AddFolderAsync("a/b/c");

            Assert.Equal(3, results.Count);
            Assert.All(results, r => Assert.False(r.Created));
            Assert.Equal("/a/", results[0].Path);
            Assert.Equal("/a/b/", results[1].Path);
            Assert.Equal("/a/b/c/", results[2].Path);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task AddFolderAsync_NestedPath_MixedAncestors_ReturnsMixedCreated()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            // Pre-create only /a/b/ so /a/ is new and /a/b/c/ is new.
            await service.AddFolderAsync("a/b");
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<FolderAddResult> results = await reopened.AddFolderAsync("a/b/c");

            Assert.Equal(3, results.Count);
            Assert.True(results[0].Created);
            Assert.Equal("/a/", results[0].Path);
            Assert.False(results[1].Created);
            Assert.Equal("/a/b/", results[1].Path);
            Assert.True(results[2].Created);
            Assert.Equal("/a/b/c/", results[2].Path);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task AddFolderAsync_TopLevel_FreshSolution_Returns1Created()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);

            IReadOnlyList<FolderAddResult> results = await service.AddFolderAsync("Build");

            Assert.Single(results);
            Assert.True(results[0].Created);
            Assert.Equal("/Build/", results[0].Path);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("///")]
    public async Task AddFolderAsync_InvalidPath_ThrowsInvalidFolderPath(string input)
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            var ex = await Assert.ThrowsAsync<SolutionItemsException>(
                async () => await service.AddFolderAsync(input));
            Assert.Equal(SolutionItemsErrorKind.InvalidFolderPath, ex.Kind);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }
}