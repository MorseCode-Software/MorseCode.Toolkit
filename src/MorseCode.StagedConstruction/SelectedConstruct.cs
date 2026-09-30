using System;

namespace MorseCode.StagedConstruction;

/// <summary>
///     The handle that the <c>Select</c> method of <see cref="Constructor{TBaseValues}" /> makes.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of Constructor does not say which method makes this handle.
internal sealed class SelectedConstruct<TSourceValues, TBaseValues>(
    Constructor<TSourceValues> source,
    Func<TSourceValues, TBaseValues> selector)
    : Constructor<TBaseValues>
{
    private Constructor<TSourceValues> Source { get; } = source;

    private Func<TSourceValues, TBaseValues> Selector { get; } = selector;

    protected override TResult ConstructCore<TResult>(Func<TBaseValues, TResult> constructor) =>
        this.Source.Construct(constructor: sourceValues => constructor(arg: this.Selector(arg: sourceValues)));
}
