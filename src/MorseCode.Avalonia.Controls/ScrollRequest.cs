using JetBrains.Annotations;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     A request of a view model to show a position, with the number of the request.
/// </summary>
/// <remarks>
///     <para>
///         A source gives each request the next number. Thus, two requests of one source are never
///         equal, also when they ask for the same position. The control scrolls one time for each
///         value, and reports the value that it handled.
///     </para>
///     <para>
///         The number is necessary. With the position only, a second request for a position equals
///         the first. Then, the control scrolls only if it saw none between the two. A binding
///         thread that takes two changes together shows it only the last.
///     </para>
/// </remarks>
/// <param name="Position">The position to show.</param>
/// <param name="Number">The number of the request in its source. The first request has the number 0.</param>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - The summary of ValueType does not say what a request is.
public readonly record struct ScrollRequest(int Position, long Number);
