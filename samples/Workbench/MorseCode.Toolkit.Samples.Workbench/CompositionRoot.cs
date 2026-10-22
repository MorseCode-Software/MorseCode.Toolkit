using Avalonia.Controls;
using SodaFlow.Bindable.ObjectModel;

namespace MorseCode.Toolkit.Samples.Workbench;

/// <summary>
///     The one place that builds the objects of the application and gives each one what it needs.
///     No container is in use: the wiring is code.
/// </summary>
internal static class CompositionRoot
{
    // The collection holds this many items. The list builds a view model for the rows on the screen
    // and for a few rows that left it. Thus, the count does not set the cost.
    private const int TotalCount = 500_000;

    private const int RetainedBeyondTheView = 64;

    /// <summary>
    ///     Builds the main window with its view model.
    /// </summary>
    /// <param name="bindingScheduler">The scheduler that the bindable values use.</param>
    /// <returns>The window.</returns>
    public static Window CreateMainWindow(IBindingScheduler bindingScheduler) =>
        new MainWindow(
            viewModel: WorkbenchViewModel.Create(
                bindingScheduler: bindingScheduler,
                tracker: RowTracker.Create(),
                totalCount: TotalCount,
                retainedBeyondTheView: RetainedBeyondTheView));
}
