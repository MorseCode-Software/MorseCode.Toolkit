using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using SodaFlow;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     Checks how much of its first and of its last visible row a shown
///     <see cref="VirtualizingItemsControl" /> reports. The window is 200 high and each row is 20 high.
/// </summary>
public sealed class VirtualizingItemsControlVisibilityTests
{
    [Test]
    public Task AListAtRestReportsItsFirstAndItsLastRowInFull() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: 10);

                ReportedRange reported = shown.List.ReportedRange;

                await Assert.That(reported.Visible).IsEqualTo(expected: new ItemRange(First: 0, Count: 10));
                await Assert.That(reported.FirstVisibilityAmount).IsEqualTo(expected: 1.0);
                await Assert.That(reported.LastVisibilityAmount).IsEqualTo(expected: 1.0);
            });

    // Row 0 has 13 of its 20 in the viewport, and row 10 has 7.
    [Test]
    public Task ARowThatAnEndOfTheViewportCutsReportsThePartOfItThatShows() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: 10);

                shown.Scroller.Offset = new Vector(x: 0, y: 7);
                shown.Settle();

                ReportedRange reported = shown.List.ReportedRange;

                await Assert.That(reported.Visible).IsEqualTo(expected: new ItemRange(First: 0, Count: 11));
                await Assert.That(Rounded(amount: reported.FirstVisibilityAmount)).IsEqualTo(expected: 0.65);
                await Assert.That(Rounded(amount: reported.LastVisibilityAmount)).IsEqualTo(expected: 0.35);
            });

    // A scroll that brings no new row changes no container. The control reports the new amounts, and
    // the view model sends no rows for that report.
    [Test]
    public Task AScrollInsideARowReportsTheNewAmountsAndBringsNoRows() =>
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
                await Assert.That(Rounded(amount: before.FirstVisibilityAmount)).IsEqualTo(expected: 0.85);
                await Assert.That(Rounded(amount: before.LastVisibilityAmount)).IsEqualTo(expected: 0.15);
                await Assert.That(Rounded(amount: after.FirstVisibilityAmount)).IsEqualTo(expected: 0.7);
                await Assert.That(Rounded(amount: after.LastVisibilityAmount)).IsEqualTo(expected: 0.3);
                await Assert.That(model.RowsValues).IsEqualTo(expected: values);
                await Assert.That(model.Built.Count).IsEqualTo(expected: built);
            });

    // The box is 105 high. Row 5 is the last row that shows, and it has 5 of its 20 in the box.
    [Test]
    public Task ARowThatAControlAboveTheListClipsReportsOnlyThePartInTheBox() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: 10);

                shown.ClipTo(height: 105, hiddenAtTheTop: 0);

                ReportedRange reported = shown.List.ReportedRange;

                await Assert.That(reported.Visible).IsEqualTo(expected: new ItemRange(First: 0, Count: 6));
                await Assert.That(reported.FirstVisibilityAmount).IsEqualTo(expected: 1.0);
                await Assert.That(Rounded(amount: reported.LastVisibilityAmount)).IsEqualTo(expected: 0.25);
            });

    // The box shows the list from 45 to 150. Rows 0 and 1 are above the box, row 2 has 15 of its 20 in
    // the box, and row 7 has 10. The panel can keep the containers of the rows above the box.
    [Test]
    public Task ARowThatAControlAboveTheListHidesIsNotARowThatShows() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: 10);

                shown.ClipTo(height: 105, hiddenAtTheTop: 45);

                ReportedRange reported = shown.List.ReportedRange;

                await Assert.That(reported.Visible).IsEqualTo(expected: new ItemRange(First: 2, Count: 6));
                await Assert.That(Rounded(amount: reported.FirstVisibilityAmount)).IsEqualTo(expected: 0.75);
                await Assert.That(Rounded(amount: reported.LastVisibilityAmount)).IsEqualTo(expected: 0.5);

                // The rows of the positions with a container stay, thus a row that the box hides is there
                // when the box moves.
                await Assert.That(reported.Constructed.First).IsEqualTo(expected: 0);
            });

    // The box is 10 high, thus one row shows, and half of it. It is the first row and the last row.
    [Test]
    public Task OneRowThatShowsGivesTheSameAmountTwoTimes() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: 10);

                shown.ClipTo(height: 10, hiddenAtTheTop: 0);

                ReportedRange reported = shown.List.ReportedRange;

                await Assert.That(reported.Visible).IsEqualTo(expected: new ItemRange(First: 0, Count: 1));
                await Assert.That(Rounded(amount: reported.FirstVisibilityAmount)).IsEqualTo(expected: 0.5);
                await Assert.That(Rounded(amount: reported.LastVisibilityAmount)).IsEqualTo(expected: 0.5);
            });

    [Test]
    public Task AnEmptyListShowsNothing() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 0, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: 10);

                ReportedRange reported = shown.List.ReportedRange;

                await Assert.That(reported.Visible).IsEqualTo(expected: ItemRange.Empty);
                await Assert.That(reported.FirstVisibilityAmount).IsEqualTo(expected: 0.0);
                await Assert.That(reported.LastVisibilityAmount).IsEqualTo(expected: 0.0);
            });

    // In a list from left to right, an amount is a part of the width of a row. The test finds that part
    // from the place of the container, because the width of a row is the width of its text.
    [Test]
    public Task AListFromLeftToRightReportsPartsOfAWidth() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);

                using ShownVirtual shown =
                    new(
                        model: model,
                        buffer: 10,
                        panel: static () => new VirtualizingStackPanel { Orientation = Orientation.Horizontal });

                ScrollViewer.SetHorizontalScrollBarVisibility(control: shown.List, value: ScrollBarVisibility.Auto);
                shown.Settle();

                const double offset = 7;
                shown.Scroller.Offset = new Vector(x: offset, y: 0);
                shown.Settle();

                ReportedRange reported = shown.List.ReportedRange;
                Rect first = BoundsAt(shown: shown, position: reported.Visible.First);
                Rect last = BoundsAt(shown: shown, position: reported.Visible.First + reported.Visible.Count - 1);
                double end = offset + shown.Scroller.Viewport.Width;

                await Assert.That(reported.Visible.First).IsEqualTo(expected: 0);
                await Assert.That(reported.FirstVisibilityAmount).IsLessThan(maximum: 1.0);

                await Assert.That(Rounded(amount: reported.FirstVisibilityAmount))
                    .IsEqualTo(expected: Rounded(amount: (first.Right - offset) / first.Width));

                await Assert.That(Rounded(amount: reported.LastVisibilityAmount))
                    .IsEqualTo(expected: Rounded(amount: Math.Min(val1: 1.0, val2: (end - last.X) / last.Width)));
            });

    // The view model reads the rows that show from its source, with their type. It also reads the
    // amounts of the first and of the last of them. They are what is on the screen after each change
    // of the view and of the collection.
    [Test]
    public Task TheRowsThatTheViewModelReadsAsVisibleAreTheRowsOnTheScreen() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: 10);

                await TheVisibleRowsAreOnTheScreen(model: model, shown: shown);
                await Assert.That(model.VisibilityAmounts).IsEqualTo(expected: (1.0, 1.0));

                // A row at each end shows in part.
                shown.Scroller.Offset = new Vector(x: 0, y: 2007);
                shown.Settle();

                await Assert.That(model.VisibleRows.Sample().Count).IsEqualTo(expected: 11);
                await TheVisibleRowsAreOnTheScreen(model: model, shown: shown);
                await Assert.That(Rounded(amount: model.VisibilityAmounts.First)).IsEqualTo(expected: 0.65);
                await Assert.That(Rounded(amount: model.VisibilityAmounts.Last)).IsEqualTo(expected: 0.35);

                // Each position gets the next key.
                model.Remove(key: 0);
                shown.Settle();

                await Assert.That(model.VisibleRows.Sample()[index: 0].Position).IsEqualTo(expected: 101);
                await TheVisibleRowsAreOnTheScreen(model: model, shown: shown);

                shown.Scroller.Offset = new Vector(x: 0, y: 10000);
                shown.Settle();

                await Assert.That(model.VisibleRows.Sample()[index: 0].Position).IsEqualTo(expected: 501);
                await TheVisibleRowsAreOnTheScreen(model: model, shown: shown);

                // A control above the list shows 105 px of it: five rows, and 5 px of the row after them.
                shown.ClipTo(height: 105, hiddenAtTheTop: 0);

                await Assert.That(model.VisibleRows.Sample().Count).IsEqualTo(expected: 6);
                await Assert.That(Rounded(amount: model.VisibilityAmounts.Last)).IsEqualTo(expected: 0.25);
            });

    private static async Task TheVisibleRowsAreOnTheScreen(RecipeModel model, ShownVirtual shown) =>
        await Assert.That(model.VisibleRows.Sample().Select(selector: static row => row.Text))
            .IsEquivalentTo(
                expected: shown.Shown().Select(selector: static pair => pair.Text),
                ordering: CollectionOrdering.Matching);

    private static Rect BoundsAt(ShownVirtual shown, int position) =>
        shown.List.ContainerFromIndex(index: position)?.Bounds ?? default;

    // An amount is a quotient of two lengths, thus the last digits of it are not exact.
    private static double Rounded(double amount) => Math.Round(value: amount, digits: 6);
}
