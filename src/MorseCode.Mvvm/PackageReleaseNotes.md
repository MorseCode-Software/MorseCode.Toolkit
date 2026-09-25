0.1.0

The first release. It contains one type, CompositeDisposable.

A view model that SodaFlow builds holds subscriptions into the graph: one for
each bindable value, command, and asynchronous pipeline that it exposes. The
view model must release all of them when it is disposed. The usual way is a list
of IDisposable and a foreach loop in Dispose, and that loop has two defects. An
entry that throws stops the loop, thus each entry after it keeps its
subscription. And a second call to Dispose disposes each entry again.

CompositeDisposable replaces the list and the loop:

  private readonly CompositeDisposable disposables;

  private SearchViewModel(/* ... */)
  {
      // ...
      this.disposables = new(disposables: [query, results, summary, status]);
  }

  public void Dispose() => this.disposables.Dispose();

The first call to Dispose disposes each entry one time, in the order of the
list. Each subsequent call does nothing, also when two threads call at the same
time. An exception from one entry does not stop the entries after it. When all
entries are complete, one exception goes to the caller unchanged, and two or
more go to the caller in one AggregateException, in the order of the list.

A null entry fails at construction with an ArgumentException that gives its
index, and not later in Dispose. The composite keeps a copy of the list, thus a
subsequent change to the array that the caller gave has no effect.

This package is pre-1.0. Its API can change in a minor version until 1.0.0.

---

About this package

Building blocks for view models written in a functional reactive style on
SodaFlow. It is part of the MorseCode toolkit, which holds the conventions that
MorseCode Software builds its own applications with.

Targets net10.0. No dependencies.

Source: https://github.com/MorseCode-Software/MorseCode.Toolkit
