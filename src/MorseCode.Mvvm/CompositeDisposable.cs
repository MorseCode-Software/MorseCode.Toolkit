using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace MorseCode.Mvvm;

public static partial class Disposable
{
    /// <summary>
    ///     Makes one disposable from a fixed list of disposables. A view model keeps
    ///     one for the subscriptions that its construction made, and disposes it in
    ///     its own <see cref="IDisposable.Dispose" /> method.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The first call disposes each entry one time, in the order of the list.
    ///         Each subsequent call does nothing, also when two threads call at the
    ///         same time.
    ///     </para>
    ///     <para>
    ///         An exception from one entry does not stop the disposal of the entries
    ///         after it. When all entries are complete, one exception from the entries
    ///         goes to the caller unchanged.
    ///         Two or more exceptions go to the caller in one
    ///         <see cref="AggregateException" />, in the order of the list.
    ///     </para>
    /// </remarks>
    /// <param name="disposables">
    ///     The entries, in the order of their disposal. The composite keeps a copy,
    ///     thus a subsequent change to the argument has no effect.
    /// </param>
    /// <returns>The composite.</returns>
    /// <exception cref="ArgumentException">An entry is null.</exception>
    public static IDisposable Composite(params IEnumerable<IDisposable> disposables) =>
        new CompositeDisposable(disposables);

    // ReSharper disable once InheritdocConsiderUsage - The summary of IDisposable does not say what this type disposes or in which order.
    private sealed class CompositeDisposable : IDisposable
    {
        private readonly IDisposable[] disposables;

        private int disposed;

        /// <summary>
        ///     Makes a composite of <paramref name="disposables" />.
        /// </summary>
        /// <param name="disposables">
        ///     The entries, in the order of their disposal. The composite keeps a copy,
        ///     thus a subsequent change to the argument has no effect.
        /// </param>
        /// <exception cref="ArgumentException">An entry is null.</exception>
        public CompositeDisposable(IEnumerable<IDisposable> disposables)
        {
            IDisposable[] copy = [.. disposables];

            // A null entry fails here, where its caller is on the stack, and not subsequently in Dispose.
            if (Array.IndexOf(array: copy, value: null) is var index and >= 0)
            {
                throw new ArgumentException(
                    message: $"The entry at index {index} is null.",
                    paramName: nameof(disposables));
            }

            this.disposables = copy;
        }

        /// <inheritdoc />
        /// <remarks>
        ///     The first call disposes each entry. Each subsequent call does nothing. The
        ///     remarks of <see cref="Composite" /> give the order and the result of an
        ///     exception.
        /// </remarks>
        /// <exception cref="AggregateException">Two or more entries threw an exception.</exception>
        public void Dispose()
        {
            if (Interlocked.Exchange(location1: ref this.disposed, value: 1) != 0)
            {
                return;
            }

            List<Exception> exceptions = [];

            foreach (IDisposable disposable in this.disposables)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
            }

            // ReSharper disable once ConvertIfStatementToSwitchStatement - A switch case must end in a jump, and a jump after ExceptionDispatchInfo.Throw is a line that cannot run.
            if (exceptions.Count == 1)
            {
                ExceptionDispatchInfo.Capture(source: exceptions[0]).Throw();
            }

            if (exceptions.Count > 1)
            {
                throw new AggregateException(exceptions);
            }
        }
    }
}
