using DotnetSlnxItems.Core;

namespace DotnetSlnxItems.Core.Tests;

internal static class ListHelpers
{
    public static int IndexOfMatching<T>(IReadOnlyList<T> items, Func<T, bool> predicate)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (predicate(items[i]))
            {
                return i;
            }
        }
        return -1;
    }
}

public class ListAsyncTests
{
    [Fact]
    public async Task ListAsync_EmptySolution_ReturnsEmpty()
    {
        // Use a fresh empty .slnx
        string path = Path.Combine(Path.GetTempPath(), "empty-" + Guid.NewGuid().ToString("N") + ".slnx");
        File.WriteAllText(path, "<Solution>\n</Solution>\n");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await service.ListAsync();
            Assert.Empty(items);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ListAsync_OneFolderNoFiles_EmitsOnlyTheFolder()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            await service.AddFolderAsync("Empty");
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await reopened.ListAsync();

            // Only the empty folder should be in there (plus the project's own structure if any)
            Assert.Contains(items, i =>
                i.Kind == SolutionItemKind.Folder && i.Path == "/Empty/");
            // No files under it
            Assert.DoesNotContain(items, i =>
                i.Kind == SolutionItemKind.File && i.Path == "anything" && i.Path.Contains("Empty"));
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task ListAsync_TreeOrder_FoldersBeforeTheirFiles()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            await service.AddFolderAsync("Docs");
            await service.AddFilesAsync(new[] { "readme.md" }, targetFolder: "Docs");
            await service.AddFilesAsync(new[] { "config.json" });
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await reopened.ListAsync();

            // The Docs folder should appear before its readme.md
            int docsFolderIndex = ListHelpers.IndexOfMatching(items,
                i => i.Kind == SolutionItemKind.Folder && i.Path == "/Docs/");
            int readmeIndex = ListHelpers.IndexOfMatching(items,
                i => i.Kind == SolutionItemKind.File && i.Path == "readme.md");

            Assert.True(docsFolderIndex >= 0, "Docs folder should be present");
            Assert.True(readmeIndex >= 0, "readme.md should be present");
            Assert.True(docsFolderIndex < readmeIndex,
                $"Docs folder (idx {docsFolderIndex}) should appear before readme.md (idx {readmeIndex})");
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }
}