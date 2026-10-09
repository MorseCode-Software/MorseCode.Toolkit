using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Data;
using SodaFlow.Functional;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     Checks that <see cref="MaybeConverter" /> converts a
///     <see cref="Maybe{T}" /> to a value or null, and refuses to convert back.
/// </summary>
public sealed class MaybeConverterTests
{
    [Test]
    public async Task AValueGoesToTheControlAsTheValue() =>
        await Assert.That(Convert(value: Maybe.Some(value: "B"))).IsEqualTo(expected: "B");

    [Test]
    public async Task NoValueGoesToTheControlAsNull() => await Assert.That(Convert(value: Maybe<string>.None)).IsNull();

    [Test]
    public async Task AValueThatIsNotAMaybeGoesToTheControlAsAnError() =>
        await Assert.That(ErrorMessage(result: Convert(value: "B"))).IsNotNull();

    [Test]
    public async Task AConversionBackIsAnErrorThatNamesTheConverterForATwoWayBinding() =>
        await Assert.That(
                ErrorMessage(
                    result: MaybeConverter.Instance.ConvertBack(
                        value: "B",
                        targetType: typeof(Maybe<string>),
                        parameter: null,
                        culture: CultureInfo.InvariantCulture)))
            .Contains(expected: "MaybeConverter<T, TSelf>");

    [Test]
    public Task ATwoWayBindingShowsTheValueAndWritesNothingBack() =>
        TestApplication.Run(
            test: static async () =>
            {
                MaybeSelectionModel model = new(items: ["None", "A", "B"], selected: Maybe.Some(value: "B"));
                using ShownMaybe shown = ShownMaybe.TwoWayInCode(model: model, converter: MaybeConverter.Instance);
                object? shownFirst = shown.Box.SelectedItem;

                shown.Box.SelectedIndex = 1;
                shown.Settle();

                await Assert.That(shownFirst).IsEqualTo(expected: "B");
                await Assert.That(model.Writes).IsEmpty();
                await Assert.That(model.Selected).IsEqualTo(expected: Maybe.Some(value: "B"));
            });

    private static object? Convert(object? value) =>
        MaybeConverter.Instance.Convert(
            value: value,
            targetType: typeof(object),
            parameter: null,
            culture: CultureInfo.InvariantCulture);

    // The message of an error that a binding shows, or null when the result is not one.
    private static string? ErrorMessage(object? result) =>
        result is BindingNotification { ErrorType: BindingErrorType.Error, Error: InvalidCastException error }
            ? error.Message
            : null;
}
