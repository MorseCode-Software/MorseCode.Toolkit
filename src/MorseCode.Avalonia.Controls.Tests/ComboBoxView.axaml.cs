using Avalonia.Controls;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     A view that binds a <see cref="BatchingComboBox" /> in XAML, as an
///     application does.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of UserControl does not say what this view holds.
internal sealed partial class ComboBoxView : UserControl
{
    /// <summary>
    ///     Makes the view. InitializeComponent, which Avalonia generates, loads the
    ///     markup and sets a field for each named control.
    /// </summary>
    // ReSharper disable once InheritdocConsiderUsage - The constructor of UserControl does not say what loads the markup.
    public ComboBoxView() => this.InitializeComponent();
}
