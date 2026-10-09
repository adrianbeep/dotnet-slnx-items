using DotnetSlnxItems.Core;

namespace DotnetSlnxItems.Core.Tests;

public class MutationTests
{
    [Fact]
    public async Task AddFilesAsync_SingleFileInDefaultFolder_AddsToSolutionItems()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            await service.AddFilesAsync(new[] { "test-item.config" });
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await reopened.ListAsync();

            Assert.Contains(items, i =>
                i.Kind == SolutionItemKind.File && i.Path == "test-item.config");
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task AddFilesAsync_MultipleFiles_AddsAll()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            await service.AddFilesAsync(new[] { "a.txt", "b.txt", "c.txt" });
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await reopened.ListAsync();

            Assert.Contains(items, i => i.Kind == SolutionItemKind.File && i.Path == "a.txt");
            Assert.Contains(items, i => i.Kind == SolutionItemKind.File && i.Path == "b.txt");
            Assert.Contains(items, i => i.Kind == SolutionItemKind.File && i.Path == "c.txt");
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task AddFilesAsync_CustomTargetFolder_AddsThere()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            await service.AddFilesAsync(new[] { "build.props" }, targetFolder: "Build");
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await reopened.ListAsync();

            Assert.Contains(items, i =>
                i.Kind == SolutionItemKind.Folder && i.Path == "/Build/");
            Assert.Contains(items, i =>
                i.Kind == SolutionItemKind.File && i.Path == "build.props");
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task AddFilesAsync_DuplicateFile_IsNoOp()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            await service.AddFilesAsync(new[] { "dup.config" });
            await service.AddFilesAsync(new[] { "dup.config" });
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await reopened.ListAsync();

            int matches = 0;
            foreach (SolutionItemInfo item in items)
            {
                if (item.Kind == SolutionItemKind.File && item.Path == "dup.config")
                {
                    matches++;
                }
            }
            Assert.Equal(1, matches);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task AddFilesAsync_FileOutsideSolutionDirectory_ThrowsInvalidFilePath()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            string outside = Path.Combine(Path.GetDirectoryName(path)!, "..", "outside.config");
            var ex = await Assert.ThrowsAsync<SolutionItemsException>(
                async () => await service.AddFilesAsync(new[] { outside }));
            Assert.Equal(SolutionItemsErrorKind.InvalidFilePath, ex.Kind);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task AddFolderAsync_NestedPath_CreatesIntermediateFolders()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            await service.AddFolderAsync("a/b/c");
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await reopened.ListAsync();

            Assert.Contains(items, i => i.Kind == SolutionItemKind.Folder && i.Path == "/a/b/c/");
            // intermediate folders should also be present (tree walk)
            Assert.Contains(items, i => i.Kind == SolutionItemKind.Folder && i.Path == "/a/");
            Assert.Contains(items, i => i.Kind == SolutionItemKind.Folder && i.Path == "/a/b/");
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task RemoveFilesAsync_ExistingFile_RemovesIt()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            await service.AddFilesAsync(new[] { "to-remove.config" });
            await service.RemoveFilesAsync(new[] { "to-remove.config" });
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await reopened.ListAsync();

            Assert.DoesNotContain(items, i =>
                i.Kind == SolutionItemKind.File && i.Path == "to-remove.config");
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task RemoveFolderAsync_ExistingFolder_RemovesIt()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            await service.AddFolderAsync("ToRemove");
            await service.RemoveFolderAsync("ToRemove");
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await reopened.ListAsync();

            Assert.DoesNotContain(items, i =>
                i.Kind == SolutionItemKind.Folder && i.Path == "/ToRemove/");
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }
}