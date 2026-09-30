0.1.1

This package now targets net472, net6.0, and netstandard2.0, and not net10.0
only. The behavior is the same on every target.

Breaking change: ViewModelBase.CreateBase takes a
StageContinuation<IOutput, Constructor<ViewModelBase>, TResult>, and not an
IConstruct<ViewModelBase>. MorseCode.StagedConstruction 0.1.1 replaced the
IConstruct interface with the Constructor class, and this package depends on
that version or later. Change the signature of each view model that calls
CreateBase, and compile it again.

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
