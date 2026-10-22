using System.Threading.Tasks;
using Avalonia.Layout;
using Avalonia.Threading;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Toolkit.Sample.Tests;

/// <summary>
///     Checks the window with a real list control, with no screen. These tests show that the
///     bindings of the window connect the control to the view model.
/// </summary>
public sealed class WindowTests
{
    [Test]
    public Task TheWindowBuildsOnlyTheRowsThatFitAndALongJumpBuildsOnlyTheRowsItReaches() =>
        TestApplication.Run(
            test: static async () =>
            {
                IRowTracker tracker = RowTracker.Create();

                IWorkbenchViewModel viewModel =
                    WorkbenchViewModelTests.Create(tracker: tracker, retainedBeyondTheView: 16);

                MainWindow window = new(viewModel: viewModel);
                window.Show();
                Settle(window: window);

                int builtAtStart = WorkbenchViewModelTests.Count(cell: tracker.Built);

                await Assert.That(builtAtStart).IsGreaterThan(minimum: 0);
                await Assert.That(builtAtStart).IsLessThan(maximum: 100);

                window.RowList.ScrollIntoView(index: viewModel.JumpTarget);
                Settle(window: window);

                int built = WorkbenchViewModelTests.Count(cell: tracker.Built);
                int alive = WorkbenchViewModelTests.Count(cell: tracker.Alive);

                // The jump skips 450,000 positions. A list that builds a row for each position that it
                // crosses builds far more than a few hundred rows.
                await Assert.That(built).IsLessThan(maximum: 300);
                await Assert.That(alive).IsLessThan(maximum: 100);

                window.Close();

                await Assert.That(WorkbenchViewModelTests.Count(cell: tracker.Alive)).IsEqualTo(expected: 0);
            });

    private static void Settle(Layoutable window)
    {
        for (int pass = 0; pass < 8; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }
    }
}
