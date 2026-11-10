using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     Checks a <see cref="VirtualizingItemsControl" /> whose panel is a <see cref="RecyclingPanel" />.
///     The control cannot read the range of such a panel. Thus, it reads the containers, and it must
///     read them only after the layout of the panel. Each test counts the values of the rows that the
///     view model sends, as the tests with the default panel do.
/// </summary>
public sealed class VirtualizingItemsControlPanelTests
{
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task AFirstLayoutReportsThePositionsOfTheContainers(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = Show(model: model, buffer: buffer);

                await Assert.That(model.RowsValues).IsEqualTo(expected: 1);

                await Assert.That(shown.List.VisibleRange)
                    .IsEqualTo(expected: new ItemRange(First: 0, Count: 10 + buffer));

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 0, count: 10), ordering: CollectionOrdering.Matching);
            });

    // The panel replaces each container, thus no container is there when the control reports.
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task ARemovedFirstKeyBringsTheRowsOnce(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = Show(model: model, buffer: buffer);

                int before = model.RowsValues;

                model.Remove(key: 0);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 1);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 1, count: 10), ordering: CollectionOrdering.Matching);
            });

    // The panel replaces only the containers from the key down. Thus, some containers are there when
    // the control reports, and they are fewer than the positions that show.
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task ARemovedKeyInsideTheRowsBringsTheRowsOnce(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = Show(model: model, buffer: buffer);

                int before = model.RowsValues;

                model.Remove(key: 4);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 1);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(
                        expected: [.. Rows(first: 0, count: 4), .. Rows(first: 5, count: 6)],
                        ordering: CollectionOrdering.Matching);
            });

    // The second value is the answer to the shorter range, as with the default panel.
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task ARemovedKeyInAListThatEndsInsideTheWindowBringsTheRowsAndThenTheSameRowsForTheShorterRange(
        int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 5, retainedBeyondTheView: 512);
                using ShownVirtual shown = Show(model: model, buffer: buffer);

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

    // The second value has the row for the new position, as with the default panel.
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task AnAddedKeyAtAnEndThatShowsBringsTheCountAndThenTheRow(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 5, retainedBeyondTheView: 512);
                using ShownVirtual shown = Show(model: model, buffer: buffer);

                int before = model.RowsValues;

                model.Add(key: 5);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 2);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 0, count: 6), ordering: CollectionOrdering.Matching);
            });

    private static ShownVirtual Show(IVirtualItemsSource model, int buffer) =>
        new(model: model, buffer: buffer, panel: static () => new RecyclingPanel());

    // The texts of a number of rows in sequence. A placeholder is never one of them. Thus, a test that
    // compares with these texts also checks that no placeholder shows.
    private static List<string> Rows(int first, int count) =>
        [.. Enumerable.Range(start: first, count: count).Select(selector: static key => $"Row {key}")];

    private static List<string> Texts(ShownVirtual shown) =>
        [.. shown.Shown().Select(selector: static pair => pair.Text)];
}
