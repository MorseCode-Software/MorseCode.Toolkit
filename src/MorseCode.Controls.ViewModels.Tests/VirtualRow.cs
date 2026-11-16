namespace MorseCode.Controls.ViewModels.Tests;

/// <summary>
///     A row of a source in these tests. It is a record. Thus, two rows with the same position are
///     equal, and a test can tell a comparison of values from a comparison of references.
/// </summary>
/// <param name="Position">The position of the row in the whole list.</param>
internal sealed record VirtualRow(int Position);
