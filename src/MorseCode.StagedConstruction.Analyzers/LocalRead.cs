using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace MorseCode.StagedConstruction.Analyzers;

/// <summary>
///     The values that one read of a local variable can get.
/// </summary>
/// <param name="values">The values.</param>
/// <param name="isComplete">True if the values are all of the values.</param>
internal sealed class LocalRead(in ImmutableArray<IOperation> values, in bool isComplete)
{
    /// <summary>
    ///     Gets the values, without conversions, in the tree of the continuation.
    ///     None is a local variable or a conditional.
    /// </summary>
    public ImmutableArray<IOperation> Values { get; } = values;

    /// <summary>
    ///     Gets a value that is true if the values are all of the values. It is
    ///     false if the analyzer does not follow the variable. It is also false if
    ///     a write with a value that the analyzer cannot know gets to the read.
    /// </summary>
    public bool IsComplete { get; } = isComplete;
}
