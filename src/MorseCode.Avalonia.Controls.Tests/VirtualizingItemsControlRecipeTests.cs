using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using SodaFlow.Functional;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     Checks a <see cref="VirtualizingItemsControl" /> with a real SodaFlow collection, with a <c>Slice</c>
///     and a <c>Map</c> after it, as the documentation describes.
/// </summary>
public sealed class VirtualizingItemsControlRecipeTests
{
    [Test]
    public Task TheMapBuildsARowOnlyForAKeyInTheSlice() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: 0);

                await Assert.That(model.Built)
                    .IsEquivalentTo(
                        expected: [.. Enumerable.Range(start: 0, count: 10)],
                        ordering: CollectionOrdering.Matching);

                await Assert.That(shown.Shown()[0].Text).IsEqualTo(expected: "Row 0");
                await Assert.That(shown.Shown().Any(predicate: static pair => pair.Text == "placeholder")).IsFalse();
            });

    [Test]
    public Task AScrollMovesTheSliceAndBuildsOnlyTheRowsItReaches() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: 0);

                shown.Scroller.Offset = new Vector(x: 0, y: 10000);
                shown.Settle();

                await Assert.That(shown.Shown()[0].Text).IsEqualTo(expected: "Row 500");
                await Assert.That(shown.Shown().Any(predicate: static pair => pair.Text == "placeholder")).IsFalse();
                await Assert.That(model.Built.Count).IsEqualTo(expected: 20);
                await Assert.That(model.Built.Any(predicate: static key => key is > 9 and < 500)).IsFalse();
            });

    // The view model asks with its action, and the request is a two-way bindable value. The control scrolls
    // and writes none back, which clears the request in the graph.
    [Test]
    public Task AnActionOfTheViewModelScrollsTheListAndTheControlClearsTheRequest() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: 0);

                model.ScrollTo.Execute(parameter: 700);
                shown.Settle();

                await Assert.That(shown.Shown().Any(predicate: static pair => pair.Text == "Row 700")).IsTrue();
                await Assert.That(model.ScrollRequest.Value).IsEqualTo(expected: Maybe<int>.None);
            });

    // The same, when the view model asks before the view binds.
    [Test]
    public Task AnActionOfTheViewModelBeforeTheViewBindsScrollsTheListWhenItBinds() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);

                model.ScrollTo.Execute(parameter: 700);

                using ShownVirtual shown = new(model: model, buffer: 0);

                await Assert.That(shown.Shown().Any(predicate: static pair => pair.Text == "Row 700")).IsTrue();
                await Assert.That(model.ScrollRequest.Value).IsEqualTo(expected: Maybe<int>.None);
            });

    [Test]
    public Task ARowThatLeftTheSliceIsReleasedWhenTheMapBoundIsPassed() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 5);
                using ShownVirtual shown = new(model: model, buffer: 0);

                shown.Scroller.Offset = new Vector(x: 0, y: 10000);
                shown.Settle();

                // Ten keys left the slice and the map keeps five, thus the five that left first go.
                await Assert.That(model.Evicted)
                    .IsEquivalentTo(expected: [0, 1, 2, 3, 4], ordering: CollectionOrdering.Matching);
            });
}
