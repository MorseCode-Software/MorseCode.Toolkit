using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.StagedConstruction.Tests;

public sealed class StageTests
{
    [Test]
    public async Task FromCallsTheBodyOnlyWhenAdvanceIsCalled()
    {
        List<string> log = [];

        IStage<int, int, int> stage = Stage.From(
            body: (int input) =>
            {
                log.Add(item: $"body got {input}");

                return (Output: input, Next: input);
            });

        int logCountBeforeAdvance = log.Count;

        _ = stage.Advance(
            input: 5,
            continuation: (output, next) =>
            {
                log.Add(item: $"continuation got {output} and {next}");

                return output;
            });

        await Assert.That(logCountBeforeAdvance).IsEqualTo(expected: 0);
        await Assert.That(log).IsEquivalentTo(expected: ["body got 5", "continuation got 5 and 5"], ordering: CollectionOrdering.Matching);
    }

    [Test]
    public async Task AdvanceGivesTheOutputAndTheHandleOfTheBodyToTheContinuation()
    {
        object output = new();
        object next = new();

        IStage<int, object, object> stage = Stage.From(body: (int _) => (Output: output, Next: next));

        (object receivedOutput, object receivedNext) = stage.Advance(input: 0, continuation: static (o, n) => (o, n));

        await Assert.That(receivedOutput).IsSameReferenceAs(expected: output);
        await Assert.That(receivedNext).IsSameReferenceAs(expected: next);
    }

    [Test]
    public async Task AdvanceReturnsWhatTheContinuationReturns()
    {
        object result = new();

        IStage<int, int, int> stage = Stage.From(body: static (int input) => (Output: input, Next: input));

        object returned = stage.Advance(input: 0, continuation: (_, _) => result);

        await Assert.That(returned).IsSameReferenceAs(expected: result);
    }

    [Test]
    public async Task FromRefusesANullBodyWhenItIsCalled()
    {
        // ReSharper disable once NullableWarningSuppressionIsUsed - The null body is the input under test: From must refuse it at run time.
        Exception caught = Catching.Catch(action: () => _ = Stage.From<int, int, int>(body: null!));

        await Assert.That(caught).IsTypeOf<ArgumentNullException>();
        await Assert.That(((ArgumentNullException)caught).ParamName).IsEqualTo(expected: "body");
    }

    [Test]
    public async Task StageIsContravariantInItsInputAndCovariantInItsOutputAndHandle()
    {
        const string output = "output";
        const string next = "next";

        IStage<string, object, object> stage = Stage.From(body: static (object _) => (Output: output, Next: next));

        (object receivedOutput, object receivedNext) = stage.Advance(input: "input", continuation: static (o, n) => (o, n));

        await Assert.That(receivedOutput).IsSameReferenceAs(expected: output);
        await Assert.That(receivedNext).IsSameReferenceAs(expected: next);
    }
}
