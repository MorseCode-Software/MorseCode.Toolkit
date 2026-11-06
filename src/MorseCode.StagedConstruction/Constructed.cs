using JetBrains.Annotations;

namespace MorseCode.StagedConstruction;

/// <summary>
///     The value that a <see cref="StageContinuation{TOutput,TNext,TResult}" />
///     returns. Only <see cref="Constructor{TBaseValues}.Construct{TResult}" />
///     makes one.
/// </summary>
/// <remarks>
///     <para>
///         A continuation returns the result of <c>Construct</c>. Or, it returns
///         the result of <c>Advance</c> on the handle for the subsequent step,
///         which gives that result back without a change. A continuation that
///         returns a different value does not compile. Thus, the compiler checks
///         that each continuation calls <c>Construct</c>. The handle is the only
///         source of the values of the base. This type is the only result.
///     </para>
///     <para>
///         The first stage of a base is its static <c>CreateBase</c> method. It
///         reads <see cref="Value" /> and gives the value to its caller. The
///         compiler cannot check a continuation that throws, because it does not
///         return.
///     </para>
/// </remarks>
/// <typeparam name="TResult">The type of the value. Usually, it is the subclass.</typeparam>
[PublicAPI]
public sealed class Constructed<TResult>
{
    // Only Construct makes an instance. The constructor is internal so that this stays true.
    internal Constructed(TResult value) => this.Value = value;

    /// <summary>
    ///     Gets the value that the constructor of the subclass returned.
    /// </summary>
    public TResult Value { get; }
}
