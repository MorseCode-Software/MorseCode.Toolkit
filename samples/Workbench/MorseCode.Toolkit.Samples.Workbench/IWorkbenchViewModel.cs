using MorseCode.Avalonia.Controls;
using MorseCode.Mvvm;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Toolkit.Samples.Workbench;

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
    ///     Gets the action that asks the list to scroll far down. The button of the window runs it.
    /// </summary>
    IBindableAction Jump { get; }

    /// <summary>
    ///     Gets the position that the view model wants to show, or none. The list scrolls there and
    ///     writes none back.
    /// </summary>
    ITwoWayBindableValue<Maybe<int>> ScrollRequest { get; }

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
