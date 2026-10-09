using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using JetBrains.Annotations;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     Converts a <see cref="Maybe{T}" /> of a view model to a value or null for
///     a control, and back, for a two-way binding.
/// </summary>
/// <remarks>
///     <para>
///         Close this class for each type with a sealed subclass that has no
///         members. XAML can then name the subclass:
///     </para>
///     <code>
///         public sealed class CourseOptionConverter
///             : MaybeConverter&lt;CourseOption, CourseOptionConverter&gt;;
///
///         SelectedItem="{Binding Course.Value, Mode=TwoWay,
///                        Converter={x:Static app:CourseOptionConverter.Instance}}"
///     </code>
///     <para>
///         Toward the control, a value gives the value, and no value gives null.
///         Toward the view model, null gives no value, and a value gives that value.
///         The code knows <typeparamref name="T" /> when it compiles. Thus, it uses
///         no reflection, and trimming and compilation ahead of time keep each
///         part that it uses.
///     </para>
///     <para>
///         A value of a different type gives a
///         <see cref="BindingNotification" /> with the error. The binding then shows
///         the error and does not write.
///     </para>
/// </remarks>
/// <typeparam name="T">The type of the value in the <see cref="Maybe{T}" />.</typeparam>
/// <typeparam name="TSelf">The sealed subclass that closes this class.</typeparam>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - IValueConverter does not say which values this converts.
public abstract class MaybeConverter<T, TSelf> : IValueConverter
    where TSelf : MaybeConverter<T, TSelf>, new()
{
    /// <summary>
    ///     Gets the converter. It keeps no state.
    /// </summary>
    // ReSharper disable once StaticMemberInGenericType - Each closed type has its own instance, which is the intent.
    public static TSelf Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Maybe<T> maybe
            ? maybe.Match<object?>(onSome: static inner => inner, onNone: static () => null)
            : MaybeConversion.Error(
                message:
                $"{typeof(TSelf)} converts a {typeof(Maybe<T>)}, and got {MaybeConversion.Describe(value: value)}.");

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (targetType != typeof(Maybe<T>))
        {
            return MaybeConversion.Error(
                message: $"{typeof(TSelf)} converts back to a {typeof(Maybe<T>)}, and not to {targetType}.");
        }

        return value switch
        {
            null => Maybe<T>.None,
            T typed => Maybe.Some(value: typed),
            _ => MaybeConversion.Error(
                message: $"{typeof(TSelf)} cannot put {MaybeConversion.Describe(value: value)} in a {targetType}.")
        };
    }
}
