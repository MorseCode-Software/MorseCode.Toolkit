using System;

namespace MorseCode.StagedConstruction;

/// <summary>
///     The handle that the default <c>SelectCore</c> method of <see cref="Constructor{TBaseValues}" />
///     makes.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of Constructor does not say which method makes this handle.
internal sealed class SelectedConstruct<TSourceValues, TBaseValues>(
    in Constructor<TSourceValues> source,
    in Func<TSourceValues, TBaseValues> selector)
    : Constructor<TBaseValues>
{
    private readonly Constructor<TSourceValues> source = source;
    private readonly Func<TSourceValues, TBaseValues> selector = selector;

    // Select claimed the source when it made this handle. Thus, this handle is the only one that can give
    // the values of the source to a constructor, and it calls the core of the source directly.
    protected override TResult ConstructCore<TResult>(Func<TBaseValues, TResult> constructor) =>
        this.source.ConstructAfterSelect(
            constructor: sourceValues => constructor(arg: this.selector(arg: sourceValues)));
}
