namespace MorseCode.Toolkit.Samples.Workbench;

/// <summary>
///     The part of a row that can change. The collection keeps it apart from the identity of the row.
/// </summary>
/// <param name="Title">The text that the row shows.</param>
internal sealed record RowState(string Title);
