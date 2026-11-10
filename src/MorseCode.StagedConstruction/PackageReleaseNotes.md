0.3.0

Breaking: Constructor<T>.Construct and Stage<,,>.Advance return a Constructed<TResult>, and a
StageContinuation returns one, so the continuation of a subclass must return what Construct gives
it. Only Construct makes a Constructed<TResult>. A continuation that returns without a call to
Construct therefore no longer compiles; before, a base found the mistake at run time, or not at
all. A CreateBase method reads Value on what the continuation returns and gives that to its
caller. A continuation that ends with return construct.Construct(...), or with return
next.Advance(...), compiles as it is; code that wrote the return type of such a lambda, or a
CreateBase that returned the continuation's result directly, changes to the new type. ConstructCore
is unchanged and still returns TResult; AdvanceCore returns the Constructed<TResult> that its
continuation returns. StageContinuation loses out on TResult, because a sealed class cannot be
covariant; no caller in this repository used that variance.

New: the package carries a Roslyn analyzer, under analyzers/dotnet/cs, for the continuation that a
subclass gives to a base. The compiler checks only that a continuation returns a
Constructed<TResult>. The analyzer reports the usual ways to return one that did not come from the
handle of the continuation. MC1001 reports Value read directly from Construct or Advance on the
handle. MC1002 reports a result of that call that is not used. MC1003 reports a null return. MC1004
reports the result of a handle that the continuation made with Constructor.From. MC1005 reports a
function for Construct that does not read the values of the base. MC1006 reports a continuation
that does not use its handle. These six are warnings, and each reports only what it can prove.
MC1007 is information: it reports where the handle goes to code that the analyzer cannot follow,
such as a helper method, a field, or a lambda that can run after the continuation returns. A C#
project that has this package anywhere in its packages runs the analyzer with no step of its own,
and a rule is turned off by its severity in .editorconfig. The documentation page on the
continuation rules describes each one.

New: the package has a net8.0 build, next to net472, net6.0, and netstandard2.0. It is the same
code, compiled for net8.0. A project that targets net8.0 or later now takes the net8.0 build and
not the net6.0 build. No signature or behavior changed. The reason is that
MorseCode.Avalonia.Controls targets net8.0 and references MorseCode.Mvvm, which references this
package, and the documentation build needs one target that all three have.

0.2.1

No signature or behavior changed. This release reformats the code and the
XMLDoc comments of the package to the wrap limits of the repository, 120
characters for code and 80 for XMLDoc, and it makes the private members of two
internal types read-only fields. The text of the XMLDoc comments is the same, so
the API reference reads the same. You do not need to change or compile again
any code that uses 0.2.0.

0.2.0

This package now targets net472, net6.0, and netstandard2.0, and not net10.0
only. The behavior is the same on every target.

Breaking change: the IConstruct and IStage interfaces are gone. The abstract
classes Constructor<TBaseValues> and Stage<TInput, TOutput, TNext> replace them.
.NET Framework and .NET Standard 2.0 do not support a default interface member,
so Select could not stay on the interface, and an interface cannot force an
implementer to check for a second call. Each class has a public Construct or
Advance method that checks for a second call, also when two threads call at the
same time, and also when the first call fails. Then it calls a method that a
derived class implements: ConstructCore or AdvanceCore. The static class that
made the usual handles, Construct, is now Constructor.

Select works the same way. It is not virtual: it refuses a null selector, uses
the handle, and then calls the virtual method SelectCore, which a derived class
can override.

Behavior change: Select now uses the handle that it is called on. In 0.1.0, a
Construct on the source after a Select succeeded, and the selected handle then
failed. Now the Construct on the source fails, with a message that says Select
used the handle. A second Select on the same handle also fails. Thus, the
values of a base reach one constructor only, also when a derived class
overrides SelectCore.

Construct and Advance now refuse a null constructor or continuation with an
ArgumentNullException, before they use the handle. In 0.1.0, the call used the
handle and then failed with a NullReferenceException.

MorseCode.Mvvm 0.1.0 does not work with this version, although the dependency
range that it was released with admits this version. Use MorseCode.Mvvm 0.2.0
or later.

You must change every signature that names the old types. For example,
StageContinuation<string, IConstruct<AnimalValues>, TResult> becomes
StageContinuation<string, Constructor<AnimalValues>, TResult>, and
Construct.From(values) becomes Constructor.From(values). Compile again any
assembly that you built against 0.1.0.

The classes are not variant, as the interfaces were. A Constructor<Dog> does not
convert to a Constructor<Animal>. A helper that must accept both is generic in
the values type, for example
Describe<T>(Constructor<T> handle) where T : AnimalValues. To change the values
type of a handle, call Select.

0.1.0

The first release. It contains the types for staged construction: the
StageContinuation callback, the IStage and IConstruct handles, and the Stage
and Construct helpers that make the usual handles.

A C# constructor that calls a base constructor has three problems. The order of
the two constructors is fixed. Each constructor can read this before the object
is complete. And a virtual member that the base constructor calls can read a
field of the subclass before the subclass sets it.

Staged construction removes all three. A static Create method builds each value
in a local variable and gives the finished values to a private constructor that
only assigns them. A base does its part in a static CreateBase method. It gives
its output to the subclass through a callback, together with a handle. The
handle is either the next stage, which takes more input from the subclass, or
an IConstruct, which gives the values of the base to the constructor of the
subclass:

  public static TResult CreateBase<TResult>(
      string name,
      StageContinuation<string, IConstruct<AnimalValues>, TResult> continuation) =>
      continuation(name.ToUpperInvariant(), Construct.From(new AnimalValues(name)));

  public static Dog Create(string name) =>
      Animal.CreateBase(name, (output, construct) =>
          construct.Construct(animal => new Dog(animal, bark: output + "!")));

The compiler infers TResult from the lambdas, thus a Create method writes no
type arguments. A base with more than one stage names every stage in the type
of its callback, in order, for example
StageContinuation<TOutput1, IStage<TInput2, TOutput2, IConstruct<TValues>>, TResult>.
The subclass cannot reach the constructor before the base finishes, when the
base keeps the constructor of its values private.

Construct.From(values) makes the usual final handle. Select on an IConstruct
lets a base that has a base of its own extend the values of that base. The
selector runs when the subclass constructs, and not before. Stage.From(body)
makes a stage from a function that takes the input of the subclass and returns
the output and the next handle. A null selector or body fails when Select or
Stage.From is called, and not later.

A subclass must use each handle one time only, and the handles that
Construct.From, Select, and Stage.From make check this: a second Construct or
Advance throws an InvalidOperationException, also when two threads call at the
same time. The first call uses the handle even if the constructor or stage
throws, so a failed construction cannot be run again. A base that implements
IConstruct or IStage by hand should make the same check.

This package is pre-1.0. Its API can change in a minor version until 1.0.0.

---

About this package

Construction of immutable objects in ordered stages, with no access to a partly
built object. It is part of the MorseCode toolkit, which holds the conventions
that MorseCode Software builds its own applications with.

Targets net472, net6.0, and netstandard2.0. No dependencies.

Source: https://github.com/MorseCode-Software/MorseCode.Toolkit
