namespace AwsPubSubLite.Models;

public enum BackoffFunction
{
    Linear,
    Arithmetic,
    Geometric,
    Exponential
}

public static class BackoffFunctionExtensions
{
    public static string ToAwsString(this BackoffFunction fn) => fn switch
    {
        BackoffFunction.Linear => "linear",
        BackoffFunction.Arithmetic => "arithmetic",
        BackoffFunction.Geometric => "geometric",
        BackoffFunction.Exponential => "exponential",
        _ => "linear"
    };
}
