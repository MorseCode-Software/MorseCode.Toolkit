using JetBrains.Annotations;

namespace MorseCode.StagedConstruction;

/// <summary>
///     The callback that a subclass gives to a stage of its base. The stage calls
///     it with the output of the stage and with the handle for the subsequent
///     step.
/// </summary>
/// <param name="output">The values that the stage makes for the subclass.</param>
/// <param name="next">
///     The handle for the subsequent step. It is a
///     <see cref="Stage{TInput,TOutput,TNext}" /> when the base has more stages.
///     It is a <see cref="Constructor{TBaseValues}" /> after the last stage.
/// </param>
/// <typeparam name="TOutput">
///     The type of the values that the stage makes for the
///     subclass.
/// </typeparam>
/// <typeparam name="TNext">The type of the handle for the subsequent step.</typeparam>
/// <typeparam name="TResult">
///     The type of the value in the <see cref="Constructed{TResult}" /> that the
///     callback returns. Usually, it is the subclass. The first stage of the base
///     reads the value and gives it to its caller without a change.
/// </typeparam>
/// <returns>
///     The <see cref="Constructed{TResult}" /> that the subclass gets from
///     <paramref name="next" />. <c>Construct</c> makes it after the last stage.
///     <c>Advance</c> on a stage gives it back without a change. Only
///     <c>Construct</c> makes one, thus the compiler checks that the callback
///     calls it.
/// </returns>
[PublicAPI]
public delegate Constructed<TResult> StageContinuation<in TOutput, in TNext, TResult>(TOutput output, TNext next);
