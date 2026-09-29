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
}
