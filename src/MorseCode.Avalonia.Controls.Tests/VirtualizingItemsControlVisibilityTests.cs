using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using SodaFlow;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     Checks how much of each row a shown <see cref="VirtualizingItemsControl" /> reports as visible.
///     The window is 200 high and each row is 20 high.
/// </summary>
public sealed class VirtualizingItemsControlVisibilityTests
{
    [Test]
    public Task AListAtRestReportsEachRowThatShowsInFull() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: 10);

                ReportedRange reported = shown.List.ReportedRange;

                await Assert.That(Enumerable.Range(start: 0, count: 10).Select(selector: reported.VisibilityOf))
                    .IsEquivalentTo(expected: Enumerable.Repeat(element: 1.0, count: 10));

                await Assert.That(reported.VisibilityOf(position: 10)).IsEqualTo(expected: 0.0);

                // This is the value that a view model makes with the two ranges only.
                ReportedRange fromTheRanges = new(Visible: reported.Visible, Constructed: reported.Constructed);

                await Assert.That(reported).IsEqualTo(expected: fromTheRanges);
            });

    // Row 0 has 13 of its 20 in the viewport, and row 10 has 7.
    [Test]
    public Task ARowThatShowsInPartReportsThePartOfItThatShows() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: 10);

                shown.Scroller.Offset = new Vector(x: 0, y: 7);
                shown.Settle();

                ReportedRange reported = shown.List.ReportedRange;

                await Assert.That(reported.Visible).IsEqualTo(expected: new ItemRange(First: 0, Count: 11));
                await Assert.That(reported.VisibilityOf(position: -1)).IsEqualTo(expected: 0.0);
                await Assert.That(Part(reported: reported, position: 0)).IsEqualTo(expected: 0.65);
                await Assert.That(reported.VisibilityOf(position: 1)).IsEqualTo(expected: 1.0);
                await Assert.That(reported.VisibilityOf(position: 9)).IsEqualTo(expected: 1.0);
                await Assert.That(Part(reported: reported, position: 10)).IsEqualTo(expected: 0.35);
                await Assert.That(reported.VisibilityOf(position: 11)).IsEqualTo(expected: 0.0);
            });

    // A scroll that brings no new row changes no container. The control reports the new parts, and the
    // view model sends no rows for that report.
    [Test]
    public Task AScrollInsideARowReportsTheNewPartsAndBringsNoRows() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: 10);

                shown.Scroller.Offset = new Vector(x: 0, y: 3);
                shown.Settle();

                ReportedRange before = shown.List.ReportedRange;
                int values = model.RowsValues;
                int built = model.Built.Count;

                shown.Scroller.Offset = new Vector(x: 0, y: 6);
                shown.Settle();

                ReportedRange after = shown.List.ReportedRange;

                await Assert.That(after.Visible).IsEqualTo(expected: before.Visible);
                await Assert.That(after.Constructed).IsEqualTo(expected: before.Constructed);
                await Assert.That(Part(reported: before, position: 0)).IsEqualTo(expected: 0.85);
                await Assert.That(Part(reported: after, position: 0)).IsEqualTo(expected: 0.7);
                await Assert.That(Part(reported: after, position: 10)).IsEqualTo(expected: 0.3);
                await Assert.That(model.RowsValues).IsEqualTo(expected: values);
                await Assert.That(model.Built.Count).IsEqualTo(expected: built);
            });

    // The box is 105 high. Row 5 has 5 of its 20 in the box, and the rows after it have nothing.
    [Test]
    public Task ARowThatAControlAboveTheListClipsReportsOnlyThePartInTheBox() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: 10);

                shown.ClipTo(height: 105);

                ReportedRange reported = shown.List.ReportedRange;

                await Assert.That(reported.VisibilityOf(position: 4)).IsEqualTo(expected: 1.0);
                await Assert.That(Part(reported: reported, position: 5)).IsEqualTo(expected: 0.25);
                await Assert.That(reported.VisibilityOf(position: 6)).IsEqualTo(expected: 0.0);
            });

    // The view model reads the rows that show from its source, with their type. They are the rows on the
    // screen after each change of the view and of the collection.
    [Test]
    public Task TheRowsThatTheViewModelReadsAsVisibleAreTheRowsOnTheScreen() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: 10);

                await TheVisibleRowsAreOnTheScreen(model: model, shown: shown);

                // A row at each end shows in part.
                shown.Scroller.Offset = new Vector(x: 0, y: 2007);
                shown.Settle();

                await Assert.That(model.VisibleRows.Sample().Count).IsEqualTo(expected: 11);
                await TheVisibleRowsAreOnTheScreen(model: model, shown: shown);

                // Each position gets the next key.
                model.Remove(key: 0);
                shown.Settle();

                await Assert.That(model.VisibleRows.Sample()[index: 0].Position).IsEqualTo(expected: 101);
                await TheVisibleRowsAreOnTheScreen(model: model, shown: shown);

                shown.Scroller.Offset = new Vector(x: 0, y: 10000);
                shown.Settle();

                await Assert.That(model.VisibleRows.Sample()[index: 0].Position).IsEqualTo(expected: 501);
                await TheVisibleRowsAreOnTheScreen(model: model, shown: shown);
            });

    private static async Task TheVisibleRowsAreOnTheScreen(RecipeModel model, ShownVirtual shown) =>
        await Assert.That(model.VisibleRows.Sample().Select(selector: static row => row.Text))
            .IsEquivalentTo(
                expected: shown.Shown().Select(selector: static pair => pair.Text),
                ordering: CollectionOrdering.Matching);

    // A part is a quotient of two areas, thus the last digits of it are not exact.
    private static double Part(ReportedRange reported, int position) =>
        Math.Round(value: reported.VisibilityOf(position: position), digits: 6);
}
