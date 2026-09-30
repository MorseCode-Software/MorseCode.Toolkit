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
///         <see cref="StagedConstruction.Constructor.From{TBaseValues}" /> and
///         <see cref="Select{TSelectedValues}" /> make fail at a second call, also when two threads call
///         at the same time. The first call uses the handle, also when the constructor fails.
///     </para>
///     <para>
///         A base that needs a handle of its own derives from this class and implements
///         <see cref="ConstructCore{TResult}" />. It does not check for a second call.
///         <see cref="Construct{TResult}" /> claims the handle before it calls
///         <see cref="ConstructCore{TResult}" />. Thus, a second call fails, also when two threads call
///         at the same time, and also when the first call fails.
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
    private int used;

    /// <summary>
    ///     Calls <paramref name="constructor" /> with the values of the base.
    /// </summary>
    /// <remarks>
    ///     This method claims the handle, and then calls <see cref="ConstructCore{TResult}" />. The
    ///     claim occurs first. Thus, a call that fails also uses the handle, and a second call does not
    ///     run the constructor again.
    /// </remarks>
    /// <param name="constructor">The constructor of the subclass, or a function that calls it.</param>
    /// <typeparam name="TResult">
    ///     The type that <paramref name="constructor" /> returns. The compiler gets it from the lambda,
    ///     thus the caller does not write it.
    /// </typeparam>
    /// <returns>The value that <paramref name="constructor" /> returns.</returns>
    /// <exception cref="InvalidOperationException">A caller used this handle before.</exception>
    public TResult Construct<TResult>(Func<TBaseValues, TResult> constructor)
    {
        SingleUse.Claim(used: ref this.used, member: nameof(this.Construct));

        return this.ConstructCore(constructor: constructor);
    }

    /// <summary>
    ///     Makes the values of the base and calls <paramref name="constructor" /> with them.
    /// </summary>
    /// <remarks>
    ///     <see cref="Construct{TResult}" /> calls this method one time only.
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
    ///     <para>
    ///         A base that has a base of its own uses this to add its values to the values of that base.
    ///         The default handle calls <paramref name="selector" /> when the subclass calls
    ///         <see cref="Construct{TResult}" /> on it, and not before. Then, it calls
    ///         <see cref="Construct{TResult}" /> on this handle. Thus, this method does not use this
    ///         handle.
    ///     </para>
    ///     <para>
    ///         An override must refuse a null <paramref name="selector" /> as this method does. It must
    ///         return a handle that a caller can use one time only.
    ///     </para>
    /// </remarks>
    /// <param name="selector">The function that makes the new values from the values of this handle.</param>
    /// <typeparam name="TSelectedValues">The type of the new values.</typeparam>
    /// <returns>The new handle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector" /> is null.</exception>
    public virtual Constructor<TSelectedValues> Select<TSelectedValues>(Func<TBaseValues, TSelectedValues> selector) =>
        // The null check occurs here, where the caller is on the stack, and not subsequently in Construct.
        new SelectedConstruct<TBaseValues, TSelectedValues>(
            source: this,
            selector: selector ?? throw new ArgumentNullException(paramName: nameof(selector)));
}
