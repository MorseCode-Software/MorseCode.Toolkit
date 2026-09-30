using System;
using JetBrains.Annotations;

namespace MorseCode.StagedConstruction;

/// <summary>
///     Makes the usual <see cref="IConstruct{TBaseValues}" /> handles.
/// </summary>
[PublicAPI]
public static class Construct
{
    /// <summary>
    ///     Makes a handle that gives <paramref name="values" /> to the constructor of the subclass.
    /// </summary>
    /// <remarks>
    ///     A base gives this handle to the subclass at its last stage, after it makes all of its values.
    /// </remarks>
    /// <param name="values">The values of the base.</param>
    /// <typeparam name="TBaseValues">The type of the values of the base.</typeparam>
    /// <returns>The handle.</returns>
    public static IConstruct<TBaseValues> From<TBaseValues>(TBaseValues values) =>
        new ValuesConstruct<TBaseValues>(values: values);

    /// <summary>
    ///     Makes a handle that gives the result of <paramref name="selector" /> to the constructor, and
    ///     not the values of <paramref name="construct" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A base that has a base of its own uses this to add its values to the values of that base.
    ///         The new handle calls <paramref name="selector" /> when the subclass calls
    ///         <see cref="IConstruct{TBaseValues}.Construct{TResult}" /> on it, and not before.
    ///     </para>
    ///     <para>
    ///         This is an extension method and not a member of <see cref="IConstruct{TBaseValues}" />.
    ///         .NET Framework and .NET Standard 2.0 do not support a default interface member.
    ///     </para>
    /// </remarks>
    /// <param name="construct">The handle that gives the values to select from.</param>
    /// <param name="selector">The function that makes the new values from the values of <paramref name="construct" />.</param>
    /// <typeparam name="TBaseValues">The type of the values of <paramref name="construct" />.</typeparam>
    /// <typeparam name="TSelectedValues">The type of the new values.</typeparam>
    /// <returns>The new handle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector" /> is null.</exception>
    public static IConstruct<TSelectedValues> Select<TBaseValues, TSelectedValues>(
        this IConstruct<TBaseValues> construct,
        Func<TBaseValues, TSelectedValues> selector) =>
        // A handle that derives from ConstructBase can override Select, so the call goes to it. The null
        // check occurs here, where the caller is on the stack, and not subsequently in Construct.
        construct is ConstructBase<TBaseValues> constructBase
            ? constructBase.Select(selector: selector)
            : new SelectedConstruct<TBaseValues, TSelectedValues>(
                source: construct,
                selector: selector ?? throw new ArgumentNullException(paramName: nameof(selector)));
}
