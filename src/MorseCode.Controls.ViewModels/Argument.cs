using System;

namespace MorseCode.Controls.ViewModels;

/// <summary>
///     The checks that the public methods of this package make on their arguments.
/// </summary>
internal static class Argument
{
    /// <summary>
    ///     Refuses an argument that is null.
    /// </summary>
    /// <remarks>
    ///     <c>ArgumentNullException.ThrowIfNull</c> does this on .NET 6 and later. This package also has
    ///     the targets netstandard2.0 and net472, which do not have that method.
    /// </remarks>
    /// <param name="argument">The value of the argument.</param>
    /// <param name="paramName">The name of the parameter, for the exception.</param>
    /// <exception cref="ArgumentNullException"><paramref name="argument" /> is null.</exception>
    internal static void ThrowIfNull(object? argument, string paramName)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull does not exist on netstandard2.0 and net472.
        if (argument is null)
        {
            throw new ArgumentNullException(paramName: paramName);
        }
#pragma warning restore CA1510
    }
}
