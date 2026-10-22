using MorseCode.Mvvm;
using SodaFlow.Bindable.ObjectModel;

namespace MorseCode.Toolkit.Sample;

/// <summary>
///     What a row shows. The list builds one for each row on the screen, and no more.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of IViewModel does not say what a row shows.
internal interface IRowViewModel : IViewModel
{
    /// <summary>
    ///     Gets the position of the row in the whole list.
    /// </summary>
    int Position { get; }

    /// <summary>
    ///     Gets the title of the row.
    /// </summary>
    IOneWayBindableValue<string> Title { get; }

    /// <summary>
    ///     Gets a second line that the row derives from its title.
    /// </summary>
    IOneWayBindableValue<string> Detail { get; }
}
