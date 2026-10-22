using System.Linq;
using System.Threading.Tasks;
using Avalonia;
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
