using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Mvvm.Tests;

public sealed class ActionDisposableTests
{
    [Test]
    public async Task CallsTheActionAtTheFirstDisposeOnly()
    {
        Counting counting = new();
        IDisposable disposable = Disposable.FromAction(onDispose: counting.Increment);

        int callsBeforeDispose = counting.Count;
        disposable.Dispose();
        disposable.Dispose();

        await Assert.That(callsBeforeDispose).IsEqualTo(expected: 0);
        await Assert.That(counting.Count).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task ConcurrentDisposeCallsTheActionOnce()
    {
        const int threads = 16;
        Counting counting = new();
        IDisposable disposable = Disposable.FromAction(onDispose: counting.Increment);

        // Each thread starts, adds one to the count, and waits for the start signal. The test sends
        // the signal when the count is equal to the number of threads. Thus, the calls overlap and
        // do not occur in sequence.
        int ready = 0;
        TaskCompletionSource<bool> start = new();

        Task[] disposals =
        [
            .. Enumerable
                .Range(start: 0, count: threads)
                .Select(_ =>
                    Task.Factory.StartNew(
                        action: () =>
                        {
                            Interlocked.Increment(location: ref ready);
                            start.Task.Wait();
                            disposable.Dispose();
                        },
                        creationOptions: TaskCreationOptions.LongRunning))
        ];

        SpinWait.SpinUntil(condition: () => Volatile.Read(location: ref ready) == threads);
        start.SetResult(result: true);

        await Task.WhenAll(tasks: disposals);

        await Assert.That(counting.Count).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task EmptyDisposesWithoutEffect()
    {
        Disposable.Empty.Dispose();
        Disposable.Empty.Dispose();

        await Assert.That(Disposable.Empty).IsNotNull();
    }

    [Test]
    public async Task NullActionFailsAtTheCall()
    {
        // ReSharper disable once NullableWarningSuppressionIsUsed - The null action is the input under test: FromAction must refuse it at run time.
        Exception caught = Catch(action: () => _ = Disposable.FromAction(onDispose: null!));

        await Assert.That(caught).IsTypeOf<ArgumentNullException>();
        await Assert.That(((ArgumentNullException)caught).ParamName).IsEqualTo(expected: "onDispose");
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

    private sealed class Counting
    {
        private int count;

        public int Count => Volatile.Read(location: ref this.count);

        public void Increment() => Interlocked.Increment(location: ref this.count);
    }
}
