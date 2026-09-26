using System;

namespace MorseCode.StagedConstruction;

/// <summary>
///     The handle that <see cref="IConstruct{TBaseValues}.Select{TSelectedValues}" /> makes.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of IConstruct does not say which method makes this handle.
internal sealed class SelectedConstruct<TSourceValues, TBaseValues>(
    IConstruct<TSourceValues> source,
    Func<TSourceValues, TBaseValues> selector)
    : IConstruct<TBaseValues>
{
    private IConstruct<TSourceValues> Source { get; } = source;

    private Func<TSourceValues, TBaseValues> Selector { get; } = selector;

    public TResult Construct<TResult>(Func<TBaseValues, TResult> constructor) =>
        this.Source.Construct(constructor: sourceValues => constructor(arg: this.Selector(arg: sourceValues)));
}
