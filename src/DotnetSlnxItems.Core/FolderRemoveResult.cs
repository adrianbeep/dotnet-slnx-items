namespace DotnetSlnxItems.Core;

/// <summary>
/// Result of a single folder removal in
/// <see cref="SolutionItemsService.RemoveFolderAsync"/>. The service
/// walks every descendant of the input path and emits one
/// <see cref="FolderRemoveResult"/> per removed folder. <see cref="Path"/>
/// is the normalized form (e.g., "/Build/", "/Build/docs/"). The
/// top-level entry has <see cref="Cascaded"/> = <c>false</c>; every
/// descendant removed as a side-effect of the top-level removal
/// has <c>true</c>. A pre-absent top-level returns an empty list.
/// </summary>
public sealed record FolderRemoveResult(string Path, bool Cascaded);