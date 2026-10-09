namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     Closes <see cref="MaybeConverter{T,TSelf}" /> for strings, as an
///     application closes it for each type that a two-way binding uses.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of MaybeConverter<T, TSelf> does not say which type the tests close it for.
internal sealed class StringMaybeConverter : MaybeConverter<string, StringMaybeConverter>;
