using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MorseCode.Toolkit.Samples.Workbench;

/// <summary>
///     The main window.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of Window does not say what this class shows.
internal sealed partial class MainWindow : Window
{
    private readonly IWorkbenchViewModel viewModel;

    /// <summary>
    ///     Makes the window.
    /// </summary>
    /// <param name="viewModel">The view model that the window shows.</param>
    // ReSharper disable once InheritdocConsiderUsage - The summary of the base constructor does not say what this one takes.
    public MainWindow(IWorkbenchViewModel viewModel)
    {
        this.viewModel = viewModel;
        this.InitializeComponent();
        this.DataContext = viewModel;
        this.Closed += (_, _) => viewModel.Dispose();
    }

    // The scroll is a gesture of the view and keeps no state. Thus, it does not use the view model.
    private void OnJumpClick(object? sender, RoutedEventArgs e) =>
        this.RowList.ScrollIntoView(index: this.viewModel.JumpTarget);
}
