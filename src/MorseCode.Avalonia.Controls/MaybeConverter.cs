using System;
using System.Globalization;
using System.Reflection;
using Avalonia.Data;
using Avalonia.Data.Converters;
using JetBrains.Annotations;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     Converts a <see cref="Maybe{T}" /> of a view model to a value or null for
///     a control, and back.
/// </summary>
/// <remarks>
///     <para>
///         A control gives null when it has no value, for example a combo box with
///         no selected item. A view model on SodaFlow uses
///         <see cref="Maybe{T}" /> and not null. Put this converter on each binding
///         that can get null from the control:
///     </para>
///     <code>
///         SelectedItem="{Binding Course.Value, Mode=TwoWay,
///                        Converter={x:Static mc:MaybeConverter.Instance}}"
///     </code>
///     <para>
///         One instance converts each <see cref="Maybe{T}" />. Toward the control,
///         a value gives the value, and no value gives null. Toward the view model,
///         the binding gives the type of the property, such as
///         <c>Maybe&lt;CourseOption&gt;</c>. Null gives no value of that type, and
///         a value gives that value.
///     </para>
///     <para>
///         A value of a different type gives a
///         <see cref="BindingNotification" /> with the error. The binding then shows
///         the error and does not write.
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
            : Error(message: $"MaybeConverter converts a Maybe<T>, and got {Describe(value: value)}.");

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!targetType.IsGenericType || targetType.GetGenericTypeDefinition() != typeof(Maybe<>))
        {
            return Error(message: $"MaybeConverter converts back to a Maybe<T>, and not to {targetType}.");
        }

        Type valueType = targetType.GetGenericArguments()[0];

        if (value == null)
        {
            // ReSharper disable once NullableWarningSuppressionIsUsed - Each Maybe<T> has the static field None.
            return targetType.GetField(
                    name: nameof(Maybe<>.None),
                    bindingAttr: BindingFlags.Public | BindingFlags.Static)!
                .GetValue(obj: null);
        }

        if (!valueType.IsInstanceOfType(o: value))
        {
            return Error(message: $"MaybeConverter cannot put {Describe(value: value)} in a {targetType}.");
        }

        // ReSharper disable once NullableWarningSuppressionIsUsed - Each Maybe<T> has the static method Some(T).
        return targetType.GetMethod(
                name: nameof(Maybe<>.Some),
                bindingAttr: BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [valueType],
                modifiers: null)!
            .Invoke(obj: null, parameters: [value]);
    }

    private static BindingNotification Error(string message) =>
        new(error: new InvalidCastException(message: message), errorType: BindingErrorType.Error);

    private static string Describe(object? value) => value == null ? "null" : $"a {value.GetType()}";
}
