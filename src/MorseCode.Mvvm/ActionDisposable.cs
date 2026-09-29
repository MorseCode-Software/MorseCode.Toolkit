using System;
using System.Threading;
using JetBrains.Annotations;

namespace MorseCode.Mvvm;

[PublicAPI]
public static partial class Disposable
{
    public static IDisposable FromAction(Action onDispose)
    {
        // The null check occurs here, where the caller is on the stack, and not subsequently in Dispose.
        ArgumentNullException.ThrowIfNull(argument: onDispose);

        return new ActionDisposable(onDispose: onDispose);
    }

    // ReSharper disable once InheritdocConsiderUsage - The summary of IDisposable does not say what this type disposes or in which order.
    private sealed class ActionDisposable : IDisposable
    {
        private readonly Action onDispose;

        private bool disposed;

        /// <summary>Creates a disposable which runs an action.</summary>
        /// <param name="onDispose">The action to run on <see cref="Dispose"/>.</param>
        public ActionDisposable(Action onDispose) => this.onDispose = onDispose;

        /// <inheritdoc />
        /// <remarks>
        ///     The first call runs the dispose action. Each subsequent call does nothing.
        /// </remarks>
        public void Dispose()
        {
            if (Interlocked.Exchange(location1: ref this.disposed, value: true))
            {
                return;
            }

            this.onDispose();
        }
    }
}
