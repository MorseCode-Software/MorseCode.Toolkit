0.2.1

ViewModelBase is no longer sealed. A view model can derive from it: its
constructor gives the base that Construct gives to the new protected
constructor, ViewModelBase(ViewModelBase), and the view model then is an
IViewModel with no members of its own for that. The protected constructor takes
the registrations of that base, and Dispose on the view model releases them.

One view model only can take a base. A second call to the protected constructor
with the same base fails with an InvalidOperationException, because two view
models cannot own the same registrations. A null base fails with an
ArgumentNullException.

Dispose now calls GC.SuppressFinalize, so a view model that derives from
ViewModelBase and adds a finalizer does not have to implement IDisposable again.

A view model that keeps the base in a field, as in 0.2.0, works as before. This
release adds to the API and changes nothing that a view model built against
0.2.0 uses.

0.2.0

This package now targets net472, net6.0, and netstandard2.0, and not net10.0
only. The behavior is the same on every target.

Breaking change: ViewModelBase.CreateBase takes a
StageContinuation<IOutput, Constructor<ViewModelBase>, TResult>, and not an
IConstruct<ViewModelBase>. MorseCode.StagedConstruction 0.2.0 replaced the
IConstruct interface with the Constructor class, and this package depends on
0.2.0 or a later 0.2.x. Change the signature of each view model that calls
CreateBase, and compile it again.

CreateBase now refuses a null continuation with an ArgumentNullException, as it
refuses a null scheduler.

The dependency on MorseCode.StagedConstruction now has the next minor version as
its ceiling while the version is below 1.0.0, and not 1.0.0. MorseCode.Mvvm
0.1.0 admits MorseCode.StagedConstruction 0.2.0, and does not work with it. Do
not use MorseCode.Mvvm 0.1.0 with MorseCode.StagedConstruction 0.2.0 or later.

0.1.0

The first release. It contains ViewModelBase, the IViewModel interface, and the
Disposable helpers that ViewModelBase uses.

A view model that SodaFlow builds holds subscriptions into the graph: one for
each bindable value, command, and listener that it exposes. The view model must
release all of them when it is disposed, and must not read a part of itself
before that part exists.

ViewModelBase does both, with MorseCode.StagedConstruction. A view model's
static Create method calls ViewModelBase.CreateBase. The base gives it an
ViewModelBase.IOutput to register each subscription with, and its
BindableFactory registers each bindable value and action that it makes. When
the view model calls Construct, the base seals the registrations and gives
itself to the view model's constructor. The view model keeps it and sends its
Dispose and PropertyChanged to it:

  public static CounterViewModel Create(IBindingScheduler bindingScheduler) =>
      Transaction.Run(() =>
          ViewModelBase.CreateBase(bindingScheduler, (output, construct) =>
          {
              StreamSink<Unit> increments = Stream.CreateSink<Unit>();
              Cell<int> count = increments.Accum(0, (_, total) => total + 1);

              IOneWayBindableValue<int> countValue = output.BindableFactory.CreateOneWay(count);
              IBindableAction increment = output.BindableFactory.CreateBindableAction(increments);

              return construct.Construct(viewModelBase =>
                  new CounterViewModel(viewModelBase, countValue, increment));
          }));

Dispose releases each registration one time, the last registration first,
listeners and disposables together. Thus, a registration stops before the
registrations that it uses. A second call does nothing, also when two threads
call at the same time. An exception from one registration does not stop the
others.

A registration after Construct fails with an InvalidOperationException, and so
does a second Construct. Thus, nothing that the construction made can be left
out of Dispose, and nothing can be added to it later. A null registration fails
at the call, with an ArgumentNullException.

AddListener keeps a reference to the listener. Thus, the garbage collector does
not remove a weak listener while the view model is alive.

PropertyChanged never occurs and keeps no handler. The properties of a view
model do not change, and each bindable value sends its own notifications. The
event is there for INotifyPropertyChanged, because WPF watches a binding source
without that interface through a PropertyDescriptor, which keeps the source
alive.

Disposable.Composite, Disposable.FromAction, and Disposable.Empty are public
too. Composite disposes a fixed list as one, in the order of the list, with the
once-only and exception behavior above. FromAction calls an action at the first
Dispose only. Empty does nothing.

This package is pre-1.0. Its API can change in a minor version until 1.0.0.

---

About this package

Building blocks for view models written in a functional reactive style on
SodaFlow. It is part of the MorseCode toolkit, which holds the conventions that
MorseCode Software builds its own applications with.

Targets net472, net6.0, and netstandard2.0. Depends on SodaFlow.Bindable.ObjectModel and
MorseCode.StagedConstruction.

Source: https://github.com/MorseCode-Software/MorseCode.Toolkit
