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

A subclass must use each handle one time only. The handles do not check this.

This package is pre-1.0. Its API can change in a minor version until 1.0.0.

---

About this package

Construction of immutable objects in ordered stages, with no access to a partly
built object. It is part of the MorseCode toolkit, which holds the conventions
that MorseCode Software builds its own applications with.

Targets net10.0. No dependencies.

Source: https://github.com/MorseCode-Software/MorseCode.Toolkit
