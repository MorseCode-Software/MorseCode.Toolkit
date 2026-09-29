using System;

namespace MorseCode.Mvvm;

public static partial class Disposable
{
    /// <summary>
    ///     A disposable that does nothing. Use it where a disposable is necessary and there is nothing
    ///     to release.
    /// </summary>
    public static readonly IDisposable Empty = new EmptyDisposable();

    // ReSharper disable once InheritdocConsiderUsage - The summary of IDisposable does not say what this type disposes or in which order.
    private sealed class EmptyDisposable : IDisposable
    {
        /// <inheritdoc />
        /// <remarks>
        ///     This method does nothing.
        /// </remarks>
        public void Dispose()
        {
        }
    }
}
