using System;
using JetBrains.Annotations;

namespace MorseCode.StagedConstruction;

/// <summary>
///     The handle for a stage of a base after its first stage. The subclass gives
///     input to the stage, and the stage gives its output and the handle for the
///     subsequent step back to the subclass.
/// </summary>
/// <remarks>
///     <para>
///         The first stage of a base is its static <c>CreateBase</c> method. Each
///         subsequent stage is a handle of this type. The handle for the step
///         after the last stage is a <see cref="Constructor{TBaseValues}" />.
///         Thus, the type of the first
///         <see cref="StageContinuation{TOutput,TNext,TResult}" /> shows all of
///         the stages in their sequence.
///     </para>
///     <para>
///         The subclass must call <see cref="Advance{TResult}" /> one time only.
///         The stage that <see cref="Stage.From{TInput,TOutput,TNext}" /> makes
///         fails at a second call, also when two threads call at the same time.
///         The first call uses the stage, also when the stage fails.
///     </para>
///     <para>
///         A base that needs a stage of its own derives from this class and
///         implements <see cref="AdvanceCore{TResult}" />. It does not check for a
///         second call.
///         <see cref="Advance{TResult}" /> claims the stage before it calls
///         <see cref="AdvanceCore{TResult}" />. Thus, a second call fails, also
///         when two threads call at the same time, and also when the first call
///         fails.
///     </para>
/// </remarks>
/// <typeparam name="TInput">
///     The type of the values that the subclass gives to the
///     stage.
/// </typeparam>
/// <typeparam name="TOutput">
///     The type of the values that the stage makes for the
///     subclass.
/// </typeparam>
/// <typeparam name="TNext">
///     The type of the handle for the subsequent step: a different
///     <see cref="Stage{TInput,TOutput,TNext}" />, or a
///     <see cref="Constructor{TBaseValues}" /> after the last stage.
/// </typeparam>
[PublicAPI]
public abstract class Stage<TInput, TOutput, TNext>
{
    private string? usedBy;

    /// <summary>
    ///     Does the work of the stage with <paramref name="input" />. Then, calls
    ///     <paramref name="continuation" /> with the output of the stage and with the
    ///     handle for the subsequent step.
    /// </summary>
    /// <remarks>
    ///     This method claims the stage, and then calls
    ///     <see cref="AdvanceCore{TResult}" />. The claim occurs first. Thus, a call
    ///     that fails also uses the stage, and a second call does not run the stage
    ///     again. A null <paramref name="continuation" /> fails before the claim, and
    ///     does not use the stage.
    /// </remarks>
    /// <param name="input">The values that the subclass gives to the stage.</param>
    /// <param name="continuation">
    ///     The callback that receives the output and the
    ///     handle.
    /// </param>
    /// <typeparam name="TResult">
    ///     The type that <paramref name="continuation" /> returns. The compiler gets
    ///     it from the lambda, thus the caller does not write it.
    /// </typeparam>
    /// <returns>The value that <paramref name="continuation" /> returns.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="continuation" /> is
    ///     null.
    /// </exception>
    /// <exception cref="InvalidOperationException">A caller used this stage before.</exception>
    public TResult Advance<TResult>(TInput input, StageContinuation<TOutput, TNext, TResult> continuation)
    {
        // The null check occurs before the claim. Thus, a null continuation does not use the stage. The input
        // is not checked: its type is the choice of the base, and null can be a valid input.
        if (continuation is null)
        {
            throw new ArgumentNullException(paramName: nameof(continuation));
        }

        SingleUse.Claim(usedBy: ref this.usedBy, member: nameof(this.Advance));

        return this.AdvanceCore(input: input, continuation: continuation);
    }

    /// <summary>
    ///     Does the work of the stage with <paramref name="input" />. Then, calls
    ///     <paramref name="continuation" /> with the output of the stage and with the
    ///     handle for the subsequent step.
    /// </summary>
    /// <remarks>
    ///     <see cref="Advance{TResult}" /> calls this method one time only, with a
    ///     <paramref name="continuation" /> that is not null.
    /// </remarks>
    /// <param name="input">The values that the subclass gives to the stage.</param>
    /// <param name="continuation">
    ///     The callback that receives the output and the
    ///     handle.
    /// </param>
    /// <typeparam name="TResult">
    ///     The type that <paramref name="continuation" />
    ///     returns.
    /// </typeparam>
    /// <returns>The value that <paramref name="continuation" /> returns.</returns>
    protected abstract TResult AdvanceCore<TResult>(
        TInput input,
        StageContinuation<TOutput, TNext, TResult> continuation);
}
