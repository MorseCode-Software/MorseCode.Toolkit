using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using JetBrains.Annotations;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     Converts a <see cref="Maybe{T}" /> of a view model to a value or null for
///     a control, for a one-way binding.
/// </summary>
/// <remarks>
///     <para>
///         A view model on SodaFlow uses <see cref="Maybe{T}" /> and not null. One
///         instance converts each <see cref="Maybe{T}" />: a value gives the value,
///         and no value gives null.
///     </para>
///     <code>
///         Text="{Binding Note.Value, Converter={x:Static mc:MaybeConverter.Instance}}"
///     </code>
///     <para>
///         A two-way binding needs <see cref="MaybeConverter{T,TSelf}" />, which
///         knows the type to give back. A conversion back with this converter gives
///         a <see cref="BindingNotification" /> that says so, and the binding does
///         not write.
///     </para>
/// </remarks>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - IValueConverter does not say which values this converts.
public sealed class MaybeConverter : IValueConverter
{
    private MaybeConverter()
    {
    }

    /// <summary>
    ///     Gets the converter. It keeps no state.
    /// </summary>
    public static MaybeConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is IMaybe maybe
            ? maybe.Match<object?>(onSome: static inner => inner, onNone: static () => null)
            : MaybeConversion.Error(
                message: $"MaybeConverter converts a Maybe<T>, and got {MaybeConversion.Describe(value: value)}.");

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        MaybeConversion.Error(
            message: "MaybeConverter is for one-way bindings. For a two-way binding to a "
                     + $"{targetType}, close MaybeConverter<T, TSelf> for the type of the value.");
}
