using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Mvvm.Tests;

public sealed class CompositeDisposableTests
{
    [Test]
    public async Task DisposesEachEntryOnceInListOrder()
    {
        List<string> log = [];

        CompositeDisposable composite =
            new(disposables: [new Recording(log: log, name: "A"), new Recording(log: log, name: "B"), new Recording(log: log, name: "C")]);

        composite.Dispose();

        await Assert.That(log).IsEquivalentTo(expected: ["A", "B", "C"], ordering: CollectionOrdering.Matching);
    }

    [Test]
    public async Task SecondDisposeDoesNothing()
    {
        List<string> log = [];

        CompositeDisposable composite =
            new(disposables: [new Recording(log: log, name: "A"), new Recording(log: log, name: "B")]);

        composite.Dispose();
        composite.Dispose();

        await Assert.That(log).IsEquivalentTo(expected: ["A", "B"], ordering: CollectionOrdering.Matching);
    }

    [Test]
    public async Task ConcurrentDisposeDisposesEachEntryOnce()
    {
        const int threads = 16;
        Counting entry = new();
        CompositeDisposable composite = new(disposables: [entry]);

        // Each thread starts, adds one to the count, and waits for the start signal. The test sends
        // the signal when the count is equal to the number of threads. Thus, the calls overlap and
        // do not occur in sequence.
        int ready = 0;
        TaskCompletionSource start = new();

        Task[] calls =
        [
            .. Enumerable
                .Range(start: 0, count: threads)
                .Select(_ => Task.Factory.StartNew(
                    action: () =>
                    {
                        Interlocked.Increment(location: ref ready);
                        start.Task.Wait();
                        composite.Dispose();
                    },
                    creationOptions: TaskCreationOptions.LongRunning))
        ];

        SpinWait.SpinUntil(condition: () => Volatile.Read(location: ref ready) == threads);
        start.SetResult();

        await Task.WhenAll(tasks: calls);

        await Assert.That(entry.Count).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task OneFailureDisposesTheRestAndRethrowsThatException()
    {
        List<string> log = [];
        InvalidOperationException failure = new(message: "B failed.");

        CompositeDisposable composite =
            new(disposables: [new Recording(log: log, name: "A"), new Throwing(exception: failure), new Recording(log: log, name: "C")]);

        Exception caught = Catch(action: composite.Dispose);

        await Assert.That(caught).IsSameReferenceAs(expected: failure);
        await Assert.That(log).IsEquivalentTo(expected: ["A", "C"], ordering: CollectionOrdering.Matching);
    }

    [Test]
    public async Task TwoFailuresThrowOneAggregateInListOrder()
    {
        List<string> log = [];
        Exception first = new InvalidOperationException(message: "A failed.");
        Exception second = new InvalidOperationException(message: "C failed.");

        CompositeDisposable composite =
            new(disposables: [new Throwing(exception: first), new Recording(log: log, name: "B"), new Throwing(exception: second)]);

        Exception caught = Catch(action: composite.Dispose);

        await Assert.That(caught).IsTypeOf<AggregateException>();

        await Assert
            .That(((AggregateException)caught).InnerExceptions)
            .IsEquivalentTo(expected: [first, second], ordering: CollectionOrdering.Matching);

        await Assert.That(log).IsEquivalentTo(expected: ["B"], ordering: CollectionOrdering.Matching);
    }

    [Test]
    public async Task NullEntryFailsAtConstruction()
    {
        List<string> log = [];

        // ReSharper disable once NullableWarningSuppressionIsUsed - The null entry is the input under test: the constructor must refuse it at run time.
        IDisposable[] entries = [new Recording(log: log, name: "A"), null!];

        Exception caught = Catch(action: () => _ = new CompositeDisposable(disposables: entries));

        await Assert.That(caught).IsTypeOf<ArgumentException>();
        await Assert.That(caught.Message).Contains(expected: "index 1");
    }

    [Test]
    public async Task LaterChangeToTheArgumentHasNoEffect()
    {
        List<string> log = [];
        IDisposable[] entries = [new Recording(log: log, name: "A"), new Recording(log: log, name: "B")];
        CompositeDisposable composite = new(disposables: entries);

        entries[1] = new Recording(log: log, name: "replacement");
        composite.Dispose();

        await Assert.That(log).IsEquivalentTo(expected: ["A", "B"], ordering: CollectionOrdering.Matching);
    }

    [Test]
    public async Task EmptyCompositeDisposesWithoutError()
    {
        CompositeDisposable composite = new();

        composite.Dispose();

        await Assert.That(composite).IsNotNull();
    }

    // The loop that each sample view model in SodaFlow writes by hand, which this type replaces. Over
    // entries that do not throw, the two dispose the same entries in the same order, for each list
    // length up to five.
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    public async Task MatchesTheLoopItReplacesWhenNoEntryThrows(int length)
    {
        List<string> byLoop = [];
        List<string> byComposite = [];
        string[] names = [.. Enumerable.Range(start: 0, count: length).Select(static i => $"entry {i}")];

        IReadOnlyList<IDisposable> loopEntries = [.. names.Select(n => new Recording(log: byLoop, name: n))];

        foreach (IDisposable disposable in loopEntries)
        {
            disposable.Dispose();
        }

        new CompositeDisposable(disposables: [.. names.Select(n => new Recording(log: byComposite, name: n))]).Dispose();

        await Assert.That(byComposite).IsEquivalentTo(expected: byLoop, ordering: CollectionOrdering.Matching);
    }

    // The two are different when an entry fails. The loop stops at the first failure, thus each
    // entry after it keeps its subscription. That is the cause for the replacement.
    [Test]
    public async Task DisposesTheEntriesThatTheLoopLeaksAfterAFailure()
    {
        List<string> byLoop = [];
        List<string> byComposite = [];
        InvalidOperationException failure = new(message: "B failed.");

        IReadOnlyList<IDisposable> loopEntries =
            [new Recording(log: byLoop, name: "A"), new Throwing(exception: failure), new Recording(log: byLoop, name: "C")];

        _ = Catch(
            action: () =>
            {
                foreach (IDisposable disposable in loopEntries)
                {
                    disposable.Dispose();
                }
            });

        CompositeDisposable composite =
            new(disposables: [new Recording(log: byComposite, name: "A"), new Throwing(exception: failure), new Recording(log: byComposite, name: "C")]);

        _ = Catch(action: composite.Dispose);

        await Assert.That(byLoop).IsEquivalentTo(expected: ["A"], ordering: CollectionOrdering.Matching);
        await Assert.That(byComposite).IsEquivalentTo(expected: ["A", "C"], ordering: CollectionOrdering.Matching);
    }

    private static Exception Catch(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new InvalidOperationException(message: "The action did not throw an exception.");
    }

    private sealed class Recording(ICollection<string> log, string name) : IDisposable
    {
        private ICollection<string> Log { get; } = log;

        private string Name { get; } = name;

        public void Dispose() => this.Log.Add(this.Name);
    }

    private sealed class Throwing(Exception exception) : IDisposable
    {
        private Exception Exception { get; } = exception;

        public void Dispose() => throw this.Exception;
    }

    private sealed class Counting : IDisposable
    {
        private int count;

        public int Count => Volatile.Read(location: ref this.count);

        public void Dispose() => Interlocked.Increment(location: ref this.count);
    }
}
