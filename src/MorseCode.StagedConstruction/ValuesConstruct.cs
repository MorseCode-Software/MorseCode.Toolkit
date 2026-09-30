using System;

namespace MorseCode.StagedConstruction;

/// <summary>
///     The handle that <see cref="StagedConstruction.Constructor.From{TBaseValues}" /> makes.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of Constructor does not say which method makes this handle.
internal sealed class ValuesConstruct<TBaseValues>(TBaseValues values) : Constructor<TBaseValues>
{
    private TBaseValues Values { get; } = values;

    protected override TResult ConstructCore<TResult>(Func<TBaseValues, TResult> constructor) =>
        constructor(arg: this.Values);
}
