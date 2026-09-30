using System;

namespace MorseCode.StagedConstruction;

/// <summary>
///     The stage that <see cref="Stage.From{TInput,TOutput,TNext}" /> makes.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of Stage does not say which method makes this stage.
internal sealed class StageFromBody<TInput, TOutput, TNext>(Func<TInput, (TOutput Output, TNext Next)> body)
    : Stage<TInput, TOutput, TNext>
{
    private Func<TInput, (TOutput Output, TNext Next)> Body { get; } = body;

    protected override TResult AdvanceCore<TResult>(
        TInput input,
        StageContinuation<TOutput, TNext, TResult> continuation)
    {
        (TOutput output, TNext next) = this.Body(arg: input);

        return continuation(output: output, next: next);
    }
}
