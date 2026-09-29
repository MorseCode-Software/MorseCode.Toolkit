using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using JetBrains.Annotations;

namespace MorseCode.Mvvm;

[PublicAPI]
public static partial class Disposable
{
    [PublicAPI]
    public static IDisposable FromAction(Action onDispose) => new ActionDisposable(onDispose);

    // ReSharper disable once InheritdocConsiderUsage - The summary of IDisposable does not say what this type disposes or in which order.
    private sealed class ActionDisposable : IDisposable
    {
        private readonly Action onDispose;

        private bool disposed;

        /// <summary>Creates a disposable which runs an action.</summary>
        /// <param name="onDispose">The action to run on <see cref="Dispose"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="onDispose"/> is null.</exception>
        public ActionDisposable(Action onDispose) =>
            this.onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));

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
