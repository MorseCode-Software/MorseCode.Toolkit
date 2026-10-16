namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     A row that a <see cref="VirtualModel" /> builds. Building one stands for the
///     cost of a heavy view model.
/// </summary>
/// <param name="Position">The position of the row in the whole list.</param>
internal sealed record VirtualRow(int Position)
{
    /// <summary>
    ///     Gets the text that a template shows.
    /// </summary>
    public string Text => $"Row {this.Position}";
}
