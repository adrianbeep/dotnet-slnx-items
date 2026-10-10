namespace DotnetSlnxItems.Core;

/// <summary>
/// Result of a single folder touch in
/// <see cref="SolutionItemsService.AddFolderAsync"/>. The service
/// walks every ancestor of the input path and emits one
/// <see cref="FolderAddResult"/> per ancestor. <see cref="Path"/>
/// is the normalized form (e.g., "/Build/", "/a/", "/a/b/",
/// "/a/b/c/"). <see cref="Created"/> is <c>true</c> if the
/// persistence library created the folder for the first time,
/// or <c>false</c> if the folder already existed.
/// </summary>
public sealed record FolderAddResult(string Path, bool Created);