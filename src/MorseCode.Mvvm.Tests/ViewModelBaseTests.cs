using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Mvvm.Tests;

public sealed class ViewModelBaseTests
{
    [Test]
    public async Task NullDisposableFailsAtRegistration()
    {
        Exception? caught = null;

        _ = Create(
            register: output =>
                // ReSharper disable once NullableWarningSuppressionIsUsed - The null entry is the input under test: AddDisposable must refuse it at run time.
                caught = Catch(action: () => output.AddDisposable<IDisposable>(disposable: null!)));

        await Assert.That(caught).IsTypeOf<ArgumentNullException>();
    }

    [Test]
    public async Task NullListenerFailsAtRegistration()
    {
        Exception? caught = null;

        _ = Create(
            register: output =>
                // ReSharper disable once NullableWarningSuppressionIsUsed - The null entry is the input under test: AddListener must refuse it at run time.
                caught = Catch(action: () => output.AddListener<IWeakListener>(listener: null!)));

        await Assert.That(caught).IsTypeOf<ArgumentNullException>();
    }

    // Before the check, a null entry got to Dispose, and the composite refused it there. Thus, Dispose
    // threw and disposed none of the entries.
    [Test]
    public async Task RefusedNullDoesNotStopTheDisposalOfTheOtherEntries()
    {
        List<string> log = [];

        ViewModelBase viewModel = Create(
            register: output =>
            {
                output.AddDisposable(disposable: new Recording(log: log, name: "A"));

                // ReSharper disable once NullableWarningSuppressionIsUsed - The null entry is the input under test: AddDisposable must refuse it at run time.
                _ = Catch(action: () => output.AddDisposable<IDisposable>(disposable: null!));

                output.AddDisposable(disposable: new Recording(log: log, name: "B"));
            });

        viewModel.Dispose();

        await Assert.That(log).IsEquivalentTo(expected: ["A", "B"], ordering: CollectionOrdering.Matching);
    }

    private static ViewModelBase Create(Action<ViewModelBase.IOutput> register) =>
        ViewModelBase.CreateBase(
            bindingScheduler: BindingScheduler.Immediate,
            continuation: (output, construct) =>
            {
                register(obj: output);

                return construct.Construct(constructor: static viewModelBase => viewModelBase);
            });

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
}
