using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Data;
using SodaFlow.Functional;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     Checks that <see cref="MaybeConverter" /> converts a
///     <see cref="Maybe{T}" /> to a value or null and back, alone and in a
///     two-way binding of a <see cref="ComboBox" />.
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
        await Assert.That(IsError(result: Convert(value: "B"))).IsTrue();

    [Test]
    public async Task NullGoesBackAsNoValueOfTheTypeOfTheProperty() =>
        await Assert.That(ConvertBack(value: null, targetType: typeof(Maybe<string>)))
            .IsEqualTo(expected: Maybe<string>.None);

    [Test]
    public async Task AValueGoesBackAsThatValue() =>
        await Assert.That(ConvertBack(value: "B", targetType: typeof(Maybe<string>)))
            .IsEqualTo(expected: Maybe.Some(value: "B"));

    [Test]
    public async Task AValueOfADerivedTypeGoesBackInAMaybeOfTheTypeOfTheProperty() =>
        await Assert.That(ConvertBack(value: "B", targetType: typeof(Maybe<object>)))
            .IsEqualTo(expected: Maybe.Some<object>(value: "B"));

    [Test]
    public async Task AValueOfADifferentTypeGoesBackAsAnError() =>
        await Assert.That(IsError(result: ConvertBack(value: 3, targetType: typeof(Maybe<string>)))).IsTrue();

    [Test]
    public async Task APropertyThatIsNotAMaybeGetsAnError() =>
        await Assert.That(IsError(result: ConvertBack(value: "B", targetType: typeof(string)))).IsTrue();

    [Test]
    public Task AValueOfTheViewModelSelectsItsItem() =>
        TestApplication.Run(
            test: static async () =>
            {
                MaybeSelectionModel model = new(items: ["None"], selected: Maybe.None);
                using ShownMaybe shown = ShownMaybe.InXaml(model: model);

                model.Post(
                    change: static m =>
                    {
                        m.SetItems(items: ["None", "A", "B"]);
                        m.SetSelected(value: Maybe.Some(value: "B"));
                    });

                shown.Settle();

                await Assert.That(model.Writes).IsEmpty();
                await Assert.That(shown.Box.SelectedItem).IsEqualTo(expected: "B");
            });

    [Test]
    public Task NoValueOfTheViewModelSelectsNoItem() =>
        TestApplication.Run(
            test: static async () =>
            {
                MaybeSelectionModel model = new(items: ["None", "A", "B"], selected: Maybe.Some(value: "B"));
                using ShownMaybe shown = ShownMaybe.InXaml(model: model);

                model.Post(change: static m => m.SetSelected(value: Maybe.None));
                shown.Settle();

                await Assert.That(model.Writes).IsEmpty();
                await Assert.That(shown.Box.SelectedItem).IsNull();
            });

    [Test]
    public Task ASelectionOfTheUserGoesToTheViewModelAsAValue() =>
        TestApplication.Run(
            test: static async () =>
            {
                MaybeSelectionModel model = new(items: ["None", "A", "B"], selected: Maybe.Some(value: "B"));
                using ShownMaybe shown = ShownMaybe.InXaml(model: model);

                shown.Box.SelectedIndex = 1;
                shown.Settle();

                await Assert.That(model.Writes)
                    .IsEquivalentTo(expected: [Maybe.Some(value: "A")], ordering: CollectionOrdering.Matching);
            });

    // The combo box writes null for an item that its list does not have. The converter makes that no value.
    [Test]
    public Task AnItemThatTheListDoesNotHaveGoesToTheViewModelAsNoValue() =>
        TestApplication.Run(
            test: static async () =>
            {
                MaybeSelectionModel model = new(items: ["None", "A", "B"], selected: Maybe.Some(value: "B"));
                using ShownMaybe shown = ShownMaybe.InXaml(model: model);

                model.Post(change: static m => m.SetSelected(value: Maybe.Some(value: "Z")));
                shown.Settle();

                await Assert.That(model.Writes)
                    .IsEquivalentTo(expected: [Maybe<string>.None], ordering: CollectionOrdering.Matching);

                await Assert.That(model.Selected).IsEqualTo(expected: Maybe<string>.None);
            });

    private static object? Convert(object? value) =>
        MaybeConverter.Instance.Convert(
            value: value,
            targetType: typeof(object),
            parameter: null,
            culture: CultureInfo.InvariantCulture);

    private static object? ConvertBack(object? value, Type targetType) =>
        MaybeConverter.Instance.ConvertBack(
            value: value,
            targetType: targetType,
            parameter: null,
            culture: CultureInfo.InvariantCulture);

    private static bool IsError(object? result) =>
        result is BindingNotification { ErrorType: BindingErrorType.Error, Error: InvalidCastException };

    // A combo box in a window, from XAML, with the first values of the model.
    private sealed class ShownMaybe : IDisposable
    {
        private readonly global::Avalonia.Controls.Window window;

        private ShownMaybe(MaybeComboBoxView view, MaybeSelectionModel model)
        {
            this.Box = view.Box;
            this.window = new global::Avalonia.Controls.Window { Content = view, Width = 300, Height = 200 };
            this.window.Show();
            this.Settle();
            model.Writes.Clear();
        }

        public ComboBox Box { get; }

        public static ShownMaybe InXaml(MaybeSelectionModel model) =>
            new(view: new MaybeComboBoxView { DataContext = model }, model: model);

        // Runs the work of the dispatcher, which includes the posted changes and the end of a batch.
        public void Settle() => this.window.Dispatcher.RunJobs();

        public void Dispose() => this.window.Close();
    }
}
