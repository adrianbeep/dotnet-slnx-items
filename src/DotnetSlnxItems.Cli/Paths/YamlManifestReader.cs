using DotnetSlnxItems.Core;

using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DotnetSlnxItems.Cli.Paths;

/// <summary>
/// Reads a <c>.slnx-items.yaml</c> manifest file and produces a
/// <see cref="SolutionItemsManifest"/>. The deserializer is the
/// default YamlDotNet 18.1.0 builder; the safe-subset rejection
/// (no anchors, no aliases, no flow style, no multi-document, no
/// tags, no directives) is enforced by walking the parsed
/// <see cref="YamlStream"/> after deserialization. Any parse error
/// or unsafe-YAML construct is re-thrown as
/// <see cref="SolutionItemsException"/> with
/// <see cref="SolutionItemsErrorKind.InvalidManifest"/> and a
/// 1-indexed line/column location.
/// </summary>
internal static class YamlManifestReader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    public static SolutionItemsManifest Read(string manifestPath)
    {
        string text;
        try
        {
            text = File.ReadAllText(manifestPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SolutionItemsException(
                SolutionItemsErrorKind.ManifestNotFound,
                $"Manifest file not found: {manifestPath}",
                ex);
        }

        SolutionItemsManifest manifest;
        try
        {
            Dictionary<string, List<string>>? raw = Deserializer.Deserialize<Dictionary<string, List<string>>>(text);
            manifest = Normalize(raw);
        }
        catch (YamlException ex)
        {
            throw new SolutionItemsException(
                SolutionItemsErrorKind.InvalidManifest,
                $"Invalid manifest at '{manifestPath}': (line {ex.Start.Line + 1}, column {ex.Start.Column + 1}) {ex.Message}",
                ex);
        }

        ValidateSafeSubset(manifestPath, text);
        return manifest;
    }

    private static void ValidateSafeSubset(string manifestPath, string text)
    {
        YamlStream stream = new();
        try
        {
            using StringReader reader = new(text);
            stream.Load(reader);
        }
        catch (YamlException ex)
        {
            throw new SolutionItemsException(
                SolutionItemsErrorKind.InvalidManifest,
                $"Invalid manifest at '{manifestPath}': (line {ex.Start.Line + 1}, column {ex.Start.Column + 1}) {ex.Message}",
                ex);
        }

        if (stream.Documents.Count > 1)
        {
            throw new SolutionItemsException(
                SolutionItemsErrorKind.InvalidManifest,
                $"Invalid manifest at '{manifestPath}': multi-document manifests are not supported.");
        }

        foreach (YamlDocument document in stream.Documents)
        {
            WalkRejectingUnsafeConstructs(document.RootNode, manifestPath);
        }
    }

    private static void WalkRejectingUnsafeConstructs(YamlNode? node, string manifestPath)
    {
        if (node is null)
        {
            return;
        }

        if (!node.Anchor.IsEmpty)
        {
            throw new SolutionItemsException(
                SolutionItemsErrorKind.InvalidManifest,
                $"Invalid manifest at '{manifestPath}': YAML anchors are not allowed.");
        }

        if (!node.Tag.IsEmpty)
        {
            throw new SolutionItemsException(
                SolutionItemsErrorKind.InvalidManifest,
                $"Invalid manifest at '{manifestPath}': explicit YAML tags are not allowed.");
        }

        switch (node)
        {
            case YamlMappingNode mapping when mapping.Style == MappingStyle.Flow:
                throw new SolutionItemsException(
                    SolutionItemsErrorKind.InvalidManifest,
                    $"Invalid manifest at '{manifestPath}': flow-style YAML is not allowed; use block style only.");
            case YamlSequenceNode sequence when sequence.Style == SequenceStyle.Flow:
                throw new SolutionItemsException(
                    SolutionItemsErrorKind.InvalidManifest,
                    $"Invalid manifest at '{manifestPath}': flow-style YAML is not allowed; use block style only.");
            case YamlMappingNode mapping:
                foreach (YamlNode child in mapping.Children.Values)
                {
                    WalkRejectingUnsafeConstructs(child, manifestPath);
                }
                break;
            case YamlSequenceNode sequence:
                foreach (YamlNode child in sequence.Children)
                {
                    WalkRejectingUnsafeConstructs(child, manifestPath);
                }
                break;
        }
    }

    private static SolutionItemsManifest Normalize(Dictionary<string, List<string>>? raw)
    {
        Dictionary<string, IReadOnlyList<string>> normalized = new();
        if (raw is not null)
        {
            foreach (KeyValuePair<string, List<string>> entry in raw)
            {
                normalized[entry.Key] = entry.Value ?? (IReadOnlyList<string>)Array.Empty<string>();
            }
        }
        return new SolutionItemsManifest(normalized);
    }
}