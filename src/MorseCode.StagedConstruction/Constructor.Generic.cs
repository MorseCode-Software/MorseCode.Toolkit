using System;
using JetBrains.Annotations;

namespace MorseCode.StagedConstruction;

/// <summary>
///     The handle for the step after the last stage of a base. It gives the values of the base to the
///     constructor of the subclass.
/// </summary>
/// <remarks>
///     <para>
///         The subclass must use a handle one time only: it calls <see cref="Construct{TResult}" /> or
///         <see cref="Select{TSelectedValues}" />, one time. A second call to either fails, also when
///         two threads call at the same time. The first call uses the handle, also when it fails.
///     </para>
///     <para>
///         A base that needs a handle of its own derives from this class and implements
///         <see cref="ConstructCore{TResult}" />. It can also override
///         <see cref="SelectCore{TSelectedValues}" />. Neither method checks for a second call.
///         <see cref="Construct{TResult}" /> and <see cref="Select{TSelectedValues}" /> claim the handle
///         before they call them. Thus, a second call fails, also when two threads call at the same
///         time, and also when the first call fails.
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
public abstract class Constructor<TBaseValues>
{
    private string? usedBy;

    /// <summary>
    ///     Calls <paramref name="constructor" /> with the values of the base.
    /// </summary>
    /// <remarks>
    ///     This method claims the handle, and then calls <see cref="ConstructCore{TResult}" />. The
    ///     claim occurs first. Thus, a call that fails also uses the handle, and a second call does not
    ///     run the constructor again. A null <paramref name="constructor" /> fails before the claim,
    ///     and does not use the handle.
    /// </remarks>
    /// <param name="constructor">The constructor of the subclass, or a function that calls it.</param>
    /// <typeparam name="TResult">
    ///     The type that <paramref name="constructor" /> returns. The compiler gets it from the lambda,
    ///     thus the caller does not write it.
    /// </typeparam>
    /// <returns>The value that <paramref name="constructor" /> returns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="constructor" /> is null.</exception>
    /// <exception cref="InvalidOperationException">A caller used this handle before.</exception>
    public TResult Construct<TResult>(Func<TBaseValues, TResult> constructor)
    {
        // The null check occurs before the claim. Thus, a null constructor does not use the handle.
        if (constructor is null)
        {
            throw new ArgumentNullException(paramName: nameof(constructor));
        }

        SingleUse.Claim(usedBy: ref this.usedBy, member: nameof(this.Construct));

        return this.ConstructCore(constructor: constructor);
    }

    /// <summary>
    ///     Makes the values of the base and calls <paramref name="constructor" /> with them.
    /// </summary>
    /// <remarks>
    ///     <see cref="Construct{TResult}" /> calls this method one time only, with a
    ///     <paramref name="constructor" /> that is not null.
    /// </remarks>
    /// <param name="constructor">The constructor of the subclass, or a function that calls it.</param>
    /// <typeparam name="TResult">The type that <paramref name="constructor" /> returns.</typeparam>
    /// <returns>The value that <paramref name="constructor" /> returns.</returns>
    protected abstract TResult ConstructCore<TResult>(Func<TBaseValues, TResult> constructor);

    /// <summary>
    ///     Makes a handle that gives the result of <paramref name="selector" /> to the constructor, and
    ///     not the values of this handle.
    /// </summary>
    /// <remarks>
    ///     A base that has a base of its own uses this to add its values to the values of that base.
    ///     This method claims this handle, and then calls <see cref="SelectCore{TSelectedValues}" />.
    ///     Thus, after a call to this method, a call to <see cref="Construct{TResult}" /> or to this
    ///     method on this handle fails, and only the new handle can give the values to a constructor.
    ///     A null <paramref name="selector" /> fails before the claim, and does not use the handle.
    /// </remarks>
    /// <param name="selector">The function that makes the new values from the values of this handle.</param>
    /// <typeparam name="TSelectedValues">The type of the new values.</typeparam>
    /// <returns>The new handle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector" /> is null.</exception>
    /// <exception cref="InvalidOperationException">A caller used this handle before.</exception>
    public Constructor<TSelectedValues> Select<TSelectedValues>(Func<TBaseValues, TSelectedValues> selector)
    {
        // The null check occurs here, where the caller is on the stack, and before the claim. Thus, a null
        // selector does not use the handle.
        if (selector is null)
        {
            throw new ArgumentNullException(paramName: nameof(selector));
        }

        SingleUse.Claim(usedBy: ref this.usedBy, member: nameof(this.Select));

        return this.SelectCore(selector: selector);
    }

    /// <summary>
    ///     Makes the handle that <see cref="Select{TSelectedValues}" /> returns.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <see cref="Select{TSelectedValues}" /> calls this method one time only, with a
    ///         <paramref name="selector" /> that is not null, after it claims this handle. Thus, an
    ///         override cannot call <see cref="Construct{TResult}" /> on this handle. It gets the
    ///         values from <see cref="ConstructCore{TResult}" />, or from its own fields.
    ///     </para>
    ///     <para>
    ///         The default handle calls <paramref name="selector" /> when the subclass calls
    ///         <see cref="Construct{TResult}" /> on it, and not before. Then, it calls
    ///         <see cref="ConstructCore{TResult}" /> on this handle.
    ///     </para>
    /// </remarks>
    /// <param name="selector">The function that makes the new values from the values of this handle.</param>
    /// <typeparam name="TSelectedValues">The type of the new values.</typeparam>
    /// <returns>The new handle.</returns>
    protected virtual Constructor<TSelectedValues> SelectCore<TSelectedValues>(Func<TBaseValues, TSelectedValues> selector) =>
        new SelectedConstruct<TBaseValues, TSelectedValues>(source: this, selector: selector);

    // The handle that the default SelectCore makes calls this. Select claimed this handle, so Construct
    // refuses it, and the protected ConstructCore cannot be called through a handle of a different
    // values type.
    internal TResult ConstructAfterSelect<TResult>(Func<TBaseValues, TResult> constructor) =>
        this.ConstructCore(constructor: constructor);
}
