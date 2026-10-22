using System.Linq;
using System.Threading.Tasks;
using MorseCode.Avalonia.Controls;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Toolkit.Sample.Tests;

/// <summary>
///     Checks the view model. It builds a row view model only for the rows in the range that the
///     view reports. It also releases the row view models when it stops.
/// </summary>
public sealed class WorkbenchViewModelTests
{
    [Test]
    public async Task NoRowIsBuiltBeforeTheViewReportsARange()
    {
        IRowTracker tracker = RowTracker.Create();

        using IWorkbenchViewModel viewModel = Create(tracker: tracker, retainedBeyondTheView: 8);

        await Assert.That(Count(cell: tracker.Built)).IsEqualTo(expected: 0);
        await Assert.That(viewModel.Rows.Value.Items.Count).IsEqualTo(expected: 0);
    }

    [Test]
    public async Task OnlyTheRowsInTheRangeAreBuiltAndTheyStartAtTheOffset()
    {
        IRowTracker tracker = RowTracker.Create();

        using IWorkbenchViewModel viewModel = Create(tracker: tracker, retainedBeyondTheView: 8);

        viewModel.VisibleRange.Value = new ItemRange(First: 400_000, Count: 12);

        IRowViewModel first = viewModel.Rows.Value.Items.OfType<IRowViewModel>().First();

        await Assert.That(Count(cell: tracker.Built)).IsEqualTo(expected: 12);
        await Assert.That(Count(cell: tracker.Alive)).IsEqualTo(expected: 12);
        await Assert.That(viewModel.Rows.Value.Offset).IsEqualTo(expected: 400_000);
        await Assert.That(viewModel.Rows.Value.Items.Count).IsEqualTo(expected: 12);
        await Assert.That(first.Position).IsEqualTo(expected: 400_000);
        await Assert.That(first.Title.Value).IsEqualTo(expected: "Item 400,000");
    }

    [Test]
    public async Task ARowThatLeftTheRangeIsDisposedOnlyAfterTheRetainedRowsFillUp()
    {
        IRowTracker tracker = RowTracker.Create();

        using IWorkbenchViewModel viewModel = Create(tracker: tracker, retainedBeyondTheView: 8);

        viewModel.VisibleRange.Value = new ItemRange(First: 0, Count: 10);
        viewModel.VisibleRange.Value = new ItemRange(First: 100_000, Count: 10);

        // Ten rows left the range, and the map keeps eight of them. Thus, the map releases two.
        await Assert.That(Count(cell: tracker.Built)).IsEqualTo(expected: 20);
        await Assert.That(Count(cell: tracker.Alive)).IsEqualTo(expected: 18);
    }

    [Test]
    public async Task DisposingTheViewModelDisposesEveryRow()
    {
        IRowTracker tracker = RowTracker.Create();
        IWorkbenchViewModel viewModel = Create(tracker: tracker, retainedBeyondTheView: 8);

        viewModel.VisibleRange.Value = new ItemRange(First: 10, Count: 10);
        viewModel.VisibleRange.Value = new ItemRange(First: 5_000, Count: 10);

        viewModel.Dispose();

        await Assert.That(Count(cell: tracker.Alive)).IsEqualTo(expected: 0);
    }

    internal static IWorkbenchViewModel Create(IRowTracker tracker, int retainedBeyondTheView) =>
        WorkbenchViewModel.Create(
            bindingScheduler: BindingScheduler.Immediate,
            tracker: tracker,
            totalCount: 500_000,
            retainedBeyondTheView: retainedBeyondTheView);

    internal static int Count(Cell<int> cell) => Transaction.Run(f: cell.Sample);
}
