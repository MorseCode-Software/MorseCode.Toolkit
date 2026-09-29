using JetBrains.Annotations;

namespace MorseCode.StagedConstruction;

/// <summary>
///     The handle for a stage of a base after its first stage. The subclass gives input to the stage,
///     and the stage gives its output and the handle for the subsequent step back to the subclass.
/// </summary>
/// <remarks>
///     <para>
///         The first stage of a base is its static <c>CreateBase</c> method. Each subsequent stage is a
///         handle of this type. The handle for the step after the last stage is an
///         <see cref="IConstruct{TBaseValues}" />. Thus, the type of the first
///         <see cref="StageContinuation{TOutput,TNext,TResult}" /> shows all of the stages in their
///         sequence.
///     </para>
///     <para>
///         The subclass must call <see cref="Advance{TResult}" /> one time only. The handle does not make
///         sure of this.
///     </para>
/// </remarks>
/// <typeparam name="TInput">The type of the values that the subclass gives to the stage.</typeparam>
/// <typeparam name="TOutput">The type of the values that the stage makes for the subclass.</typeparam>
/// <typeparam name="TNext">
///     The type of the handle for the subsequent step: a different <see cref="IStage{TInput,TOutput,TNext}" />,
///     or an <see cref="IConstruct{TBaseValues}" /> after the last stage.
/// </typeparam>
[PublicAPI]
public interface IStage<in TInput, out TOutput, out TNext>
{
    /// <summary>
    ///     Does the work of the stage with <paramref name="input" />. Then, calls
    ///     <paramref name="continuation" /> with the output of the stage and with the handle for the
    ///     subsequent step.
    /// </summary>
    /// <param name="input">The values that the subclass gives to the stage.</param>
    /// <param name="continuation">The callback that receives the output and the handle.</param>
    /// <typeparam name="TResult">
    ///     The type that <paramref name="continuation" /> returns. The compiler gets it from the lambda,
    ///     thus the caller does not write it.
    /// </typeparam>
    /// <returns>The value that <paramref name="continuation" /> returns.</returns>
    TResult Advance<TResult>(TInput input, StageContinuation<TOutput, TNext, TResult> continuation);
}
