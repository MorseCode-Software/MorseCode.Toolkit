using System;
using JetBrains.Annotations;

namespace MorseCode.StagedConstruction;

/// <summary>
///     The handle for the step after the last stage of a base. It gives the values of the base to the
///     constructor of the subclass.
/// </summary>
/// <remarks>
///     <para>
///         The subclass must call <see cref="Construct{TResult}" /> one time only. The handles that
///         <see cref="StagedConstruction.Construct.From{TBaseValues}" /> and
///         <c>Select</c> make fail at a second call, also when two threads call
///         at the same time. The first call uses the handle, also when the constructor fails. A base
///         that implements this interface itself must do the same.
///     </para>
///     <para>
///         A base can keep the constructor of <typeparamref name="TBaseValues" /> private. Then, this
///         handle is the only source of the values. Thus, the subclass cannot call its own constructor
///         before the base completes all of its stages.
///     </para>
/// </remarks>
/// <typeparam name="TBaseValues">
///     The type of the values of the base. The subclass keeps them for delegation, or it gives them to
///     the constructor of its base class.
/// </typeparam>
[PublicAPI]
public interface IConstruct<out TBaseValues>
{
    /// <summary>
    ///     Calls <paramref name="constructor" /> with the values of the base.
    /// </summary>
    /// <param name="constructor">The constructor of the subclass, or a function that calls it.</param>
    /// <typeparam name="TResult">
    ///     The type that <paramref name="constructor" /> returns. The compiler gets it from the lambda,
    ///     thus the caller does not write it.
    /// </typeparam>
    /// <returns>The value that <paramref name="constructor" /> returns.</returns>
    /// <exception cref="InvalidOperationException">A caller used this handle before.</exception>
    TResult Construct<TResult>(Func<TBaseValues, TResult> constructor);
}
