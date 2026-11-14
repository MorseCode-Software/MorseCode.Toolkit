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

            ViewModelBase viewModel =
                ViewModelBase.CreateBase(
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

            ViewModelBase viewModel =
                ViewModelBase.CreateBase(
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

    // SodaFlow runs one transaction at a time across the process. The constructing thread is in a
    // transaction, as a view model is in its own Transaction.Run. A second thread calls the factory. It
    // goes through the first check of the factory, and then it waits in SodaFlow for that transaction.
    // The constructing thread calls Construct during that wait. Thus, the seal occurs between the two
    // checks of the factory. The test does not use the moment at which SodaFlow first runs the function
    // of a cell, which is different between versions of SodaFlow. A one-way value that stays attached
    // posts to the scheduler when its cell changes.
    //
    // A one-way value holds its listener weakly. The garbage collector can remove an attached one-way
    // value that nothing refers to, and then the test cannot see it. Thus, the test prevents garbage
    // collection until it reads the scheduler. That prevention applies to the full process, thus the
    // test runs alone.
    //
    // The runtime ends the region without a signal when the process allocates more than the budget. The
    // test host allocates too, and the test does not control those allocations. A region that ended
    // early does not show a defect, but it makes a pass uncertain. Thus, the test reports a skip then,
    // and the budget is large so that this is rare.
    [Test]
    [NotInParallel]
    public async Task ConstructWhileTheFactoryMakesAnObjectDisposesTheObject()
    {
        CellSink<int> source = Cell.CreateSink(initialValue: 1);
        CountingScheduler scheduler = new();
        List<Thread> factoryCalls = [];
        List<Exception> caught = [];
        bool waitedInTheFactory = false;
        int postsBeforeTheChange;
        int postsAfterTheChange;
        bool collectionPrevented = GC.TryStartNoGCRegion(totalSize: 64 * 1024 * 1024);
        bool collectionPreventedToTheEnd;
        ViewModelBase viewModel;

        try
        {
            viewModel =
                Transaction.Run(
                    f: () =>
                        ViewModelBase.CreateBase(
                            bindingScheduler: scheduler,
                            continuation: (output, construct) =>
                            {
                                // The thread catches each exception. An exception that leaves a thread
                                // stops the test process.
                                Thread factoryCall =
                                    new(
                                        start: () =>
                                        {
                                            try
                                            {
                                                output.BindableFactory.CreateOneWay(cell: source);
                                            }
                                            catch (Exception exception)
                                            {
                                                caught.Add(item: exception);
                                            }
                                        })
                                    {
                                        IsBackground = true
                                    };

                                factoryCalls.Add(item: factoryCall);
                                factoryCall.Start();

                                // The thread has this state only while it waits. Before the factory makes
                                // the value, the thread waits for nothing: this thread does not hold the
                                // lock of the output.
                                waitedInTheFactory =
                                    SpinWait.SpinUntil(
                                        condition: () =>
                                            (factoryCall.ThreadState & ThreadState.WaitSleepJoin) != 0,
                                        timeout: TimeSpan.FromSeconds(value: 30));

                                return construct.Construct(constructor: static viewModelBase => viewModelBase);
                            }));

            // No transaction is open now, thus the factory call ends.
            bool factoryCallEnded = factoryCalls[0].Join(timeout: TimeSpan.FromSeconds(value: 30));

            postsBeforeTheChange = scheduler.Posts;
            source.Send(a: 2);
            postsAfterTheChange = scheduler.Posts;

            await Assert.That(factoryCallEnded).IsTrue();
        }
        finally
        {
            collectionPreventedToTheEnd = GCSettings.LatencyMode == GCLatencyMode.NoGCRegion;

            if (collectionPreventedToTheEnd)
            {
                GC.EndNoGCRegion();
            }
        }

        await Assert.That(waitedInTheFactory).IsTrue();
        await Assert.That(caught.Count).IsEqualTo(expected: 1);
        await Assert.That(caught[0]).IsTypeOf<InvalidOperationException>();
        await Assert.That(caught[0].Message).Contains(expected: "registrations are closed");
        await Assert.That(postsAfterTheChange).IsEqualTo(expected: postsBeforeTheChange);

        viewModel.Dispose();

        // A garbage collection can hide a leak, but it cannot make a released value post. Thus, the checks
        // above come first, and a failure of them is a failure. A pass without the region is not a pass.
        if (!collectionPrevented)
        {
            Skip.Test(reason: "The runtime refused the no-GC region. A leak could hide from the test.");
        }

        if (!collectionPreventedToTheEnd)
        {
            Skip.Test(reason: "The no-GC region ended before the test read the scheduler. A leak could hide from the test.");
        }
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
