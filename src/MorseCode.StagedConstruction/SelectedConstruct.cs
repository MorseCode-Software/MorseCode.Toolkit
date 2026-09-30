using System;

namespace MorseCode.StagedConstruction;

/// <summary>
///     The handle that the <c>Select</c> extension method makes.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of IConstruct does not say which method makes this handle.
internal sealed class SelectedConstruct<TSourceValues, TBaseValues>(
    IConstruct<TSourceValues> source,
    Func<TSourceValues, TBaseValues> selector)
    : ConstructBase<TBaseValues>
{
    private IConstruct<TSourceValues> Source { get; } = source;

    private Func<TSourceValues, TBaseValues> Selector { get; } = selector;

    // This handle makes its own claim, in the base class. The source can be a handle that a base
    // implements, and such a handle does not always make the claim.
    protected override TResult ConstructCore<TResult>(Func<TBaseValues, TResult> constructor) =>
        this.Source.Construct(constructor: sourceValues => constructor(arg: this.Selector(arg: sourceValues)));
}
