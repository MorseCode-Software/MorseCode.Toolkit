using System;

namespace MorseCode.StagedConstruction;

/// <summary>
///     The handle that the default <c>SelectCore</c> method of <see cref="Constructor{TBaseValues}" />
///     makes.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of Constructor does not say which method makes this handle.
internal sealed class SelectedConstruct<TSourceValues, TBaseValues>(
    Constructor<TSourceValues> source,
    Func<TSourceValues, TBaseValues> selector)
    : Constructor<TBaseValues>
{
    private Constructor<TSourceValues> Source { get; } = source;

    private Func<TSourceValues, TBaseValues> Selector { get; } = selector;

    // Select claimed the source when it made this handle. Thus, this handle is the only one that can give
    // the values of the source to a constructor, and it calls the core of the source directly.
    protected override TResult ConstructCore<TResult>(Func<TBaseValues, TResult> constructor) =>
        this.Source.ConstructAfterSelect(constructor: sourceValues => constructor(arg: this.Selector(arg: sourceValues)));
}
