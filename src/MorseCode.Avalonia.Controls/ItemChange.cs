namespace MorseCode.Avalonia.Controls;

/// <summary>
///     The item of one position of a <see cref="VirtualItemList" /> before and after
///     an update.
/// </summary>
/// <param name="Index">The position.</param>
/// <param name="Before">The item before the update.</param>
/// <param name="After">The item after the update.</param>
// ReSharper disable once InheritdocConsiderUsage - The summary of ValueType does not say what a change is.
internal readonly record struct ItemChange(int Index, object? Before, object? After);
