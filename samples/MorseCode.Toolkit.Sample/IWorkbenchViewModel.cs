using MorseCode.Avalonia.Controls;
using MorseCode.Mvvm;
using SodaFlow.Bindable.ObjectModel;

namespace MorseCode.Toolkit.Sample;

/// <summary>
///     What the main window shows: a long list, and the counts of the row view models.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of IViewModel does not say what the main window shows.
internal interface IWorkbenchViewModel : IViewModel
{
    /// <summary>
    ///     Gets the number of positions in the whole list.
    /// </summary>
    int TotalCount { get; }

    /// <summary>
    ///     Gets a position far down the list, for the button that scrolls there.
    /// </summary>
    int JumpTarget { get; }

    /// <summary>
    ///     Gets the rows for the range that the view reported last.
    /// </summary>
    IOneWayBindableValue<VirtualRows> Rows { get; }

    /// <summary>
    ///     Gets the range that the view reports. The view writes it, and the view model reads it.
    /// </summary>
    IOneWayToSourceBindableValue<ItemRange> VisibleRange { get; }

    /// <summary>
    ///     Gets the number of row view models built, from the start.
    /// </summary>
    IOneWayBindableValue<int> BuiltRows { get; }

    /// <summary>
    ///     Gets the number of row view models that are not yet disposed.
    /// </summary>
    IOneWayBindableValue<int> AliveRows { get; }
}
