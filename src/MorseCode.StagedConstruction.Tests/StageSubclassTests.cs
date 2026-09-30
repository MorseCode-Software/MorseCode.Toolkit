using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.StagedConstruction.Tests;

public sealed class StageSubclassTests
{
    [Test]
    public async Task SecondAdvanceFailsAndTheCoreRunsOnce()
    {
        Recording stage = new();

        _ = stage.Advance(input: 1, continuation: static (output, _) => output);
        Exception caught = Catching.Catch(action: () => _ = stage.Advance(input: 1, continuation: static (output, _) => output));

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(caught.Message).Contains(expected: "Advance was already called");
        await Assert.That(stage.CoreCalls).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task AdvanceGivesTheInputToTheCoreAndTheResultOfTheCoreToTheContinuation()
    {
        Recording stage = new();

        int result = stage.Advance(input: 20, continuation: static (output, next) => output + next);

        // The core gives the input plus one as the output, and the input as the next handle.
        await Assert.That(result).IsEqualTo(expected: 41);
    }

    // The claim occurs before the core runs. Thus, a core that fails also uses the stage, and a second
    // call does not run it again.
    [Test]
    public async Task FailedCoreUsesTheStage()
    {
        InvalidOperationException failure = new(message: "The core failed.");
        Recording stage = new(failure: failure);

        Exception first = Catching.Catch(action: () => _ = stage.Advance(input: 1, continuation: static (output, _) => output));
        Exception second = Catching.Catch(action: () => _ = stage.Advance(input: 1, continuation: static (output, _) => output));

        await Assert.That(first).IsSameReferenceAs(expected: failure);
        await Assert.That(second.Message).Contains(expected: "Advance was already called");
        await Assert.That(stage.CoreCalls).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task ConcurrentAdvanceSucceedsOnce()
    {
        Recording stage = new();

        int[] results = await Concurrency.RunTogether(
            call: () => _ = stage.Advance(input: 1, continuation: static (output, _) => output));

        int succeeded = results.Count(predicate: static result => result == Concurrency.Succeeded);
        int refused = results.Count(predicate: static result => result == Concurrency.Refused);

        await Assert.That(succeeded).IsEqualTo(expected: 1);
        await Assert.That(refused).IsEqualTo(expected: Concurrency.Threads - 1);
        await Assert.That(stage.CoreCalls).IsEqualTo(expected: 1);
    }

    private sealed class Recording(in Exception? failure = null) : Stage<int, int, int>
    {
        private readonly Exception? failure = failure;

        private int coreCalls;

        public int CoreCalls => Volatile.Read(location: ref this.coreCalls);

        protected override TResult AdvanceCore<TResult>(int input, StageContinuation<int, int, TResult> continuation)
        {
            Interlocked.Increment(location: ref this.coreCalls);

            return this.failure is null ? continuation(output: input + 1, next: input) : throw this.failure;
        }
    }
}
