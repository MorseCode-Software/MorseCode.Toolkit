using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Mvvm.Tests;

public sealed class ViewModelBaseConcurrencyTests
{
    private const int Iterations = 200;

    private const int Threads = 8;

    // Threads add disposables to the registrations without a stop while the constructing thread calls
    // Construct. Dispose must release each accepted registration one time. A refused registration
    // must stay not disposed. An accepted registration that Dispose does not release is a leak.
    [Test]
    public async Task ConcurrentRegistrationIsDisposedOrRefusedAndDoesNotLeak()
    {
        int accepted = 0;
        int refused = 0;
        int leaked = 0;
        int refusedAndDisposed = 0;

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            ConcurrentBag<Entry> acceptedEntries = [];
            ConcurrentBag<Entry> refusedEntries = [];
            Race race = new(threads: Threads);

            ViewModelBase viewModel = ViewModelBase.CreateBase(
                bindingScheduler: BindingScheduler.Immediate,
                continuation: (output, construct) =>
                {
                    race.Start(
                        register: () =>
                        {
                            Entry entry = new();

                            try
                            {
                                output.AddDisposable(disposable: entry);
                                acceptedEntries.Add(item: entry);

                                return true;
                            }
                            catch (InvalidOperationException)
                            {
                                refusedEntries.Add(item: entry);

                                return false;
                            }
                        });

                    return construct.Construct(constructor: static viewModelBase => viewModelBase);
                });

            await race.Completion;
            viewModel.Dispose();

            accepted += acceptedEntries.Count;
            refused += refusedEntries.Count;
            leaked += acceptedEntries.Count(predicate: static entry => entry.Disposals != 1);
            refusedAndDisposed += refusedEntries.Count(predicate: static entry => entry.Disposals != 0);
        }

        await Assert.That(leaked).IsEqualTo(expected: 0);
        await Assert.That(refusedAndDisposed).IsEqualTo(expected: 0);

        // Each thread stops at its first refusal. Thus, each thread of each iteration found the seal.
        await Assert.That(refused).IsEqualTo(expected: Iterations * Threads);
        await Assert.That(accepted).IsGreaterThan(minimum: 0);
    }

    // The same race through the factory. Dispose of the view model must release each action that the
    // factory returns.
    [Test]
    public async Task ConcurrentBindableIsDisposedOrRefusedAndDoesNotLeak()
    {
        int accepted = 0;
        int leaked = 0;

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            StreamSink<Unit> firings = Stream.CreateSink<Unit>();
            ConcurrentBag<IBindableAction> acceptedActions = [];
            Race race = new(threads: Threads);

            ViewModelBase viewModel = ViewModelBase.CreateBase(
                bindingScheduler: BindingScheduler.Immediate,
                continuation: (output, construct) =>
                {
                    race.Start(
                        register: () =>
                        {
                            try
                            {
                                acceptedActions.Add(
                                    item: output.BindableFactory.CreateBindableAction(firingsStreamSink: firings));

                                return true;
                            }
                            catch (InvalidOperationException)
                            {
                                return false;
                            }
                        });

                    return construct.Construct(constructor: static viewModelBase => viewModelBase);
                });

            await race.Completion;
            viewModel.Dispose();

            accepted += acceptedActions.Count;
            leaked += acceptedActions.Count(predicate: static action => action.CanExecute(parameter: null));
        }

        await Assert.That(leaked).IsEqualTo(expected: 0);
        await Assert.That(accepted).IsGreaterThan(minimum: 0);
    }

    // The mapped cell gets its first value when SodaFlow first samples it. In one transaction, that
    // sample occurs while the factory makes the one-way value, as a view model does in its own
    // Transaction.Run. The function of the cell calls Construct there. Thus, the seal occurs between the
    // two checks of the factory. A one-way value that stays attached posts to the scheduler when its
    // cell changes.
    //
    // A one-way value holds its listener weakly. The garbage collector can remove an attached one-way
    // value that nothing refers to, and then the test cannot see it. Thus, the test prevents garbage
    // collection until it reads the scheduler. That prevention applies to the full process, thus the
    // test runs alone.
    [Test]
    [NotInParallel]
    public async Task ConstructWhileTheFactoryMakesAnObjectDisposesTheObject()
    {
        CellSink<int> source = Cell.CreateSink(initialValue: 1);
        CountingScheduler scheduler = new();
        List<string> log = [];
        List<Exception> caught = [];
        int postsBeforeTheChange;
        int postsAfterTheChange;
        bool collectionPrevented = GC.TryStartNoGCRegion(totalSize: 16 * 1024 * 1024);
        bool collectionPreventedToTheEnd;
        ViewModelBase viewModel;

        try
        {
            viewModel = Transaction.Run(
                f: () => ViewModelBase.CreateBase(
                    bindingScheduler: scheduler,
                    continuation: (output, construct) =>
                    {
                        List<ViewModelBase> constructed = [];

                        Cell<int> sealsWhenSampled = source.Map(
                            f: value =>
                            {
                                if (constructed.Count == 0)
                                {
                                    log.Add(item: "construct");

                                    constructed.Add(
                                        item: construct.Construct(constructor: static viewModelBase => viewModelBase));
                                }

                                return value;
                            });

                        log.Add(item: "factory call starts");

                        caught.Add(
                            item: Catch(action: () => output.BindableFactory.CreateOneWay(cell: sealsWhenSampled)));

                        log.Add(item: "factory call ends");

                        return constructed[0];
                    }));

            postsBeforeTheChange = scheduler.Posts;
            source.Send(a: 2);
            postsAfterTheChange = scheduler.Posts;
        }
        finally
        {
            collectionPreventedToTheEnd = GCSettings.LatencyMode == GCLatencyMode.NoGCRegion;

            if (collectionPreventedToTheEnd)
            {
                GC.EndNoGCRegion();
            }
        }

        await Assert.That(collectionPrevented).IsTrue();
        await Assert.That(collectionPreventedToTheEnd).IsTrue();

        await Assert
            .That(log)
            .IsEquivalentTo(
                expected: ["factory call starts", "construct", "factory call ends"],
                ordering: CollectionOrdering.Matching);

        await Assert.That(caught[0]).IsTypeOf<InvalidOperationException>();
        await Assert.That(caught[0].Message).Contains(expected: "registrations are closed");
        await Assert.That(postsAfterTheChange).IsEqualTo(expected: postsBeforeTheChange);

        viewModel.Dispose();
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

    // Starts the threads that add registrations, and returns when they overlap with the caller. Each
    // thread waits for one start signal, and then adds registrations until one fails. The caller calls Construct
    // after some registrations occur, thus the seal falls between registrations on the other threads.
    private sealed class Race(in int threads)
    {
        private readonly int threads = threads;

        private int attempts;

        private int ready;

        private Task[] tasks = [];

        public Task Completion => Task.WhenAll(tasks: this.tasks);

        public void Start(Func<bool> register)
        {
            TaskCompletionSource<bool> start = new();

            this.tasks =
            [
                .. Enumerable
                    .Range(start: 0, count: this.threads)
                    .Select(_ =>
                        Task.Factory.StartNew(
                            action: () =>
                            {
                                Interlocked.Increment(location: ref this.ready);
                                start.Task.Wait();

                                do
                                {
                                    Interlocked.Increment(location: ref this.attempts);
                                }
                                while (register());
                            },
                            creationOptions: TaskCreationOptions.LongRunning))
            ];

            SpinWait.SpinUntil(condition: () => Volatile.Read(location: ref this.ready) == this.threads);
            start.SetResult(result: true);
            SpinWait.SpinUntil(condition: () => Volatile.Read(location: ref this.attempts) >= this.threads * 4);
        }
    }

    private sealed class Entry : IDisposable
    {
        private int disposals;

        public int Disposals => Volatile.Read(location: ref this.disposals);

        public void Dispose() => Interlocked.Increment(location: ref this.disposals);
    }

    private sealed class CountingScheduler : IBindingScheduler
    {
        private int posts;

        public int Posts => Volatile.Read(location: ref this.posts);

        public bool CheckAccess() => true;

        public void Post(Action action) => Interlocked.Increment(location: ref this.posts);
    }
}
