using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.StagedConstruction.Tests;

public sealed class ConstructTests
{
    [Test]
    public async Task FromGivesTheSameValuesToTheConstructor()
    {
        object values = new();

        object received = Construct.From(values: values).Construct(constructor: static v => v);

        await Assert.That(received).IsSameReferenceAs(expected: values);
    }

    [Test]
    public async Task ConstructReturnsWhatTheConstructorReturns()
    {
        object result = new();

        object returned = Construct.From(values: 1).Construct(constructor: _ => result);

        await Assert.That(returned).IsSameReferenceAs(expected: result);
    }

    [Test]
    public async Task SelectCallsTheSelectorOnlyWhenConstructIsCalled()
    {
        int calls = 0;

        IConstruct<int> selected = Construct.From(values: 1).Select(
            selector: v =>
            {
                calls++;

                return v + 1;
            });

        int callsBeforeConstruct = calls;
        int received = selected.Construct(constructor: static v => v);

        await Assert.That(callsBeforeConstruct).IsEqualTo(expected: 0);
        await Assert.That(calls).IsEqualTo(expected: 1);
        await Assert.That(received).IsEqualTo(expected: 2);
    }

    // A chain of Select calls against the composition of the same functions that it replaces.
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(-7)]
    [Arguments(int.MaxValue)]
    public async Task SelectChainMatchesTheCompositionOfItsSelectors(int values)
    {
        string bySelect = Construct
            .From(values: values)
            .Select(selector: Double)
            .Select(selector: Describe)
            .Construct(constructor: Enclose);

        string byComposition = Enclose(text: Describe(number: Double(number: values)));

        await Assert.That(bySelect).IsEqualTo(expected: byComposition);

        return;

        static long Double(int number) => number * 2L;

        static string Describe(long number) => $"number {number}";

        static string Enclose(string text) => $"[{text}]";
    }

    [Test]
    public async Task SelectRefusesANullSelectorWhenItIsCalled()
    {
        IConstruct<int> construct = Construct.From(values: 1);

        // ReSharper disable once NullableWarningSuppressionIsUsed - The null selector is the input under test: Select must refuse it at run time.
        Exception caught = Catching.Catch(action: () => _ = construct.Select<int>(selector: null!));

        await Assert.That(caught).IsTypeOf<ArgumentNullException>();
        await Assert.That(((ArgumentNullException)caught).ParamName).IsEqualTo(expected: "selector");
    }

    [Test]
    public async Task HandleIsCovariantInItsValues()
    {
        const string values = "values";

        IConstruct<object> construct = Construct.From(values: values);

        object received = construct.Construct(constructor: static v => v);

        await Assert.That(received).IsSameReferenceAs(expected: values);
    }
}
