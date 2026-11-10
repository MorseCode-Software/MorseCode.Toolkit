using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     Checks a shown <see cref="VirtualizingItemsControl" /> while its collection changes, and while
///     it scrolls by less than a screen. Each test counts the values of the rows that the view model
///     sends. A control that reports an incorrect range makes the view model send the rows again.
/// </summary>
public sealed class VirtualizingItemsControlEditTests
{
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task AScrollByThreeRowsBringsTheRowsOnce(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: buffer);

                int before = model.RowsValues;

                shown.Scroller.Offset = new Vector(x: 0, y: 60);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 1);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 3, count: 10), ordering: CollectionOrdering.Matching);

                before = model.RowsValues;

                shown.Scroller.Offset = new Vector(x: 0, y: 0);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 1);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 0, count: 10), ordering: CollectionOrdering.Matching);
            });

    // Each row that shows moves up by one position, thus the panel replaces each container.
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task ARemovedKeyAboveTheRowsBringsTheRowsOnce(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: buffer);

                shown.Scroller.Offset = new Vector(x: 0, y: 10000);
                shown.Settle();

                int before = model.RowsValues;

                model.Remove(key: 0);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 1);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 501, count: 10), ordering: CollectionOrdering.Matching);
            });

    // The rows above the key stay, and the panel replaces only the containers from the key down.
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task ARemovedKeyInsideTheRowsBringsTheRowsOnce(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: buffer);

                int before = model.RowsValues;

                model.Remove(key: 4);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 1);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(
                        expected: [.. Rows(first: 0, count: 4), .. Rows(first: 5, count: 6)],
                        ordering: CollectionOrdering.Matching);
            });

    // The list ends in the window, thus the removal also removes the last container. The range that
    // the control reports stops at the count. Thus, the control reports a shorter range, and the second
    // value is the answer to it, with the same rows.
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task ARemovedKeyInAListThatEndsInsideTheWindowBringsTheRowsAndThenTheSameRowsForTheShorterRange(
        int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 5, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: buffer);

                int before = model.RowsValues;
                int built = model.Built.Count;

                model.Remove(key: 2);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 2);
                await Assert.That(model.Built.Count).IsEqualTo(expected: built);
                await Assert.That(shown.List.VisibleRange).IsEqualTo(expected: new ItemRange(First: 0, Count: 4));

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(
                        expected: ["Row 0", "Row 1", "Row 3", "Row 4"],
                        ordering: CollectionOrdering.Matching);
            });

    // The first value has the new count. The control can report the new position only after the panel
    // made a container for it. The second value has the row for that position.
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task AnAddedKeyAtAnEndThatShowsBringsTheCountAndThenTheRow(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 5, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: buffer);

                int before = model.RowsValues;

                model.Add(key: 5);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 2);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 0, count: 6), ordering: CollectionOrdering.Matching);
            });

    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task AnAddedKeyBelowTheRowsBringsTheCountOnly(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: buffer);

                int before = model.RowsValues;
                int built = model.Built.Count;

                model.Add(key: 1000);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 1);
                await Assert.That(model.Built.Count).IsEqualTo(expected: built);
                await Assert.That(shown.Scroller.Extent.Height).IsEqualTo(expected: 20020);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 0, count: 10), ordering: CollectionOrdering.Matching);
            });

    // The row of a key is made from the key. Thus, a change of the state of an item is not a change of
    // the rows.
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task AChangedStateOfAKeyThatShowsBringsNoRows(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: buffer);

                int before = model.RowsValues;

                model.Update(key: 3);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 0);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 0, count: 10), ordering: CollectionOrdering.Matching);
            });

    // The texts of a number of rows in sequence. A placeholder is never one of them. Thus, a test that
    // compares with these texts also checks that no placeholder shows.
    private static List<string> Rows(int first, int count) =>
        [.. Enumerable.Range(start: first, count: count).Select(selector: static key => $"Row {key}")];

    private static List<string> Texts(ShownVirtual shown) =>
        [.. shown.Shown().Select(selector: static pair => pair.Text)];
}
