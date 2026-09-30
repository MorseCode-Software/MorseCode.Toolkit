using System;
using JetBrains.Annotations;

namespace MorseCode.StagedConstruction;

/// <summary>
///     The base class for a handle that implements <see cref="IConstruct{TBaseValues}" />. It makes
///     sure that a caller uses the handle one time only.
/// </summary>
/// <remarks>
///     <para>
///         A class that derives from this class implements <see cref="ConstructCore{TResult}" />, and
///         it does not check for a second call. The <see cref="Construct{TResult}" /> method claims the
///         handle before it calls <see cref="ConstructCore{TResult}" />. Thus, a second call fails,
///         also when two threads call at the same time, and also when the first call fails.
///     </para>
///     <para>
///         A derived class can override <see cref="Select{TSelectedValues}" /> when the default handle
///         does not suit it. The <c>Select</c> extension method of
///         <see cref="StagedConstruction.Construct" /> calls the override, also for a receiver that has
///         the type <see cref="IConstruct{TBaseValues}" />.
///     </para>
/// </remarks>
/// <typeparam name="TBaseValues">The type of the values of the base.</typeparam>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - The summary of IConstruct does not say that this class checks for a second call.
public abstract class ConstructBase<TBaseValues> : IConstruct<TBaseValues>
{
    private int used;

    /// <inheritdoc />
    /// <remarks>
    ///     This method claims the handle, and then calls <see cref="ConstructCore{TResult}" />. The
    ///     claim occurs first. Thus, a call that fails also uses the handle, and a second call does not
    ///     run the constructor again.
    /// </remarks>
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
    public virtual IConstruct<TSelectedValues> Select<TSelectedValues>(Func<TBaseValues, TSelectedValues> selector) =>
        // The null check occurs here, where the caller is on the stack, and not subsequently in Construct.
        new SelectedConstruct<TBaseValues, TSelectedValues>(
            source: this,
            selector: selector ?? throw new ArgumentNullException(paramName: nameof(selector)));
}
