using DotnetSlnxItems.Core;

namespace DotnetSlnxItems.Core.Tests;

/// <summary>
/// Tests for the cascade-aware contract of
/// <see cref="SolutionItemsService.RemoveFolderAsync"/>. The
/// service must return one <see cref="FolderRemoveResult"/> per
/// folder removed: the targeted top-level folder with
/// <c>Cascaded == false</c>, and every descendant with
/// <c>Cascaded == true</c>. A pre-absent folder returns an empty
/// list rather than throwing.
/// </summary>
public class RemoveFolderAsyncTests
{
    [Fact]
    public async Task RemoveFolderAsync_TopLevelOnly_ReturnsSingleNotCascaded()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            await service.AddFolderAsync("Build");
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<FolderRemoveResult> results = await reopened.RemoveFolderAsync("Build");

            Assert.Single(results);
            Assert.Equal("/Build/", results[0].Path);
            Assert.False(results[0].Cascaded);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task RemoveFolderAsync_NestedPath_ReturnsAllAncestorsWithCascadeFlags()
    {
        // Pre-create /a/b/c/ with two deeper descendants /a/b/c/d/ and
        // /a/b/c/e/ so that RemoveFolderAsync on /a/b/c/ finds two
        // descendants to cascade. The persistence library's
        // RemoveFolder cascades by design; the cascade walk in our
        // service reports the same set the library removes.
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            await service.AddFolderAsync("a/b/c/d");
            await service.AddFolderAsync("a/b/c/e");
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<FolderRemoveResult> results = await reopened.RemoveFolderAsync("a/b/c");

            // Top-level first, then descendants in Ordinal order
            // (shorter prefix before longer).
            Assert.Equal(3, results.Count);
            Assert.Equal("/a/b/c/", results[0].Path);
            Assert.False(results[0].Cascaded);
            Assert.Equal("/a/b/c/d/", results[1].Path);
            Assert.True(results[1].Cascaded);
            Assert.Equal("/a/b/c/e/", results[2].Path);
            Assert.True(results[2].Cascaded);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task RemoveFolderAsync_PreAbsent_ReturnsEmptyList()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<FolderRemoveResult> results = await service.RemoveFolderAsync("Build");

            Assert.Empty(results);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }
}