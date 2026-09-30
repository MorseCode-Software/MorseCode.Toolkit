using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.StagedConstruction.Tests;

public sealed class SingleUseTests
{
    private const int Threads = 16;

    [Test]
    public async Task FromFailsAtASecondConstruct()
    {
        Counting constructorCalls = new();
        IConstruct<int> construct = Construct.From(values: 1);

        _ = construct.Construct(constructor: constructorCalls.Pass);
        Exception caught = Catching.Catch(action: () => _ = construct.Construct(constructor: constructorCalls.Pass));

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(caught.Message).Contains(expected: "Construct was already called");
        await Assert.That(constructorCalls.Count).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task SelectFailsAtASecondConstructAndCallsTheSelectorOnce()
    {
        Counting selectorCalls = new();
        IConstruct<int> selected = Construct.From(values: 1).Select(selector: selectorCalls.Pass);

        _ = selected.Construct(constructor: static values => values);
        Exception caught = Catching.Catch(action: () => _ = selected.Construct(constructor: static values => values));

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(selectorCalls.Count).IsEqualTo(expected: 1);
    }

    // A handle that a base implements does not have to make the claim. The handle that Select makes over
    // it must make the claim itself.
    [Test]
    public async Task SelectChecksOverASourceThatDoesNotCheck()
    {
        Counting selectorCalls = new();
        IConstruct<int> source = new Reusable(values: 1);
        IConstruct<int> selected = source.Select(selector: selectorCalls.Pass);

        _ = selected.Construct(constructor: static values => values);
        Exception caught = Catching.Catch(action: () => _ = selected.Construct(constructor: static values => values));

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(selectorCalls.Count).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task AdvanceFailsAtASecondCallAndCallsTheBodyOnce()
    {
        Counting bodyCalls = new();
        IStage<int, int, int> stage = Stage.From(body: (int input) => (Output: bodyCalls.Pass(value: input), Next: input));

        _ = stage.Advance(input: 1, continuation: static (output, _) => output);

        Exception caught =
            Catching.Catch(action: () => _ = stage.Advance(input: 1, continuation: static (output, _) => output));

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(caught.Message).Contains(expected: "Advance was already called");
        await Assert.That(bodyCalls.Count).IsEqualTo(expected: 1);
    }

    // The claim occurs before the constructor runs. Thus, a constructor that fails also uses the handle,
    // and a second call does not run it again.
    [Test]
    public async Task FailedConstructUsesTheHandle()
    {
        Counting constructorCalls = new();
        InvalidOperationException failure = new(message: "The constructor failed.");
        IConstruct<int> construct = Construct.From(values: 1);

        Exception first = Catching.Catch(action: () => _ = construct.Construct(constructor: Fail));
        Exception second = Catching.Catch(action: () => _ = construct.Construct(constructor: Fail));

        await Assert.That(first).IsSameReferenceAs(expected: failure);
        await Assert.That(second.Message).Contains(expected: "Construct was already called");
        await Assert.That(constructorCalls.Count).IsEqualTo(expected: 1);

        return;

        int Fail(int values)
        {
            _ = constructorCalls.Pass(value: values);

            throw failure;
        }
    }

    [Test]
    public async Task FailedAdvanceUsesTheHandle()
    {
        Counting bodyCalls = new();
        InvalidOperationException failure = new(message: "The stage failed.");
        IStage<int, int, int> stage = Stage.From<int, int, int>(body: Fail);

        Exception first = Catching.Catch(action: () => _ = stage.Advance(input: 1, continuation: static (output, _) => output));
        Exception second = Catching.Catch(action: () => _ = stage.Advance(input: 1, continuation: static (output, _) => output));

        await Assert.That(first).IsSameReferenceAs(expected: failure);
        await Assert.That(second.Message).Contains(expected: "Advance was already called");
        await Assert.That(bodyCalls.Count).IsEqualTo(expected: 1);

        return;

        (int Output, int Next) Fail(int input)
        {
            _ = bodyCalls.Pass(value: input);

            throw failure;
        }
    }

    [Test]
    public async Task ConcurrentConstructSucceedsOnce()
    {
        Counting constructorCalls = new();
        IConstruct<int> construct = Construct.From(values: 1);

        int[] results = await RunTogether(call: () => _ = construct.Construct(constructor: constructorCalls.Pass));

        await Assert.That(results.Count(predicate: static result => result == Succeeded)).IsEqualTo(expected: 1);
        await Assert.That(results.Count(predicate: static result => result == Refused)).IsEqualTo(expected: Threads - 1);
        await Assert.That(constructorCalls.Count).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task ConcurrentAdvanceSucceedsOnce()
    {
        Counting bodyCalls = new();
        IStage<int, int, int> stage = Stage.From(body: (int input) => (Output: bodyCalls.Pass(value: input), Next: input));

        int[] results =
            await RunTogether(call: () => _ = stage.Advance(input: 1, continuation: static (output, _) => output));

        await Assert.That(results.Count(predicate: static result => result == Succeeded)).IsEqualTo(expected: 1);
        await Assert.That(results.Count(predicate: static result => result == Refused)).IsEqualTo(expected: Threads - 1);
        await Assert.That(bodyCalls.Count).IsEqualTo(expected: 1);
    }

    private const int Succeeded = 1;

    private const int Refused = 2;

    // Each thread starts, adds one to the count, and waits for the start signal. The test sends the
    // signal when the count is equal to the number of threads. Thus, the calls overlap and do not occur
    // in sequence.
    private static async Task<int[]> RunTogether(Action call)
    {
        int ready = 0;
        TaskCompletionSource start = new();

        Task<int>[] calls =
        [
            .. Enumerable
                .Range(start: 0, count: Threads)
                .Select(_ =>
                    Task.Factory.StartNew(
                        function: () =>
                        {
                            Interlocked.Increment(location: ref ready);
                            start.Task.Wait();

                            try
                            {
                                call();

                                return Succeeded;
                            }
                            catch (InvalidOperationException)
                            {
                                return Refused;
                            }
                        },
                        creationOptions: TaskCreationOptions.LongRunning))
        ];

        SpinWait.SpinUntil(condition: () => Volatile.Read(location: ref ready) == Threads);
        start.SetResult();

        return await Task.WhenAll(tasks: calls);
    }

    private sealed class Counting
    {
        private int count;

        public int Count => Volatile.Read(location: ref this.count);

        public int Pass(int value)
        {
            Interlocked.Increment(location: ref this.count);

            return value;
        }
    }

    private sealed class Reusable(in int values) : IConstruct<int>
    {
        private readonly int values = values;

        public TResult Construct<TResult>(Func<int, TResult> constructor) => constructor(arg: this.values);
    }
}
