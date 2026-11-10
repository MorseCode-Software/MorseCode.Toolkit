using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MorseCode.StagedConstruction;
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
    public async Task NullSchedulerFailsAtTheCall()
    {
        Exception caught =
            Catch(
                action: static () =>
                    ViewModelBase.CreateBase(
                        // ReSharper disable once NullableWarningSuppressionIsUsed - The null scheduler is the input under test: CreateBase must refuse it at run time.
                        bindingScheduler: null!,
                        continuation: static (_, construct) =>
                            construct.Construct(constructor: static viewModelBase => viewModelBase)));

        await Assert.That(caught).IsTypeOf<ArgumentNullException>();
        await Assert.That((caught as ArgumentNullException)?.ParamName).IsEqualTo(expected: "bindingScheduler");
    }

    [Test]
    public async Task NullContinuationFailsAtTheCall()
    {
        Exception caught =
            Catch(
                action: static () =>
                    ViewModelBase.CreateBase<ViewModelBase>(
                        bindingScheduler: BindingScheduler.Immediate,
                        // ReSharper disable once NullableWarningSuppressionIsUsed - The null continuation is the input under test: CreateBase must refuse it at run time.
                        continuation: null!));

        await Assert.That(caught).IsTypeOf<ArgumentNullException>();
        await Assert.That((caught as ArgumentNullException)?.ParamName).IsEqualTo(expected: "continuation");
    }

    [Test]
    public async Task NullDisposableFailsAtRegistration()
    {
        Exception? caught = null;

        _ =
            Create(
                register: output =>
                    // ReSharper disable once NullableWarningSuppressionIsUsed - The null entry is the input under test: AddDisposable must refuse it at run time.
                    caught = Catch(action: () => output.AddDisposable<IDisposable>(disposable: null!)));

        await Assert.That(caught).IsTypeOf<ArgumentNullException>();
    }

    [Test]
    public async Task NullListenerFailsAtRegistration()
    {
        Exception? caught = null;

        _ =
            Create(
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

        ViewModelBase viewModel =
            Create(
                register: output =>
                {
                    output.AddDisposable(disposable: new Recording(log: log, name: "A"));

                    // ReSharper disable once NullableWarningSuppressionIsUsed - The null entry is the input under test: AddDisposable must refuse it at run time.
                    _ = Catch(action: () => output.AddDisposable<IDisposable>(disposable: null!));

                    output.AddDisposable(disposable: new Recording(log: log, name: "B"));
                });

        viewModel.Dispose();

        await Assert.That(log).IsEquivalentTo(expected: ["B", "A"], ordering: CollectionOrdering.Matching);
    }

    [Test]
    public async Task DisposeDisposesTheEntriesOfTheConstructionOneTime()
    {
        List<string> log = [];

        ViewModelBase viewModel =
            Create(
                register: output =>
                {
                    output.AddDisposable(disposable: new Recording(log: log, name: "A"));
                    output.AddDisposable(disposable: new Recording(log: log, name: "B"));
                });

        viewModel.Dispose();
        viewModel.Dispose();

        await Assert.That(log).IsEquivalentTo(expected: ["B", "A"], ordering: CollectionOrdering.Matching);
    }

    // Before one list held the two kinds, Dispose stopped each listener before it disposed any
    // disposable, in the sequence B, A, C. After that, one list released them in the sequence A, B, C.
    // That sequence stopped a registration after the registrations that it can use.
    [Test]
    public async Task DisposeReleasesListenersAndDisposablesInTheReverseOrderOfTheirRegistration()
    {
        List<string> log = [];

        ViewModelBase viewModel =
            Create(
                register: output =>
                {
                    output.AddDisposable(disposable: new Recording(log: log, name: "A"));
                    output.AddListener(listener: new RecordingListener(log: log, name: "B"));
                    output.AddDisposable(disposable: new Recording(log: log, name: "C"));
                });

        viewModel.Dispose();

        await Assert.That(log).IsEquivalentTo(expected: ["C", "B", "A"], ordering: CollectionOrdering.Matching);
    }

    [Test]
    public async Task TwoFailuresThrowOneAggregateInTheOrderOfTheirRelease()
    {
        Exception first = new InvalidOperationException(message: "A failed.");
        Exception second = new InvalidOperationException(message: "B failed.");

        ViewModelBase viewModel =
            Create(
                register: output =>
                {
                    output.AddDisposable(disposable: Disposable.FromAction(onDispose: () => throw first));
                    output.AddDisposable(disposable: Disposable.FromAction(onDispose: () => throw second));
                });

        Exception caught = Catch(action: viewModel.Dispose);

        await Assert.That(caught).IsTypeOf<AggregateException>();

        await Assert
            .That((caught as AggregateException)?.InnerExceptions)
            .IsEquivalentTo(expected: [second, first], ordering: CollectionOrdering.Matching);
    }

    // Before the seal, the composite read the registrations in Dispose. Thus, Dispose released an
    // entry that a kept output added after construction, and an entry added after Dispose leaked.
    [Test]
    public async Task DisposableRegistrationAfterConstructionFailsAndIsNotKept()
    {
        List<string> log = [];
        List<ViewModelBase.IOutput> kept = [];

        ViewModelBase viewModel = Create(register: kept.Add);

        Exception caught =
            Catch(action: () => kept[0].AddDisposable(disposable: new Recording(log: log, name: "late")));

        viewModel.Dispose();

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(log).IsEmpty();
    }

    [Test]
    public async Task ListenerRegistrationAfterConstructionFailsAndIsNotKept()
    {
        List<string> log = [];
        List<ViewModelBase.IOutput> kept = [];

        ViewModelBase viewModel = Create(register: kept.Add);

        Exception caught =
            Catch(action: () => kept[0].AddListener(listener: new RecordingListener(log: log, name: "late")));

        viewModel.Dispose();

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(log).IsEmpty();
    }

    [Test]
    public async Task BindableAfterConstructionFails()
    {
        List<ViewModelBase.IOutput> kept = [];

        _ = Create(register: kept.Add);

        Exception caught =
            Catch(action: () => kept[0].BindableFactory.CreateOneWay(cell: Cell.CreateSink(initialValue: 1)));

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
    }

    // The base seals the output when the constructor that Construct calls returns, and not before. Thus,
    // the constructor of the subclass can register, and Dispose releases what it registered.
    [Test]
    public async Task RegistrationInTheConstructorIsReleasedByDispose()
    {
        List<string> log = [];

        ViewModelBase viewModel =
            ViewModelBase.CreateBase(
                bindingScheduler: BindingScheduler.Immediate,
                continuation: (output, construct) =>
                    construct.Construct(
                        constructor: viewModelBase =>
                        {
                            output.AddDisposable(disposable: new Recording(log: log, name: "in constructor"));

                            return viewModelBase;
                        }));

        await Assert.That(log).IsEmpty();

        viewModel.Dispose();

        await Assert.That(log).IsEquivalentTo(expected: ["in constructor"]);
    }

    [Test]
    public async Task RegistrationAfterTheConstructorReturnsFails()
    {
        List<ViewModelBase.IOutput> kept = [];

        _ =
            ViewModelBase.CreateBase(
                bindingScheduler: BindingScheduler.Immediate,
                continuation: (output, construct) =>
                {
                    kept.Add(output);

                    return construct.Construct(constructor: static viewModelBase => viewModelBase);
                });

        Exception caught =
            Catch(
                action: () =>
                    kept[0]
                        .AddDisposable(
                            disposable: Disposable.FromAction(
                                onDispose: static () =>
                                {
                                })));

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
    }

    // A subclass that disposes itself on a failure path in its constructor must release what it
    // registered up to then. The call must not wait for the end of the construction.
    [Test]
    public async Task DisposeInTheConstructorReleasesTheRegistrationsMadeBefore()
    {
        List<string> log = [];

        ViewModelBase viewModel =
            ViewModelBase.CreateBase(
                bindingScheduler: BindingScheduler.Immediate,
                continuation: (output, construct) =>
                {
                    output.AddDisposable(disposable: new Recording(log: log, name: "first"));
                    output.AddDisposable(disposable: new Recording(log: log, name: "second"));

                    return construct.Construct(
                        constructor: static viewModelBase =>
                        {
                            viewModelBase.Dispose();

                            return viewModelBase;
                        });
                });

        await Assert.That(log).IsEquivalentTo(expected: ["second", "first"], ordering: CollectionOrdering.Matching);

        viewModel.Dispose();

        await Assert.That(log).IsEquivalentTo(expected: ["second", "first"], ordering: CollectionOrdering.Matching);
    }

    [Test]
    public async Task RegistrationAfterDisposeInTheConstructorFailsAndIsNotKept()
    {
        List<string> log = [];
        Exception? caught = null;

        _ =
            ViewModelBase.CreateBase(
                bindingScheduler: BindingScheduler.Immediate,
                continuation: (output, construct) =>
                    construct.Construct(
                        constructor: viewModelBase =>
                        {
                            viewModelBase.Dispose();

                            caught =
                                Catch(
                                    action: () =>
                                        output.AddDisposable(disposable: new Recording(log: log, name: "late")));

                            return viewModelBase;
                        }));

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(log).IsEmpty();
    }

    // The subclass never receives a base from a failed construction, so nothing else can release what the
    // construction registered. The exception goes to the caller unchanged.
    [Test]
    public async Task FailedConstructorReleasesTheRegistrationsAndRethrowsTheSameException()
    {
        List<string> log = [];
        List<ViewModelBase.IOutput> kept = [];
        InvalidOperationException failure = new(message: "The constructor failed.");

        Exception caught =
            Catch(
                action: () =>
                    ViewModelBase.CreateBase<ViewModelBase>(
                        bindingScheduler: BindingScheduler.Immediate,
                        continuation: (output, construct) =>
                        {
                            kept.Add(output);
                            output.AddDisposable(disposable: new Recording(log: log, name: "registered"));

                            return construct.Construct<ViewModelBase>(constructor: _ => throw failure);
                        }));

        Exception late =
            Catch(action: () => kept[0].AddDisposable(disposable: new Recording(log: log, name: "late")));

        await Assert.That(caught).IsSameReferenceAs(expected: failure);
        await Assert.That(log).IsEquivalentTo(expected: ["registered"]);
        await Assert.That(late).IsTypeOf<InvalidOperationException>();
    }

    // The caller must see both errors, and the exception of the constructor is the first.
    [Test]
    public async Task FailedConstructorWithAFailingRegistrationThrowsBothExceptions()
    {
        Exception failure = new InvalidOperationException(message: "The constructor failed.");
        Exception releaseFailure = new InvalidOperationException(message: "The release failed.");
        List<string> log = [];
        List<ViewModelBase.IOutput> kept = [];

        Exception caught =
            Catch(
                action: () =>
                    ViewModelBase.CreateBase<ViewModelBase>(
                        bindingScheduler: BindingScheduler.Immediate,
                        continuation: (output, construct) =>
                        {
                            kept.Add(output);

                            output.AddDisposable(
                                disposable: Disposable.FromAction(onDispose: () => throw releaseFailure));

                            return construct.Construct<ViewModelBase>(constructor: _ => throw failure);
                        }));

        await Assert.That(caught).IsTypeOf<AggregateException>();

        await Assert
            .That((caught as AggregateException)?.InnerExceptions)
            .IsEquivalentTo(expected: [failure, releaseFailure], ordering: CollectionOrdering.Matching);

        // The output closes before the release runs. Thus, it is closed also when the release throws.
        Exception late =
            Catch(action: () => kept[0].AddDisposable(disposable: new Recording(log: log, name: "late")));

        await Assert.That(late).IsTypeOf<InvalidOperationException>();
        await Assert.That(log).IsEmpty();
    }

    // No view model owns the registrations of a continuation that did not call Construct. Thus, the
    // base releases them, and the caller learns that the continuation is wrong. The compiler accepts a
    // null return. It also accepts the result of a different handle, and the subsequent test is for it.
    [Test]
    public async Task AContinuationThatReturnsWithoutConstructFailsAndReleasesTheRegistrations()
    {
        List<string> log = [];
        List<ViewModelBase.IOutput> kept = [];

#pragma warning disable MC1003, MC1006 // The continuation that does not use its handle and returns null is the input under test.
        Exception caught =
            Catch(
                action: () =>
                    ViewModelBase.CreateBase<ViewModelBase>(
                        bindingScheduler: BindingScheduler.Immediate,
                        continuation: (output, _) =>
                        {
                            kept.Add(output);
                            output.AddDisposable(disposable: new Recording(log: log, name: "registered"));

                            // ReSharper disable once NullableWarningSuppressionIsUsed - A null is the only value that a continuation can return without a call to Construct, because only Construct makes the value that the continuation returns.
                            return null!;
                        }));
#pragma warning restore MC1003, MC1006

        Exception late =
            Catch(action: () => kept[0].AddDisposable(disposable: new Recording(log: log, name: "late")));

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(caught.Message).Contains(expected: "without a call to Construct");
        await Assert.That(log).IsEquivalentTo(expected: ["registered"]);
        await Assert.That(late).IsTypeOf<InvalidOperationException>();
    }

    // A different handle can also make the value that the continuation returns. The continuation did
    // not call Construct on the handle of the base, thus no view model owns the registrations.
    [Test]
    public async Task AContinuationThatUsesOnlyADifferentHandleFailsAndReleasesTheRegistrations()
    {
        List<string> log = [];

#pragma warning disable MC1004, MC1006 // The continuation that uses only a different handle is the input under test.
        Exception caught =
            Catch(
                action: () =>
                    ViewModelBase.CreateBase(
                        bindingScheduler: BindingScheduler.Immediate,
                        continuation: (output, _) =>
                        {
                            output.AddDisposable(disposable: new Recording(log: log, name: "registered"));

                            return Constructor.From(values: "something else")
                                .Construct(constructor: static values => values);
                        }));
#pragma warning restore MC1004, MC1006

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(caught.Message).Contains(expected: "without a call to Construct");
        await Assert.That(log).IsEquivalentTo(expected: ["registered"]);
    }

    // The exception leaves the continuation before Construct. Nothing else can release what the
    // continuation registered.
    [Test]
    public async Task AContinuationThatThrowsBeforeConstructReleasesTheRegistrationsAndRethrows()
    {
        List<string> log = [];
        InvalidOperationException failure = new(message: "The continuation failed.");

        Exception caught =
            Catch(
                action: () =>
                    ViewModelBase.CreateBase<ViewModelBase>(
                        bindingScheduler: BindingScheduler.Immediate,
                        continuation: (output, _) =>
                        {
                            output.AddDisposable(disposable: new Recording(log: log, name: "registered"));

                            throw failure;
                        }));

        await Assert.That(caught).IsSameReferenceAs(expected: failure);
        await Assert.That(log).IsEquivalentTo(expected: ["registered"]);
    }

    // The view model exists, but the continuation lost it when it threw. The caller never receives it,
    // thus the base releases its registrations.
    [Test]
    public async Task AContinuationThatThrowsAfterConstructReleasesTheRegistrationsAndRethrows()
    {
        List<string> log = [];
        InvalidOperationException failure = new(message: "The continuation failed.");

#pragma warning disable MC1002 // The continuation throws after Construct, thus it cannot return the result.
        Exception caught =
            Catch(
                action: () =>
                    ViewModelBase.CreateBase<ViewModelBase>(
                        bindingScheduler: BindingScheduler.Immediate,
                        continuation: (output, construct) =>
                        {
                            output.AddDisposable(disposable: new Recording(log: log, name: "registered"));
                            _ = construct.Construct(constructor: static viewModelBase => viewModelBase);

                            throw failure;
                        }));
#pragma warning restore MC1002

        await Assert.That(caught).IsSameReferenceAs(expected: failure);
        await Assert.That(log).IsEquivalentTo(expected: ["registered"]);
    }

    // The continuation called Construct, and then returned the value of a different handle. The view
    // model that Construct made goes to nobody, thus the base releases its registrations.
    [Test]
    public async Task AContinuationThatDropsTheResultOfConstructFailsAndReleasesTheRegistrations()
    {
        List<string> log = [];
        List<ViewModelBase.IOutput> kept = [];

#pragma warning disable MC1002, MC1004 // The dropped result of Construct and the result of a different handle are the inputs under test.
        Exception caught =
            Catch(
                action: () =>
                    ViewModelBase.CreateBase(
                        bindingScheduler: BindingScheduler.Immediate,
                        continuation: (output, construct) =>
                        {
                            kept.Add(output);
                            output.AddDisposable(disposable: new Recording(log: log, name: "registered"));
                            _ = construct.Construct(constructor: static viewModelBase => viewModelBase);

                            return Constructor.From(values: "something else")
                                .Construct(constructor: static values => values);
                        }));
#pragma warning restore MC1002, MC1004

        Exception late =
            Catch(action: () => kept[0].AddDisposable(disposable: new Recording(log: log, name: "late")));

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(caught.Message).Contains(expected: "did not return the result of Construct");
        await Assert.That(log).IsEquivalentTo(expected: ["registered"]);
        await Assert.That(late).IsTypeOf<InvalidOperationException>();
    }

    // The two values have the same type. Thus, only the instance shows that the continuation returned
    // a view model that this construction did not make.
    [Test]
    public async Task AContinuationThatReturnsADifferentViewModelFailsAndReleasesTheRegistrations()
    {
        List<string> log = [];

        ViewModelBase other =
            Create(
                register: static _ =>
                {
                });

#pragma warning disable MC1002, MC1004 // The dropped result of Construct and the result of a different handle are the inputs under test.
        Exception caught =
            Catch(
                action: () =>
                    ViewModelBase.CreateBase(
                        bindingScheduler: BindingScheduler.Immediate,
                        continuation: (output, construct) =>
                        {
                            output.AddDisposable(disposable: new Recording(log: log, name: "registered"));
                            _ = construct.Construct(constructor: static viewModelBase => viewModelBase);

                            return Constructor.From(values: other).Construct(constructor: static values => values);
                        }));
#pragma warning restore MC1002, MC1004

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(caught.Message).Contains(expected: "did not return the result of Construct");
        await Assert.That(log).IsEquivalentTo(expected: ["registered"]);
    }

    // Before, CreateBase read the value of a null result, and the caller got a NullReferenceException
    // with the registrations not released.
    [Test]
    public async Task AContinuationThatReturnsNullAfterConstructFailsAndReleasesTheRegistrations()
    {
        List<string> log = [];

#pragma warning disable MC1002, MC1003 // The dropped result of Construct and the null return are the inputs under test.
        Exception caught =
            Catch(
                action: () =>
                    ViewModelBase.CreateBase<ViewModelBase>(
                        bindingScheduler: BindingScheduler.Immediate,
                        continuation: (output, construct) =>
                        {
                            output.AddDisposable(disposable: new Recording(log: log, name: "registered"));
                            _ = construct.Construct(constructor: static viewModelBase => viewModelBase);

                            // ReSharper disable once NullableWarningSuppressionIsUsed - The null return is the input under test: CreateBase must refuse it at run time.
                            return null!;
                        }));
#pragma warning restore MC1002, MC1003

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(caught.Message).Contains(expected: "did not return the result of Construct");
        await Assert.That(log).IsEquivalentTo(expected: ["registered"]);
    }

    // The constructor failed, and the base released the registrations then. The continuation caught
    // the exception and returned a different value. No view model exists, thus CreateBase must not
    // return as if one did. The log shows that the base released the registration one time only.
    [Test]
    public async Task AContinuationThatCatchesAFailedConstructorAndReturnsADifferentValueFails()
    {
        List<string> log = [];

#pragma warning disable MC1004 // The result of a different handle after a failed constructor is the input under test.
        Exception caught =
            Catch(
                action: () =>
                    ViewModelBase.CreateBase(
                        bindingScheduler: BindingScheduler.Immediate,
                        continuation: (output, construct) =>
                        {
                            output.AddDisposable(disposable: new Recording(log: log, name: "registered"));

                            try
                            {
                                return construct.Construct<string>(
                                    constructor: static _ =>
                                        throw new NotSupportedException(message: "The constructor failed."));
                            }
                            catch (NotSupportedException)
                            {
                                return Constructor.From(values: "something else")
                                    .Construct(constructor: static values => values);
                            }
                        }));
#pragma warning restore MC1004

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(caught.Message).Contains(expected: "did not return the result of Construct");
        await Assert.That(log).IsEquivalentTo(expected: ["registered"]);
    }

    // The caller must see both errors, and the exception of the base is the first.
    [Test]
    public async Task ADroppedResultOfConstructWithAFailingRegistrationThrowsBothExceptions()
    {
        Exception releaseFailure = new InvalidOperationException(message: "The release failed.");

#pragma warning disable MC1002, MC1004 // The dropped result of Construct and the result of a different handle are the inputs under test.
        Exception caught =
            Catch(
                action: () =>
                    ViewModelBase.CreateBase(
                        bindingScheduler: BindingScheduler.Immediate,
                        continuation: (output, construct) =>
                        {
                            output.AddDisposable(
                                disposable: Disposable.FromAction(onDispose: () => throw releaseFailure));

                            _ = construct.Construct(constructor: static viewModelBase => viewModelBase);

                            return Constructor.From(values: "something else")
                                .Construct(constructor: static values => values);
                        }));
#pragma warning restore MC1002, MC1004

        Exception[] both = caught is AggregateException aggregate ? [.. aggregate.InnerExceptions] : [];

        await Assert.That(caught).IsTypeOf<AggregateException>();
        await Assert.That(both.Length).IsEqualTo(expected: 2);
        await Assert.That(both[0]).IsTypeOf<InvalidOperationException>();
        await Assert.That(both[0].Message).Contains(expected: "did not return the result of Construct");
        await Assert.That(both[1]).IsSameReferenceAs(expected: releaseFailure);
    }

    // CreateBase compares the value, and not the Constructed that holds it. The caller gets the view
    // model that owns the registrations, thus the result is correct.
    [Test]
    public async Task AContinuationThatReturnsTheSameViewModelFromADifferentHandleGetsIt()
    {
        List<string> log = [];

#pragma warning disable MC1001, MC1004 // The same view model returned through a different handle is the input under test.
        ViewModelBase viewModel =
            ViewModelBase.CreateBase(
                bindingScheduler: BindingScheduler.Immediate,
                continuation: (output, construct) =>
                {
                    output.AddDisposable(disposable: new Recording(log: log, name: "registered"));

                    ViewModelBase made =
                        construct.Construct(constructor: static viewModelBase => viewModelBase).Value;

                    return Constructor.From(values: made).Construct(constructor: static values => values);
                });
#pragma warning restore MC1001, MC1004

        await Assert.That(log).IsEmpty();

        viewModel.Dispose();

        await Assert.That(log).IsEquivalentTo(expected: ["registered"]);
    }

    // The handle that Select makes calls the constructor of the subclass in the Construct call of the
    // base. Thus, the value of that handle is the value that the base made.
    [Test]
    public async Task AContinuationThatConstructsThroughSelectGetsItsViewModel()
    {
        List<string> log = [];

        Derived viewModel =
            ViewModelBase.CreateBase(
                bindingScheduler: BindingScheduler.Immediate,
                continuation: (output, construct) =>
                {
                    output.AddDisposable(disposable: new Recording(log: log, name: "registered"));

                    return construct
                        .Select(selector: static viewModelBase => (Base: viewModelBase, Name: "selected"))
                        .Construct(constructor: static values => new Derived(viewModelBase: values.Base));
                });

        await Assert.That(log).IsEmpty();

        viewModel.Dispose();

        await Assert.That(log).IsEquivalentTo(expected: ["registered"]);
    }

    // A struct has no instance to compare. The handle keeps the value in a box, and an equal value is
    // the same value.
    [Test]
    public async Task AContinuationThatReturnsAStructGetsIt()
    {
        List<string> log = [];

        (ViewModelBase Base, int Count) made =
            ViewModelBase.CreateBase(
                bindingScheduler: BindingScheduler.Immediate,
                continuation: (output, construct) =>
                {
                    output.AddDisposable(disposable: new Recording(log: log, name: "registered"));

                    return construct.Construct(constructor: static viewModelBase => (Base: viewModelBase, Count: 3));
                });

        await Assert.That(made.Count).IsEqualTo(expected: 3);
        await Assert.That(log).IsEmpty();

        made.Base.Dispose();

        await Assert.That(log).IsEquivalentTo(expected: ["registered"]);
    }

    [Test]
    public async Task AContinuationThatReturnsADifferentStructFailsAndReleasesTheRegistrations()
    {
        List<string> log = [];

        ViewModelBase other =
            Create(
                register: static _ =>
                {
                });

#pragma warning disable MC1002, MC1004 // The dropped result of Construct and the result of a different handle are the inputs under test.
        Exception caught =
            Catch(
                action: () =>
                    ViewModelBase.CreateBase(
                        bindingScheduler: BindingScheduler.Immediate,
                        continuation: (output, construct) =>
                        {
                            output.AddDisposable(disposable: new Recording(log: log, name: "registered"));

                            _ =
                                construct.Construct(
                                    constructor: static viewModelBase => (Base: viewModelBase, Count: 3));

                            return Constructor.From(values: (Base: other, Count: 3))
                                .Construct(constructor: static values => values);
                        }));
#pragma warning restore MC1002, MC1004

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(caught.Message).Contains(expected: "did not return the result of Construct");
        await Assert.That(log).IsEquivalentTo(expected: ["registered"]);
    }

    // The continuation gets the base from Construct, and a different handle gives it to the view
    // model. CreateBase then gets a value that the base did not make, and it cannot know that this
    // value owns the base. Select adds values to the base and keeps the two values the same.
    [Test]
    public async Task ABaseThatADifferentHandleGivesToTheViewModelFails()
    {
        List<string> log = [];

#pragma warning disable MC1001, MC1004 // The base taken out and given to the view model through a different handle is the input under test.
        Exception caught =
            Catch(
                action: () =>
                    ViewModelBase.CreateBase(
                        bindingScheduler: BindingScheduler.Immediate,
                        continuation: (output, construct) =>
                        {
                            output.AddDisposable(disposable: new Recording(log: log, name: "registered"));

                            ViewModelBase taken =
                                construct.Construct(constructor: static viewModelBase => viewModelBase).Value;

                            return Constructor.From(values: taken)
                                .Construct(constructor: static values => new Derived(viewModelBase: values));
                        }));
#pragma warning restore MC1001, MC1004

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(caught.Message).Contains(expected: "did not return the result of Construct");
        await Assert.That(log).IsEquivalentTo(expected: ["registered"]);
    }

    // A second Construct call cannot make a second owner of the same entries. The handle that Select
    // makes refuses the call, and the base does not seal the output a second time.
    [Test]
    public async Task SecondConstructFails()
    {
#pragma warning disable MC1001 // The first Construct is used up for its value, so that the second call is the one under test.
        Exception caught =
            Catch(
                action: static () =>
                    ViewModelBase.CreateBase(
                        bindingScheduler: BindingScheduler.Immediate,
                        continuation: static (_, construct) =>
                        {
                            construct.Construct(constructor: static viewModelBase => viewModelBase).Value.Dispose();

                            return construct.Construct(constructor: static viewModelBase => viewModelBase);
                        }));
#pragma warning restore MC1001

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(caught.Message).Contains(expected: "Construct was already called");
    }

    // A field-like event keeps each handler, and the target of the handler, alive with the view model.
    [Test]
    public async Task PropertyChangedKeepsNoHandler()
    {
        ViewModelBase viewModel =
            Create(
                register: static _ =>
                {
                });

        WeakReference subscriber = Subscribe(viewModel: viewModel);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await Assert.That(subscriber.IsAlive).IsFalse();

        GC.KeepAlive(obj: viewModel);
    }

    [Test]
    public async Task PropertyChangedAcceptsTheRemovalOfAHandler()
    {
        ViewModelBase viewModel =
            Create(
                register: static _ =>
                {
                });

        Subscriber subscriber = new();

        viewModel.PropertyChanged += subscriber.OnPropertyChanged;
        viewModel.PropertyChanged -= subscriber.OnPropertyChanged;

        await Assert.That(viewModel).IsNotNull();
    }

    [Test]
    public async Task DisposeOnASubclassReleasesTheRegistrationsOfItsBase()
    {
        List<string> log = [];

        Derived viewModel =
            ViewModelBase.CreateBase(
                bindingScheduler: BindingScheduler.Immediate,
                continuation: (output, construct) =>
                {
                    output.AddDisposable(disposable: new Recording(log: log, name: "A"));
                    output.AddDisposable(disposable: new Recording(log: log, name: "B"));

                    return construct.Construct(
                        constructor: static viewModelBase => new Derived(viewModelBase: viewModelBase));
                });

        viewModel.Dispose();
        viewModel.Dispose();

        await Assert.That(log).IsEquivalentTo(expected: ["B", "A"], ordering: CollectionOrdering.Matching);
    }

    [Test]
    public async Task SubclassRefusesANullBase()
    {
        // ReSharper disable once NullableWarningSuppressionIsUsed - The null base is the input under test: the constructor must refuse it at run time.
        Exception caught = Catch(action: static () => _ = new Derived(viewModelBase: null!));

        await Assert.That(caught).IsTypeOf<ArgumentNullException>();
        await Assert.That((caught as ArgumentNullException)?.ParamName).IsEqualTo(expected: "viewModelBase");
    }

    // Two view models that own the same registrations could each dispose them while the other still
    // uses them. Thus, one subclass only can take a base, and the first one keeps it.
    [Test]
    public async Task SecondSubclassOfTheSameBaseFails()
    {
        List<string> log = [];

        ViewModelBase viewModelBase =
            Create(register: output => output.AddDisposable(disposable: new Recording(log: log, name: "A")));

        Derived first = new(viewModelBase: viewModelBase);
        Exception caught = Catch(action: () => _ = new Derived(viewModelBase: viewModelBase));

        first.Dispose();

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(caught.Message).Contains(expected: "already derives from this base");
        await Assert.That(log).IsEquivalentTo(expected: ["A"], ordering: CollectionOrdering.Matching);
    }

    // A different method, so that no local of the test keeps the subscriber alive.
    [MethodImpl(methodImplOptions: MethodImplOptions.NoInlining)]
    private static WeakReference Subscribe(INotifyPropertyChanged viewModel)
    {
        Subscriber subscriber = new();
        viewModel.PropertyChanged += subscriber.OnPropertyChanged;

        return new WeakReference(target: subscriber);
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

    private sealed class Derived(in ViewModelBase viewModelBase)
        : ViewModelBase(viewModelBase: viewModelBase);

    private sealed class Recording(in ICollection<string> log, in string name) : IDisposable
    {
        private ICollection<string> Log { get; } = log;

        private string Name { get; } = name;

        public void Dispose() => this.Log.Add(this.Name);
    }

    private sealed class Subscriber
    {
        // ReSharper disable once MemberCanBeMadeStatic.Local - The handler must be an instance method, so that the delegate refers to the subscriber that the test watches.
        public void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
        }
    }

    private sealed class RecordingListener(in ICollection<string> log, in string name) : IWeakListener
    {
        private ICollection<string> Log { get; } = log;

        private string Name { get; } = name;

        public void Unlisten() => this.Log.Add(this.Name);

        public IListenerWithWeakReference GetListenerWithWeakReference() =>
            throw new NotSupportedException(message: "ViewModelBase does not ask for this view.");
    }
}
