using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using JetBrains.Annotations;

namespace MorseCode.Mvvm;

/// <summary>
///     Disposes a fixed list of disposables as one. A view model keeps one of these for the
///     subscriptions that its construction made, and its <see cref="IDisposable.Dispose" /> calls
///     <see cref="Dispose" />.
/// </summary>
/// <remarks>
///     <para>
///         The first call disposes each entry one time, in the order of the list. Each subsequent
///         call does nothing, also when two threads call at the same time.
///     </para>
///     <para>
///         An exception from one entry does not stop the disposal of the entries after it. When
///         all entries are complete, one exception from the entries goes to the caller unchanged.
///         Two or more exceptions go to the caller in one <see cref="AggregateException" />, in
///         the order of the list.
///     </para>
/// </remarks>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - The summary of IDisposable does not say what this type disposes or in which order.
public sealed class CompositeDisposable : IDisposable
{
    private readonly IDisposable[] disposables;

    private bool disposed;

    /// <summary>
    ///     Makes a composite of <paramref name="disposables" />.
    /// </summary>
    /// <param name="disposables">
    ///     The entries, in the order of their disposal. The composite keeps a copy, thus a
    ///     subsequent change to the argument has no effect.
    /// </param>
    /// <exception cref="ArgumentException">An entry is null.</exception>
    public CompositeDisposable(params IReadOnlyList<IDisposable> disposables)
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
    ///     The first call disposes each entry. Each subsequent call does nothing. The remarks of
    ///     <see cref="CompositeDisposable" /> give the order and the result of an exception.
    /// </remarks>
    /// <exception cref="AggregateException">Two or more entries threw an exception.</exception>
    public void Dispose()
    {
        if (Interlocked.Exchange(location1: ref this.disposed, value: true))
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

        switch (exceptions.Count)
        {
            case 0:
                return;
            case 1:
                ExceptionDispatchInfo.Throw(exceptions[0]);
                return;
            default:
                throw new AggregateException(exceptions);
        }
    }
}
