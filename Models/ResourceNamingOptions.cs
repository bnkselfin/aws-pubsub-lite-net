namespace AwsPubSubLite.Models;

public sealed record ResourceNamingOptions
{
    public string? Prefix { get; init; }
    public string? Suffix { get; init; }
    public SeparatorSymbol? PrefixSeparator { get; init; }
    public SeparatorSymbol? SuffixSeparator { get; init; }
}
