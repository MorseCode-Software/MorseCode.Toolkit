using Avalonia.Controls;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     A view that binds the selected item of a <see cref="ComboBox" /> to a
///     <see cref="SodaFlow.Functional.Maybe{T}" /> through
///     <see cref="MaybeConverter" />, in XAML, as an application does.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of UserControl does not say what this view holds.
internal sealed partial class MaybeComboBoxView : UserControl
{
    /// <summary>
    ///     Makes the view. InitializeComponent, which Avalonia generates, loads the
    ///     markup and sets a field for each named control.
    /// </summary>
    // ReSharper disable once InheritdocConsiderUsage - The constructor of UserControl does not say what loads the markup.
    public MaybeComboBoxView() => this.InitializeComponent();
}
