using Avalonia.Controls;

namespace MorseCode.Toolkit.Samples.Workbench;

/// <summary>
///     The main window.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of Window does not say what this class shows.
internal sealed partial class MainWindow : Window
{
    /// <summary>
    ///     Makes the window.
    /// </summary>
    /// <param name="viewModel">The view model that the window shows.</param>
    // ReSharper disable once InheritdocConsiderUsage - The summary of the base constructor does not say what this one takes.
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor - The window shows an IWorkbenchViewModel, which x:DataType names, so the constructor takes that type.
    public MainWindow(IWorkbenchViewModel viewModel)
    {
        this.InitializeComponent();
        this.DataContext = viewModel;
        this.Closed += (_, _) => viewModel.Dispose();
    }
}
