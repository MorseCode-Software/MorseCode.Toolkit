using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.StagedConstruction.Tests;

public sealed class ConstructorSubclassTests
{
    [Test]
    public async Task SecondConstructFailsAndTheCoreRunsOnce()
    {
        Recording construct = new(values: 1);

        _ = construct.Construct(constructor: static values => values);
        Exception caught = Catching.Catch(action: () => _ = construct.Construct(constructor: static values => values));

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(caught.Message).Contains(expected: "Construct was already called");
        await Assert.That(construct.CoreCalls).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task ConstructGivesTheValuesOfTheCoreToTheConstructor()
    {
        Recording construct = new(values: 41);

        int result = construct.Construct(constructor: static values => values + 1);

        await Assert.That(result).IsEqualTo(expected: 42);
    }

    // The claim occurs before the core runs. Thus, a core that fails also uses the handle, and a second
    // call does not run it again.
    [Test]
    public async Task FailedCoreUsesTheHandle()
    {
        InvalidOperationException failure = new(message: "The core failed.");
        Recording construct = new(values: 1, failure: failure);

        Exception first = Catching.Catch(action: () => _ = construct.Construct(constructor: static values => values));
        Exception second = Catching.Catch(action: () => _ = construct.Construct(constructor: static values => values));

        await Assert.That(first).IsSameReferenceAs(expected: failure);
        await Assert.That(second.Message).Contains(expected: "Construct was already called");
        await Assert.That(construct.CoreCalls).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task ConcurrentConstructSucceedsOnce()
    {
        Recording construct = new(values: 1);

        int[] results = await Concurrency.RunTogether(call: () => _ = construct.Construct(constructor: static values => values));

        int succeeded = results.Count(predicate: static result => result == Concurrency.Succeeded);
        int refused = results.Count(predicate: static result => result == Concurrency.Refused);

        await Assert.That(succeeded).IsEqualTo(expected: 1);
        await Assert.That(refused).IsEqualTo(expected: Concurrency.Threads - 1);
        await Assert.That(construct.CoreCalls).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task DefaultSelectGivesTheSelectedValuesToTheConstructor()
    {
        Recording source = new(values: 20);
        int selectorCalls = 0;

        Constructor<int> selected = source.Select(
            selector: values =>
            {
                Interlocked.Increment(location: ref selectorCalls);

                return values * 2;
            });

        await Assert.That(selectorCalls).IsEqualTo(expected: 0);

        int result = selected.Construct(constructor: static values => values + 2);

        await Assert.That(result).IsEqualTo(expected: 42);
        await Assert.That(selectorCalls).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task DefaultSelectDoesNotUseTheSource()
    {
        Recording source = new(values: 1);

        _ = source.Select(selector: static values => values);
        int result = source.Construct(constructor: static values => values);

        await Assert.That(result).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task DefaultSelectHandleIsUsedOnce()
    {
        Recording source = new(values: 1);
        Constructor<int> selected = source.Select(selector: static values => values);

        _ = selected.Construct(constructor: static values => values);
        Exception caught = Catching.Catch(action: () => _ = selected.Construct(constructor: static values => values));

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(source.CoreCalls).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task DefaultSelectRefusesANullSelectorWhenItIsCalled()
    {
        Recording source = new(values: 1);

        // ReSharper disable once NullableWarningSuppressionIsUsed - The null selector is the input under test: Select must refuse it at run time.
        Exception caught = Catching.Catch(action: () => _ = source.Select<int>(selector: null!));

        await Assert.That(caught).IsTypeOf<ArgumentNullException>();
        await Assert.That(((ArgumentNullException)caught).ParamName).IsEqualTo(expected: "selector");
        await Assert.That(source.CoreCalls).IsEqualTo(expected: 0);
    }

    // The override selects at once, and the default selects when the subclass constructs. Thus, the
    // count of the calls shows which one ran.
    [Test]
    public async Task OverrideOfSelectRunsInPlaceOfTheDefault()
    {
        Overriding source = new(values: 1);

        Constructor<int> selected = source.Select(selector: static values => values + 1);

        await Assert.That(source.SelectCalls).IsEqualTo(expected: 1);
        await Assert.That(selected.Construct(constructor: static values => values)).IsEqualTo(expected: 2);
    }

    private sealed class Recording(in int values, in Exception? failure = null) : Constructor<int>
    {
        private readonly int values = values;

        private readonly Exception? failure = failure;

        private int coreCalls;

        public int CoreCalls => Volatile.Read(location: ref this.coreCalls);

        protected override TResult ConstructCore<TResult>(Func<int, TResult> constructor)
        {
            Interlocked.Increment(location: ref this.coreCalls);

            return this.failure is null ? constructor(arg: this.values) : throw this.failure;
        }
    }

    private sealed class Overriding(in int values) : Constructor<int>
    {
        private readonly int values = values;

        private int selectCalls;

        public int SelectCalls => Volatile.Read(location: ref this.selectCalls);

        public override Constructor<TSelectedValues> Select<TSelectedValues>(Func<int, TSelectedValues> selector)
        {
            Interlocked.Increment(location: ref this.selectCalls);

            return Constructor.From(values: selector(arg: this.values));
        }

        protected override TResult ConstructCore<TResult>(Func<int, TResult> constructor) =>
            constructor(arg: this.values);
    }
}
