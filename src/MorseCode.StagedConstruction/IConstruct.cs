using System;
using JetBrains.Annotations;

namespace MorseCode.StagedConstruction;

/// <summary>
///     The handle for the step after the last stage of a base. It gives the values of the base to the
///     constructor of the subclass.
/// </summary>
/// <remarks>
///     <para>
///         The subclass must call <see cref="Construct{TResult}" /> one time only. The handle does not
///         make sure of this.
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
    TResult Construct<TResult>(Func<TBaseValues, TResult> constructor);

    /// <summary>
    ///     Makes a handle that gives the result of <paramref name="selector" /> to the constructor, and
    ///     not the values of this handle.
    /// </summary>
    /// <remarks>
    ///     A base that has a base of its own uses this to add its values to the values of that base.
    ///     The new handle calls <paramref name="selector" /> when the subclass calls
    ///     <see cref="Construct{TResult}" /> on it, and not before.
    /// </remarks>
    /// <param name="selector">The function that makes the new values from the values of this handle.</param>
    /// <typeparam name="TSelectedValues">The type of the new values.</typeparam>
    /// <returns>The new handle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector" /> is null.</exception>
    IConstruct<TSelectedValues> Select<TSelectedValues>(Func<TBaseValues, TSelectedValues> selector)
    {
        // The null check occurs here, where the caller is on the stack, and not subsequently in Construct.
        ArgumentNullException.ThrowIfNull(argument: selector);

        return new SelectedConstruct<TBaseValues, TSelectedValues>(source: this, selector: selector);
    }
}
