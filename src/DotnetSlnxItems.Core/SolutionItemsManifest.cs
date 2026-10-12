namespace DotnetSlnxItems.Core;

/// <summary>
/// Deserialized form of a <c>.slnx-items.yaml</c> manifest. Each
/// key is a folder name (e.g., <c>&quot;Solution Items&quot;</c>,
/// <c>&quot;Build&quot;</c>, or <c>&quot;docs/infrastructure&quot;</c>)
/// and the corresponding list is the set of file paths that should
/// appear under that folder. File paths are relative to the manifest
/// file's directory and are converted to absolute paths by the CLI
/// before they reach the Core service. The schema spec lives at
/// <c>docs/design/manifest-schema.md</c>; Core does not interpret
/// the dictionary beyond deserialization.
/// </summary>
public sealed record SolutionItemsManifest(
    IReadOnlyDictionary<string, IReadOnlyList<string>> Folders);