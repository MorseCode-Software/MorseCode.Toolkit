using SodaFlow;

namespace MorseCode.Toolkit.Samples.Workbench;

/// <summary>
///     Counts the view models of the rows. The count shows that the list builds a view model only
///     for a row on the screen.
/// </summary>
internal interface IRowTracker
{
    /// <summary>
    ///     Gets the number of row view models that were built, from the start.
    /// </summary>
    Cell<int> Built { get; }

    /// <summary>
    ///     Gets the number of row view models that are built and not yet disposed.
    /// </summary>
    Cell<int> Alive { get; }

    /// <summary>
    ///     Records that a row view model was built.
    /// </summary>
    void RowBuilt();

    /// <summary>
    ///     Records that a row view model stopped.
    /// </summary>
    void RowReleased();
}
