0.3.0

Breaking: BindableFactoryExtensions.CreateScrollRequest is no longer in this package, and the type
BindableFactoryExtensions is gone. It served only VirtualizingItemsControl, so it moved into
MorseCode.Controls.ViewModels 0.3.0, where it is not public. Use CreateVirtualItemsSource there to
make the scroll request with the rest of the source.

Changed: CreateBase now releases the registrations when its continuation returns without a call to
Construct, and fails with an InvalidOperationException that says so. It also releases them when the
continuation throws, before or after Construct, and lets the exception through. Before, a
continuation that registered a disposable or a bindable and then returned another value, or threw,
left those registrations attached to the graph with nothing to release them. A continuation that
calls Construct one time, as every view model does, sees no change.

Changed: the continuation of CreateBase returns the Constructed<TResult> that Construct gives, from
MorseCode.StagedConstruction 0.3.0, and CreateBase returns the value inside it. A continuation that
ends with return construct.Construct(...) compiles as it is. One that returns without a call to any
Construct no longer compiles, so the InvalidOperationException above is reached only by a null
return, or by the result of a handle that CreateBase did not give. This version needs
MorseCode.StagedConstruction 0.3.0.

Changed: a continuation that calls Construct inside ForwardReference<T>.WithoutCaptures no longer
compiles. The function of a forward reference must return T, and Construct now returns a
Constructed<T>. Put the forward reference outside CreateBase, so that its function returns what
CreateBase returns: ForwardReference<T>.WithoutCaptures(loop => CreateBase(scheduler, (output,
construct) => ...)). The forward reference builds one transaction of its own, so a Transaction.Run
in that continuation is no longer needed. A view model without a loop keeps its Transaction.Run.
The view models page shows the shape.

Changed: CreateBase now checks that the value it returns is the value that the constructor
returned, which is the same instance for a class and an equal value for a struct. A continuation
that calls Construct and then returns null, or the result of a different handle, fails with an
InvalidOperationException, and CreateBase releases the registrations. Before, CreateBase returned
the other value, the view model that was constructed went to nobody, and nothing released its
registrations. A null return after the call failed with a NullReferenceException. A continuation
that returns what Construct gives, directly or through Select, sees no change. One that takes the
ViewModelBase out with one Construct call and gives it to the view model through a second handle
now fails; use Select to add values to the base.

New: a project that references this package gets the continuation rules MC1001 to MC1007, from the
analyzer inside MorseCode.StagedConstruction 0.3.0. They report, in the build, the usual forms of
the continuation that CreateBase refuses when it runs. Six are warnings, so a project that treats
warnings as errors and has such a continuation no longer builds. The notes of
MorseCode.StagedConstruction 0.3.0 list the rules.

New: the package has a net8.0 build, next to net472, net6.0, and netstandard2.0. It is the same
code, compiled for net8.0. A project that targets net8.0 or later now takes the net8.0 build and
not the net6.0 build. MorseCode.Controls.UI.Avalonia depends on this package through
MorseCode.Controls.ViewModels, and it targets net8.0.

New: the net8.0 build is marked as compatible with trimming and native AOT. The build of the
package runs the trim, AOT, and single-file analyzers on it and fails on a report, so code that
needs reflection cannot be released by mistake. The assembly carries the metadata IsTrimmable and
IsAotCompatible, so an app that trims only the assemblies so marked, with TrimMode partial, now
trims this one too. The net472, net6.0, and netstandard2.0 builds are not marked, because the SDK
has no such analyzers for them. A native AOT publish that roots the assembly reports nothing in its
code. It reports IL2091 inside SodaFlow.Core 5, which this package depends on, where SodaFlow.Core
gives a value factory to Lazy<T>. That is SodaFlow code, which the mark of this package does not
cover.

Changed: the dependencies on SodaFlow packages now have a ceiling at their next major version, as
the dependencies between the packages of this repository already did: SodaFlow.Bindable.ObjectModel
[4.0.0, 5.0.0). Before, each was a floor only. A project that asks for a SodaFlow major past the
ceiling now gets NuGet warning NU1608 at restore, where before it got nothing and could fail at run
time. A SodaFlow major therefore needs a release of this package, even when nothing here changes.

0.2.4

New: BindableFactoryExtensions.CreateScrollRequest, an extension method of
IBindableFactory. It makes the two-way bindable value of Maybe<int> that asks
VirtualizingItemsControl to scroll, from a Stream<int> of positions. The value
starts as none, each position makes it that position, and the write of none by
the control after it scrolls clears it. The method makes the sink for the writes
of the view, so you do not. An overload also takes a Stream<Unit> of clears, which
set the value to none, for a view model that withdraws a request. When a request
and a clear come in one transaction, the request wins. Both overloads refuse a
null argument with an ArgumentNullException that names it.

No existing type changed, and the package has no new dependency. The
documentation page for VirtualizingItemsControl shows the method.

0.2.3

No signature or behavior changed. This release reformats the code and the
XMLDoc comments of the package to the wrap limits of the repository, 120
characters for code and 80 for XMLDoc. The text of the XMLDoc comments is the
same, so the API reference reads the same. You do not need to change or compile
again any code that uses 0.2.2.

0.2.2

The registrations of a view model now stay open while the constructor that
Construct calls runs, and they close when that constructor returns. In 0.2.1
and before, they closed before the constructor ran, so a registration in the
constructor of a view model failed. Now the constructor of a view model that
derives from ViewModelBase can add its own subscriptions to IOutput, and Dispose
releases them with the others. A registration after the constructor returns
still fails with an InvalidOperationException.

Dispose on the base now releases the registrations made so far, also when it
runs inside the constructor or on a different thread, and it closes the
registrations. If the constructor throws, the base releases the registrations
made so far, and the exception goes to the caller unchanged. If a registration
also throws as it is released, the caller gets an AggregateException that holds
the exception of the constructor first. The message of the
InvalidOperationException for a late registration now says that the
registrations are closed. No signature changed.

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
