using JetBrains.Annotations;

namespace MorseCode.StagedConstruction;

/// <summary>
///     The base class for a stage that implements <see cref="IStage{TInput,TOutput,TNext}" />. It makes
///     sure that a caller uses the stage one time only.
/// </summary>
/// <remarks>
///     A class that derives from this class implements <see cref="AdvanceCore{TResult}" />, and it does
///     not check for a second call. The <see cref="Advance{TResult}" /> method claims the stage before it
///     calls <see cref="AdvanceCore{TResult}" />. Thus, a second call fails, also when two threads call
///     at the same time, and also when the first call fails.
/// </remarks>
/// <typeparam name="TInput">The type of the values that the subclass gives to the stage.</typeparam>
/// <typeparam name="TOutput">The type of the values that the stage makes for the subclass.</typeparam>
/// <typeparam name="TNext">The type of the handle for the subsequent step.</typeparam>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - The summary of IStage does not say that this class checks for a second call.
public abstract class StageBase<TInput, TOutput, TNext> : IStage<TInput, TOutput, TNext>
{
    private int used;

    /// <inheritdoc />
    /// <remarks>
    ///     This method claims the stage, and then calls <see cref="AdvanceCore{TResult}" />. The claim
    ///     occurs first. Thus, a call that fails also uses the stage, and a second call does not run the
    ///     stage again.
    /// </remarks>
    public TResult Advance<TResult>(TInput input, StageContinuation<TOutput, TNext, TResult> continuation)
    {
        SingleUse.Claim(used: ref this.used, member: nameof(this.Advance));

        return this.AdvanceCore(input: input, continuation: continuation);
    }

    /// <summary>
    ///     Does the work of the stage with <paramref name="input" />. Then, calls
    ///     <paramref name="continuation" /> with the output of the stage and with the handle for the
    ///     subsequent step.
    /// </summary>
    /// <remarks>
    ///     <see cref="Advance{TResult}" /> calls this method one time only.
    /// </remarks>
    /// <param name="input">The values that the subclass gives to the stage.</param>
    /// <param name="continuation">The callback that receives the output and the handle.</param>
    /// <typeparam name="TResult">The type that <paramref name="continuation" /> returns.</typeparam>
    /// <returns>The value that <paramref name="continuation" /> returns.</returns>
    protected abstract TResult AdvanceCore<TResult>(
        TInput input,
        StageContinuation<TOutput, TNext, TResult> continuation);
}
