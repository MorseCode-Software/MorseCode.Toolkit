using System;

namespace MorseCode.StagedConstruction;

/// <summary>
///     The handle that <see cref="StagedConstruction.Construct.From{TBaseValues}" /> makes.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of IConstruct does not say which method makes this handle.
internal sealed class ValuesConstruct<TBaseValues>(TBaseValues values) : IConstruct<TBaseValues>
{
    private TBaseValues Values { get; } = values;

    public TResult Construct<TResult>(Func<TBaseValues, TResult> constructor) => constructor(arg: this.Values);
}
