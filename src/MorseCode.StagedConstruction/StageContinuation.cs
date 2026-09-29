using JetBrains.Annotations;

namespace MorseCode.StagedConstruction;

/// <summary>
///     The callback that a subclass gives to a stage of its base. The stage calls it with the output
///     of the stage and with the handle for the subsequent step.
/// </summary>
/// <param name="output">The values that the stage makes for the subclass.</param>
/// <param name="next">
///     The handle for the subsequent step. It is an <see cref="IStage{TInput,TOutput,TNext}" /> when
///     the base has more stages. It is an <see cref="IConstruct{TBaseValues}" /> after the last stage.
/// </param>
/// <typeparam name="TOutput">The type of the values that the stage makes for the subclass.</typeparam>
/// <typeparam name="TNext">The type of the handle for the subsequent step.</typeparam>
/// <typeparam name="TResult">
///     The type that the callback returns. Usually, it is the subclass. The base gives this value back
///     to its caller without a change.
/// </typeparam>
/// <returns>The value that the subclass gets from <paramref name="next" />.</returns>
[PublicAPI]
public delegate TResult StageContinuation<in TOutput, in TNext, out TResult>(TOutput output, TNext next);
