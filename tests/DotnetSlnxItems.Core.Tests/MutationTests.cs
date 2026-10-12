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
            IReadOnlyList<FolderAddResult> results = await service.AddFolderAsync("a/b/c");
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await reopened.ListAsync();

            // Per-record dedupe: every ancestor must report Created=true on a fresh solution.
            Assert.Equal(3, results.Count);
            Assert.All(results, r => Assert.True(r.Created));
            Assert.Equal("/a/", results[0].Path);
            Assert.Equal("/a/b/", results[1].Path);
            Assert.Equal("/a/b/c/", results[2].Path);

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

    // ---- Defensive / no-op cases ----

    [Fact]
    public async Task AddFilesAsync_EmptyList_IsNoOp()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            await service.AddFilesAsync(Array.Empty<string>());
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await reopened.ListAsync();

            // The default Solution Items folder exists (created on save even with no files)
            // but it must not contain any files.
            Assert.DoesNotContain(items, i => i.Kind == SolutionItemKind.File);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task AddFilesAsync_NullList_IsNoOp()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            // The service treats null as "nothing to do" rather than throwing.
            await service.AddFilesAsync(null!);
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await reopened.ListAsync();

            Assert.DoesNotContain(items, i => i.Kind == SolutionItemKind.File);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task RemoveFilesAsync_NonExistentFile_IsNoOp()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            // File was never added — remove must be silent.
            await service.RemoveFilesAsync(new[] { "never-added.config" });
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await reopened.ListAsync();

            Assert.DoesNotContain(items, i =>
                i.Kind == SolutionItemKind.File && i.Path == "never-added.config");
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task RemoveFolderAsync_NonExistentFolder_IsNoOp()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            // Folder was never added — remove must be silent.
            await service.RemoveFolderAsync("NeverAdded");
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await reopened.ListAsync();

            Assert.DoesNotContain(items, i =>
                i.Kind == SolutionItemKind.Folder && i.Path == "/NeverAdded/");
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task AddFolderAsync_EmptyPath_ThrowsInvalidFolderPath()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            var ex = await Assert.ThrowsAsync<SolutionItemsException>(
                async () => await service.AddFolderAsync(""));
            Assert.Equal(SolutionItemsErrorKind.InvalidFolderPath, ex.Kind);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task AddFolderAsync_OnlySlashes_ThrowsInvalidFolderPath()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            var ex = await Assert.ThrowsAsync<SolutionItemsException>(
                async () => await service.AddFolderAsync("///"));
            Assert.Equal(SolutionItemsErrorKind.InvalidFolderPath, ex.Kind);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task RemoveFolderAsync_EmptyPath_ThrowsInvalidFolderPath()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            var ex = await Assert.ThrowsAsync<SolutionItemsException>(
                async () => await service.RemoveFolderAsync(""));
            Assert.Equal(SolutionItemsErrorKind.InvalidFolderPath, ex.Kind);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    // ---- Path validation (escape attempts) ----

    [Fact]
    public async Task AddFilesAsync_RelativePathWithDotDot_ThrowsInvalidFilePath()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            var ex = await Assert.ThrowsAsync<SolutionItemsException>(
                async () => await service.AddFilesAsync(new[] { "../escape.config" }));
            Assert.Equal(SolutionItemsErrorKind.InvalidFilePath, ex.Kind);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task AddFilesAsync_AbsolutePathOutsideSolutionDir_ThrowsInvalidFilePath()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            string outside = Path.Combine(Path.GetTempPath(), "outside-" + Guid.NewGuid().ToString("N") + ".config");
            File.WriteAllText(outside, "x");
            try
            {
                var ex = await Assert.ThrowsAsync<SolutionItemsException>(
                    async () => await service.AddFilesAsync(new[] { outside }));
                Assert.Equal(SolutionItemsErrorKind.InvalidFilePath, ex.Kind);
            }
            finally
            {
                File.Delete(outside);
            }
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task AddFilesAsync_ParentDirectoryPath_ThrowsInvalidFilePath()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            // The path ".." resolves to the parent of the solution dir.
            // The relative path from solution dir to that point is "..".
            var ex = await Assert.ThrowsAsync<SolutionItemsException>(
                async () => await service.AddFilesAsync(new[] { ".." }));
            Assert.Equal(SolutionItemsErrorKind.InvalidFilePath, ex.Kind);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task RemoveFilesAsync_RelativePathWithDotDot_ThrowsInvalidFilePath()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            var ex = await Assert.ThrowsAsync<SolutionItemsException>(
                async () => await service.RemoveFilesAsync(new[] { "../escape.config" }));
            Assert.Equal(SolutionItemsErrorKind.InvalidFilePath, ex.Kind);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task RemoveFilesAsync_AbsolutePathOutsideSolutionDir_ThrowsInvalidFilePath()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            string outside = Path.Combine(Path.GetTempPath(), "outside-" + Guid.NewGuid().ToString("N") + ".config");
            File.WriteAllText(outside, "x");
            try
            {
                var ex = await Assert.ThrowsAsync<SolutionItemsException>(
                    async () => await service.RemoveFilesAsync(new[] { outside }));
                Assert.Equal(SolutionItemsErrorKind.InvalidFilePath, ex.Kind);
            }
            finally
            {
                File.Delete(outside);
            }
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    // ---- requireExistingFolder (issue #14, GAP 4) ----

    [Fact]
    public async Task AddFilesAsync_RequireExistingFolder_MissingFolder_ThrowsFolderNotFound()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            var ex = await Assert.ThrowsAsync<SolutionItemsException>(
                async () => await service.AddFilesAsync(
                    new[] { "build.props" },
                    targetFolder: "Build",
                    requireExistingFolder: true));
            Assert.Equal(SolutionItemsErrorKind.FolderNotFound, ex.Kind);
            Assert.Contains("'/Build/'", ex.Message);
            Assert.Contains("was not found", ex.Message);

            // Solution must not have been mutated.
            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await reopened.ListAsync();
            Assert.DoesNotContain(items, i => i.Kind == SolutionItemKind.Folder && i.Path == "/Build/");
            Assert.DoesNotContain(items, i => i.Kind == SolutionItemKind.File && i.Path == "build.props");
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task RemoveFilesAsync_RequireExistingFolder_MissingFolder_ThrowsFolderNotFound()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            var ex = await Assert.ThrowsAsync<SolutionItemsException>(
                async () => await service.RemoveFilesAsync(
                    new[] { "missing.config" },
                    targetFolder: "Build",
                    requireExistingFolder: true));
            Assert.Equal(SolutionItemsErrorKind.FolderNotFound, ex.Kind);
            Assert.Contains("'/Build/'", ex.Message);
            Assert.Contains("was not found", ex.Message);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task AddFilesAsync_DefaultRequireExistingFolderFalse_SilentlyCreatesMissingFolder()
    {
        // MVP behavior preservation: the default `false` keeps the
        // silent auto-create that the default /Solution Items/ flow
        // and `sync` depend on.
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            await service.AddFilesAsync(new[] { "build.props" }, targetFolder: "Build");
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await reopened.ListAsync();
            Assert.Contains(items, i => i.Kind == SolutionItemKind.Folder && i.Path == "/Build/");
            Assert.Contains(items, i => i.Kind == SolutionItemKind.File && i.Path == "build.props");
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task RemoveFilesAsync_DefaultRequireExistingFolderFalse_SilentlyNoOpsOnMissingFolder()
    {
        // MVP behavior preservation: the default `false` keeps the
        // silent no-op so idempotent remove calls don't error on
        // absent folders.
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            await service.RemoveFilesAsync(new[] { "never.config" }, targetFolder: "Build");
            await service.SaveAsync();

            SolutionItemsService reopened = await SolutionItemsService.CreateAsync(path);
            IReadOnlyList<SolutionItemInfo> items = await reopened.ListAsync();
            Assert.DoesNotContain(items, i => i.Kind == SolutionItemKind.Folder && i.Path == "/Build/");
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }

    [Fact]
    public async Task RemoveFilesAsync_ParentDirectoryPath_ThrowsInvalidFilePath()
    {
        string path = FixtureHelper.CopyFixtureTo("RealWorldFixture.slnx", "RealWorldFixture.Project.csproj");
        try
        {
            SolutionItemsService service = await SolutionItemsService.CreateAsync(path);
            // The path ".." resolves to the parent of the solution dir.
            // The relative path from solution dir to that point is "..".
            var ex = await Assert.ThrowsAsync<SolutionItemsException>(
                async () => await service.RemoveFilesAsync(new[] { ".." }));
            Assert.Equal(SolutionItemsErrorKind.InvalidFilePath, ex.Kind);
        }
        finally
        {
            FixtureHelper.Cleanup(path);
        }
    }
}