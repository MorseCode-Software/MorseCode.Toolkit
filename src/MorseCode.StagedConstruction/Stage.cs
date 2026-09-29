using System;
using JetBrains.Annotations;

namespace MorseCode.StagedConstruction;

/// <summary>
///     Makes the usual <see cref="IStage{TInput,TOutput,TNext}" /> handles.
/// </summary>
[PublicAPI]
public static class Stage
{
    /// <summary>
    ///     Makes a stage that calls <paramref name="body" /> with the input of the subclass. Then, the
    ///     stage gives the output and the handle that <paramref name="body" /> returns to the subclass.
    /// </summary>
    /// <remarks>
    ///     This stage does not do work after the subclass continues. A stage that must do work around
    ///     the subsequent steps, for example in a transaction, implements
    ///     <see cref="IStage{TInput,TOutput,TNext}" /> directly.
    /// </remarks>
    /// <param name="body">
    ///     The work of the stage. It gets the values of the previous stages from the closure that
    ///     contains it.
    /// </param>
    /// <typeparam name="TInput">The type of the values that the subclass gives to the stage.</typeparam>
    /// <typeparam name="TOutput">The type of the values that the stage makes for the subclass.</typeparam>
    /// <typeparam name="TNext">The type of the handle for the subsequent step.</typeparam>
    /// <returns>The stage.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="body" /> is null.</exception>
    public static IStage<TInput, TOutput, TNext> From<TInput, TOutput, TNext>(
        Func<TInput, (TOutput Output, TNext Next)> body)
    {
        // The null check occurs here, where the caller is on the stack, and not subsequently in Advance.
        ArgumentNullException.ThrowIfNull(argument: body);

        return new StageFromBody<TInput, TOutput, TNext>(body: body);
    }
}
