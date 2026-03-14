namespace AwsPubSubLite.Models;

public enum SeparatorSymbol
{
    Underscore,
    Hyphen
}

public static class SeparatorSymbolExtensions
{
    public static char ToChar(this SeparatorSymbol symbol) => symbol switch
    {
        SeparatorSymbol.Underscore => '_',
        SeparatorSymbol.Hyphen => '-',
        _ => '-'
    };
}
