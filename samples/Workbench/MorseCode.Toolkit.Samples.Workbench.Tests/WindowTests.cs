using System.Threading.Tasks;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using SodaFlow.Functional;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Toolkit.Samples.Workbench.Tests;

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

                MainWindow window = new() { DataContext = viewModel };
                window.Show();
                Settle(window: window);

                int builtAtStart = WorkbenchViewModelTests.Count(cell: tracker.Built);

                await Assert.That(builtAtStart).IsGreaterThan(minimum: 0);
                await Assert.That(builtAtStart).IsLessThan(maximum: 100);

                // A click on the button runs the action of the view model. The view model asks for the
                // scroll, the list scrolls, and the list clears the request.
                // A press and a release of the left button over the middle of the button, as a user does.
                Point middle =
                    window.JumpButton.TranslatePoint(
                        point: new Point(x: window.JumpButton.Bounds.Width / 2, y: window.JumpButton.Bounds.Height / 2),
                        relativeTo: window)
                    ?? default;

                window.MouseDown(point: middle, button: MouseButton.Left);
                window.MouseUp(point: middle, button: MouseButton.Left);
                Settle(window: window);

                await Assert.That(viewModel.ScrollRequest.Value).IsEqualTo(expected: Maybe<int>.None);

                // The list is at the end of the jump, 450,000 positions from the start.
                await Assert.That(window.RowList.VisibleRange.First).IsGreaterThan(minimum: 449_000);

                int built = WorkbenchViewModelTests.Count(cell: tracker.Built);
                int alive = WorkbenchViewModelTests.Count(cell: tracker.Alive);

                // The jump skips 450,000 positions. A list that builds a row for each position that it
                // crosses builds far more than a few hundred rows.
                await Assert.That(built).IsLessThan(maximum: 300);
                await Assert.That(alive).IsLessThan(maximum: 100);

                // The application releases the view model when it exits. The test does the same.
                window.Close();
                viewModel.Dispose();

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
