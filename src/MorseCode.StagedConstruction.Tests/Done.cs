namespace MorseCode.StagedConstruction.Tests;

/// <summary>
///     Makes a <see cref="Constructed{TResult}" /> for a test whose continuation
///     has no handle to construct with, through the one path that makes one.
/// </summary>
internal static class Done
{
    public static Constructed<T> Of<T>(T value) =>
        Constructor.From(values: value).Construct(constructor: static v => v);
}
