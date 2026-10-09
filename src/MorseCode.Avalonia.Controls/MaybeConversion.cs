using System;
using Avalonia.Data;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     The errors that the two Maybe converters give.
/// </summary>
internal static class MaybeConversion
{
    /// <summary>
    ///     Makes the error that a binding shows. The binding then does not write.
    /// </summary>
    /// <param name="message">The message of the error.</param>
    /// <returns>The error.</returns>
    public static BindingNotification Error(string message) =>
        new(error: new InvalidCastException(message: message), errorType: BindingErrorType.Error);

    /// <summary>
    ///     Describes a value for the message of an error.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>"null", or the type of the value.</returns>
    public static string Describe(object? value) => value == null ? "null" : $"a {value.GetType()}";
}
