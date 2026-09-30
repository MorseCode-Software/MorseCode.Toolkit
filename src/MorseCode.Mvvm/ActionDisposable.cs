using System;
using System.Threading;
using JetBrains.Annotations;

namespace MorseCode.Mvvm;

/// <summary>
///     Makes the usual <see cref="IDisposable" /> objects: one that calls an action, one that disposes a
///     list, and one that does nothing.
/// </summary>
[PublicAPI]
public static partial class Disposable
{
    /// <summary>
    ///     Makes a disposable that calls <paramref name="onDispose" /> at the first call to its
    ///     <see cref="IDisposable.Dispose" /> method.
    /// </summary>
    /// <remarks>
    ///     Each subsequent call does nothing, also when two threads call at the same time.
    /// </remarks>
    /// <param name="onDispose">The action that releases the resource.</param>
    /// <returns>The disposable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onDispose" /> is null.</exception>
    public static IDisposable FromAction(Action onDispose) =>
        // The null check occurs here, where the caller is on the stack, and not subsequently in Dispose.
        new ActionDisposable(onDispose: onDispose ?? throw new ArgumentNullException(paramName: nameof(onDispose)));

    // ReSharper disable once InheritdocConsiderUsage - The summary of IDisposable does not say what this type disposes or in which order.
    private sealed class ActionDisposable : IDisposable
    {
        private readonly Action onDispose;

        private int disposed;

        /// <summary>Creates a disposable which runs an action.</summary>
        /// <param name="onDispose">The action to run on <see cref="Dispose"/>.</param>
        public ActionDisposable(Action onDispose) => this.onDispose = onDispose;

        /// <inheritdoc />
        /// <remarks>
        ///     The first call runs the dispose action. Each subsequent call does nothing.
        /// </remarks>
        public void Dispose()
        {
            if (Interlocked.Exchange(location1: ref this.disposed, value: 1) != 0)
            {
                return;
            }

            this.onDispose();
        }
    }
}
