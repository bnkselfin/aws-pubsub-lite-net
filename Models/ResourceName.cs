using System.Text;
using System.Text.RegularExpressions;
using AwsPubSubLite.Errors;

namespace AwsPubSubLite.Models;

public sealed partial class ResourceName
{
    private readonly string _baseName;

    public ResourceType Type { get; }
    public ResourceNamingOptions NamingOptions { get; }

    private ResourceName(string baseName, ResourceType type, ResourceNamingOptions namingOptions)
    {
        _baseName = baseName;
        Type = type;
        NamingOptions = namingOptions;
    }

    public static ResourceName Create(
        string baseName, ResourceType type, ResourceNamingOptions namingOptions)
    {
        var name = new ResourceName(baseName, type, namingOptions);
        var resolved = name.ToString();

        var maxLen = type switch
        {
            ResourceType.Topic => 256,
            ResourceType.Queue => 80,
            _ => 256
        };

        ValidateAwsResourceName(resolved, maxLen);
        return name;
    }

    public void ValidateResourceType(ResourceType target)
    {
        if (Type != target)
            throw new ResourceException(
                ToString(), target.ToString(),
                $"Invalid resource type '{Type}' provided for '{this}' (required: '{target}')");
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(NamingOptions.Prefix))
        {
            sb.Append(NamingOptions.Prefix);
            if (NamingOptions.PrefixSeparator.HasValue)
                sb.Append(NamingOptions.PrefixSeparator.Value.ToChar());
        }

        sb.Append(_baseName);

        if (!string.IsNullOrEmpty(NamingOptions.Suffix))
        {
            if (NamingOptions.SuffixSeparator.HasValue)
                sb.Append(NamingOptions.SuffixSeparator.Value.ToChar());
            sb.Append(NamingOptions.Suffix);
        }

        return sb.ToString();
    }

    private static void ValidateAwsResourceName(string name, int maxLen)
    {
        if (string.IsNullOrEmpty(name))
            throw ResourceNameException.Empty();

        if (name.Length > maxLen)
            throw ResourceNameException.TooLong(name, maxLen);

        if (!AwsNamePattern().IsMatch(name))
            throw ResourceNameException.InvalidPattern(name);
    }

    [GeneratedRegex(@"^[a-zA-Z0-9_-]+$")]
    private static partial Regex AwsNamePattern();
}
