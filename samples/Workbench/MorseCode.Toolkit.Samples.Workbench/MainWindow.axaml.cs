using Avalonia.Controls;

namespace MorseCode.Toolkit.Samples.Workbench;

/// <summary>
///     The main window. It holds only markup. <see cref="App" /> gives the data context before the
///     window shows.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of Window does not say what this class shows.
internal sealed partial class MainWindow : Window
{
    /// <summary>
    ///     Makes the window. The markup loads here.
    /// </summary>
    // ReSharper disable once InheritdocConsiderUsage - The summary of the base constructor does not say what this one loads.
    public MainWindow() => this.InitializeComponent();
}
