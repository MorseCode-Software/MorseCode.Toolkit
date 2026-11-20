using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using MorseCode.Analyzers.Tests;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.StagedConstruction.Analyzers.Tests;

/// <summary>
///     Compiles samples of code with <see cref="ContinuationAnalyzer" />, and reads
///     what it reports.
/// </summary>
internal sealed class ContinuationAnalyzerTests
{
    // The usual continuation: Construct on the handle, and its result returned.
    [Test]
    public async Task AContinuationThatReturnsConstructGivesNoReport()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    source: Samples.OfBase(continuation: $"(output, construct) => construct.Construct({Samples.Make})"))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found).IsEmpty();
    }

    // A Select of the handle is the handle, and so is a local variable that holds one. A local variable
    // can also hold the result.
    [Test]
    public async Task ASelectAndALocalVariableAreTheHandle()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    source: Samples.OfBase(
                        continuation: $$"""
                                        (output, construct) =>
                                        {
                                            Constructor<Values> selected = construct.Select(selector: static values => values);
                                            Constructed<Thing> made = selected.Construct({{Samples.Make}});

                                            return made;
                                        }
                                        """))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found).IsEmpty();
    }

    // A base with a base of its own gives the handle to the continuation of its subclass. It does
    // that directly, in the function of Stage.From, and in a stage of its own type. The second is the
    // shape of the Workbench sample.
    [Test]
    public async Task ABaseThatGivesItsHandleToTheNextStepGivesNoReport()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    source: Samples.With(
                        code: """
                              public static class Middle
                              {
                                  public static TResult Direct<TResult>(
                                      StageContinuation<string, Constructor<Values>, TResult> continuation) =>
                                      Base.Create(
                                          continuation: (output, construct) =>
                                              continuation(output: output, next: construct.Select(selector: static values => values)));

                                  public static TResult InAStage<TResult>(
                                      StageContinuation<string, Stage<int, string, Constructor<Values>>, TResult> continuation) =>
                                      Base.Create(
                                          continuation: (output, construct) =>
                                              continuation(
                                                  output: output,
                                                  next: Stage.From<int, string, Constructor<Values>>(
                                                      body: input =>
                                                      {
                                                          string text = $"{input}";

                                                          return (Output: text,
                                                              Next: construct.Select(selector: static values => values));
                                                      })));

                                  public static TResult InItsOwnStage<TResult>(
                                      StageContinuation<string, Stage<int, string, Constructor<Values>>, TResult> continuation) =>
                                      Base.Create(
                                          continuation: (output, construct) =>
                                              continuation(output: output, next: new OwnStage(handle: construct)));
                              }
                              """))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found).IsEmpty();
    }

    // Advance uses the handle of a stage, and the continuation of the stage has a handle of its own.
    [Test]
    public async Task AContinuationThatAdvancesAStageGivesNoReport()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    source: Samples.OfStaged(
                        continuation: $"""
                                       (output, stage) =>
                                           stage.Advance(
                                               input: 1,
                                               continuation: static (second, construct) => construct.Construct({Samples.Make}))
                                       """))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found).IsEmpty();
    }

    // The shape to use for a view model with a loop has the forward reference around the base, and
    // its continuation is the usual one. Two other shapes compile. One has the forward reference in the
    // function of Construct. One has Construct in the forward reference, and keeps the Constructed in a
    // variable. It reads Value from the variable, and not directly from the call. No rule reports one
    // of the three.
    [Test]
    public async Task AViewModelWithALoopGivesNoReport()
    {
        IReadOnlyList<Diagnostic> outside =
            await Analyze(
                    source: Samples.With(
                        code: $$"""
                                public static class Use
                                {
                                    public static Thing Go() =>
                                        ForwardReference<Thing>.WithoutCaptures(
                                            loop => Base.Create(continuation: (output, construct) => construct.Construct({{Samples.Make}})));
                                }
                                """))
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> inside =
            await Analyze(
                    source: Samples.OfBase(
                        continuation: """
                                      (output, construct) =>
                                          Transaction.Run(
                                              () =>
                                                  construct.Construct(
                                                      constructor: values =>
                                                          ForwardReference<Thing>.WithoutCaptures(loop => new Thing(values: values))))
                                      """))
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> around =
            await Analyze(
                    source: Samples.OfBase(
                        continuation: $$"""
                                        (output, construct) =>
                                            Transaction.Run(
                                                () =>
                                                    ForwardReference<Thing>.WithCaptures(
                                                            loop =>
                                                            {
                                                                Constructed<Thing> made = construct.Construct({{Samples.Make}});

                                                                return (made.Value, made);
                                                            })
                                                        .Captures)
                                        """))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(outside).IsEmpty();
        await Assert.That(inside).IsEmpty();
        await Assert.That(around).IsEmpty();
    }

    // Each member that the analyzer knows by name calls its function before it returns. A sample that
    // names a member that SodaFlow does not have does not compile. Thus, this test also compares the
    // list of names with the library.
    [Test]
    public async Task EachKnownMemberCanHoldTheHandleInItsFunction()
    {
        string[] calls =
        [
            "Transaction.RunVoid(() => made = construct.Construct(MAKE));",
            "made = Transaction.Run(() => construct.Construct(MAKE));",
            "_ = ForwardReference<int>.WithoutCaptures(loop => { made = construct.Construct(MAKE); return 1; });",
            "made = ForwardReference<int>.WithCaptures(loop => (1, construct.Construct(MAKE))).Captures;",
            "_ = Cell.Loop<int>().WithoutCaptures(loop => { made = construct.Construct(MAKE); return Cell.Constant(1); });",
            "made = Cell.Loop<int>().WithCaptures(loop => (Cell.Constant(1), construct.Construct(MAKE))).Captures;",
            "_ = Stream.Loop<int>().WithoutCaptures(loop => { made = construct.Construct(MAKE); return Stream.Never<int>(); });",
            "made = Stream.Loop<int>().WithCaptures(loop => (Stream.Never<int>(), construct.Construct(MAKE))).Captures;",
            "_ = Behavior.Loop<int>().WithoutCaptures(loop => { made = construct.Construct(MAKE); return Behavior.Constant(1); });",
            "made = Behavior.Loop<int>().WithCaptures(loop => (Behavior.Constant(1), construct.Construct(MAKE))).Captures;"
        ];

        foreach (string call in calls)
        {
            IReadOnlyList<Diagnostic> found =
                await Analyze(
                        source: Samples.OfBase(
                            continuation: $$"""
                                            (output, construct) =>
                                            {
                                                Constructed<Thing>? made = null;
                                                {{call.Replace(oldValue: "MAKE", newValue: Samples.Make)}}

                                                return made ?? throw new InvalidOperationException();
                                            }
                                            """))
                    .ConfigureAwait(continueOnCapturedContext: false);

            await Assert.That(Ids(found: found)).IsEmpty().Because(message: call);
        }
    }

    // InstantHandle on a parameter says that the method calls the function before it returns. The
    // analyzer reads the attribute on a method of the same project.
    [Test]
    public async Task AParameterWithInstantHandleCanHoldTheHandleInItsFunction()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    source: Samples.OfBase(
                        continuation:
                        $"(output, construct) => Helpers.Now(f: () => construct.Construct({Samples.Make}))"))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found).IsEmpty();
    }

    // A stage can give a value that is not a handle to its continuation. That continuation has no
    // handle to construct with, thus no rule applies, also when it returns null or the result of
    // Constructor.From.
    [Test]
    public async Task AContinuationWithoutAHandleIsNotExamined()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    source: Samples.With(
                        code: """
                              public static class Use
                              {
                                  public static int Sum() =>
                                      Stage.From<int, int, int>(body: static input => (Output: input, Next: input))
                                          .Advance(input: 1, continuation: static (output, next) => Done.Of(value: output + next))
                                          .Value;

                                  public static Constructed<int> Nothing() =>
                                      Stage.From<int, int, int>(body: static input => (Output: input, Next: input))
                                          .Advance<int>(input: 1, continuation: static (_, _) => null!);
                              }
                              """))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found).IsEmpty();
    }

    // A continuation that only throws returns no value. A function for Construct that only throws
    // returns none either. Neither has a result to get wrong.
    [Test]
    public async Task ALambdaThatOnlyThrowsGivesNoReport()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    source: Samples.With(
                        code: """
                              public static class Use
                              {
                                  public static Thing Discard() =>
                                      Base.Create<Thing>(continuation: static (_, _) => throw new InvalidOperationException());

                                  public static Thing Block() =>
                                      Base.Create<Thing>(
                                          continuation: static (output, construct) =>
                                          {
                                              throw new InvalidOperationException(output);
                                          });

                                  public static Thing Function() =>
                                      Base.Create(
                                          continuation: static (output, construct) =>
                                              construct.Construct<Thing>(
                                                  constructor: static _ => throw new InvalidOperationException()));
                              }
                              """))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found).IsEmpty();
    }

    // MC1001. A read of Value directly from the call does not keep the Constructed. The call is
    // Construct in the first sample, and Advance in the second.
    [Test]
    public async Task ValueReadDirectlyFromTheCallIsReported()
    {
        IReadOnlyList<Diagnostic> construct =
            await Analyze(
                    source: Samples.OfBase(
                        continuation:
                        $"(output, construct) => Done.Of(value: construct.Construct({Samples.Make}).Value)"))
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> advance =
            await Analyze(
                    source: Samples.OfStaged(
                        continuation: $"""
                                       (output, stage) =>
                                           Done.Of(
                                               value: stage.Advance(
                                                       input: 1,
                                                       continuation: static (second, construct) => construct.Construct({Samples.Make}))
                                                   .Value)
                                       """))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Ids(found: construct))
            .IsEquivalentTo(expected: ["MC1001"], ordering: CollectionOrdering.Matching);

        await Assert.That(Covered(found: construct))
            .IsEquivalentTo(expected: ["Value"], ordering: CollectionOrdering.Matching);

        await Assert.That(construct[0].GetMessage())
            .IsEqualTo(
                expected:
                "Value is read directly from the call to Construct, so the continuation cannot return the result of "
                + "that call: return the result, or keep it in a variable and return that");

        await Assert.That(Ids(found: advance))
            .IsEquivalentTo(expected: ["MC1001"], ordering: CollectionOrdering.Matching);

        await Assert.That(advance[0].GetMessage()).Contains(expected: "the call to Advance");
    }

    // MC1002. A statement that is only the call, and an assignment of the call to a discard.
    [Test]
    public async Task AResultThatIsNotUsedIsReported()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    source: Samples.OfBase(
                        continuation: $$"""
                                        (output, construct) =>
                                        {
                                            if (Use.Flag)
                                            {
                                                construct.Construct({{Samples.Make}});
                                            }
                                            else
                                            {
                                                _ = construct.Construct({{Samples.Make}});
                                            }

                                            return Done.Of(value: new Thing(values: new Values()));
                                        }
                                        """))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Ids(found: found))
            .IsEquivalentTo(expected: ["MC1002", "MC1002"], ordering: CollectionOrdering.Matching);

        await Assert.That(Covered(found: found))
            .IsEquivalentTo(expected: ["Construct", "Construct"], ordering: CollectionOrdering.Matching);

        await Assert.That(found[0].GetMessage())
            .IsEqualTo(expected: "The result of the call to Construct is not used: return it from the continuation");
    }

    // MC1003. A null on one path, and a continuation that is only a null. The second one does not use
    // its handle either, which is MC1006.
    [Test]
    public async Task ANullReturnIsReported()
    {
        IReadOnlyList<Diagnostic> onePath =
            await Analyze(
                    source: Samples.OfBase(
                        continuation: $"(output, construct) => Use.Flag ? construct.Construct({Samples.Make}) : null!"))
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> only =
            await Analyze(
                    source: Samples.With(
                        code: """
                              public static class Use
                              {
                                  public static Thing Go() => Base.Create<Thing>(continuation: static (output, _) => default!);
                              }
                              """))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Ids(found: onePath))
            .IsEquivalentTo(expected: ["MC1003"], ordering: CollectionOrdering.Matching);

        await Assert.That(onePath[0].GetMessage())
            .IsEqualTo(expected: "The continuation returns null: return the result of its handle construct");

        await Assert.That(Ids(found: only))
            .IsEquivalentTo(expected: ["MC1006", "MC1003"], ordering: CollectionOrdering.Matching);
    }

    // MC1004. Each sample returns the result of a handle from Constructor.From on one path. The path
    // is one side of a conditional, a catch, a Select with two local variables, and the function of
    // Transaction.Run. On the other path, the continuation uses its own handle, thus no other rule
    // reports.
    [Test]
    public async Task TheResultOfAHandleFromConstructorFromIsReported()
    {
        IReadOnlyList<Diagnostic> onePath =
            await Analyze(
                    source: Samples.OfBase(
                        continuation:
                        $"(output, construct) => Use.Flag ? construct.Construct({Samples.Make}) : {Samples.Foreign}"))
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> inACatch =
            await Analyze(
                    source: Samples.OfBase(
                        continuation: $$"""
                                        (output, construct) =>
                                        {
                                            try
                                            {
                                                return construct.Construct({{Samples.Make}});
                                            }
                                            catch (InvalidOperationException)
                                            {
                                                return {{Samples.Foreign}};
                                            }
                                        }
                                        """))
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> throughVariables =
            await Analyze(
                    source: Samples.OfBase(
                        continuation: $$"""
                                        (output, construct) =>
                                        {
                                            Constructor<Thing> other = Constructor.From(values: new Thing(values: new Values()));

                                            Constructed<Thing> made =
                                                other.Select(selector: static thing => thing).Construct(constructor: static thing => thing);

                                            return Use.Flag ? construct.Construct({{Samples.Make}}) : made;
                                        }
                                        """))
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> inATransaction =
            await Analyze(
                    source: Samples.OfBase(
                        continuation:
                        $"(output, construct) => Transaction.Run(() => Use.Flag ? construct.Construct({Samples.Make}) : {Samples.Foreign})"))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Ids(found: onePath))
            .IsEquivalentTo(expected: ["MC1004"], ordering: CollectionOrdering.Matching);

        await Assert.That(Covered(found: onePath))
            .IsEquivalentTo(expected: [Samples.Foreign], ordering: CollectionOrdering.Matching);

        await Assert.That(onePath[0].GetMessage())
            .IsEqualTo(
                expected:
                "The continuation returns the result of a handle from Constructor.From, and not the result of its "
                + "handle construct");

        await Assert.That(Ids(found: inACatch))
            .IsEquivalentTo(expected: ["MC1004"], ordering: CollectionOrdering.Matching);

        await Assert.That(Ids(found: throughVariables))
            .IsEquivalentTo(expected: ["MC1004"], ordering: CollectionOrdering.Matching);

        await Assert.That(Covered(found: throughVariables))
            .IsEquivalentTo(expected: ["made"], ordering: CollectionOrdering.Matching);

        await Assert.That(Ids(found: inATransaction))
            .IsEquivalentTo(expected: ["MC1004"], ordering: CollectionOrdering.Matching);
    }

    // MC1004 follows a returned local variable to each value that can get to the return. A subsequent write
    // replaces an earlier write. The analyzer follows a chain of variables, and each path through a branch,
    // a loop, and a try statement counts. An out argument and a write in a lambda give a value that the analyzer cannot
    // know, thus they give no report. Before, the analyzer read only the declaration of the variable. The
    // chain does not use the handle, which is MC1006.
    [Test]
    public async Task AReturnedVariableIsFollowedToEachValueThatCanGetToTheReturn()
    {
        IReadOnlyList<Diagnostic> replaced =
            await AnalyzeBody(
                    statements: $"""
                                 Constructed<Thing> made = {Samples.Foreign};
                                 made = construct.Construct({Samples.Make});
                                 return made;
                                 """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> chain =
            await AnalyzeBody(
                    statements: $"""
                                 Constructed<Thing> first = {Samples.Foreign};
                                 Constructed<Thing> second = first;
                                 return second;
                                 """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> branch =
            await AnalyzeBody(
                    statements: $$"""
                                  Constructed<Thing> made = construct.Construct({{Samples.Make}});

                                  if (Use.Flag)
                                  {
                                      made = {{Samples.Foreign}};
                                  }

                                  return made;
                                  """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> loopThatRunsOnce =
            await AnalyzeBody(
                    statements: $$"""
                                  Constructed<Thing> made = {{Samples.Foreign}};

                                  do
                                  {
                                      made = construct.Construct({{Samples.Make}});
                                  }
                                  while (Use.Flag);

                                  return made;
                                  """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> loopLeftAtABreak =
            await AnalyzeBody(
                    statements: $$"""
                                  Constructed<Thing> made = {{Samples.Foreign}};

                                  while (true)
                                  {
                                      made = construct.Construct({{Samples.Make}});

                                      break;
                                  }

                                  return made;
                                  """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> tryAndCatch =
            await AnalyzeBody(
                    statements: $$"""
                                  Constructed<Thing> made = construct.Construct({{Samples.Make}});

                                  try
                                  {
                                      made = {{Samples.Foreign}};
                                  }
                                  catch (InvalidOperationException)
                                  {
                                      made = construct.Construct({{Samples.Make}});
                                  }

                                  return made;
                                  """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> outArgument =
            await AnalyzeBody(
                    statements: $"""
                                 Constructed<Thing> made = {Samples.Foreign};
                                 Replace(value: out made);

                                 return made;

                                 void Replace(out Constructed<Thing> value) => value = construct.Construct({Samples.Make});
                                 """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> writtenInALambda =
            await AnalyzeBody(
                    statements: $"""
                                 Constructed<Thing> made = {Samples.Foreign};
                                 _ = Helpers.Now(f: () => made = construct.Construct({Samples.Make}));

                                 return made;
                                 """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(replaced).IsEmpty();

        await Assert.That(Ids(found: chain))
            .IsEquivalentTo(expected: ["MC1006", "MC1004"], ordering: CollectionOrdering.Matching);

        await Assert.That(Covered(found: chain))
            .IsEquivalentTo(expected: ["construct", "second"], ordering: CollectionOrdering.Matching);

        await Assert.That(Ids(found: branch))
            .IsEquivalentTo(expected: ["MC1004"], ordering: CollectionOrdering.Matching);

        await Assert.That(loopThatRunsOnce).IsEmpty();
        await Assert.That(loopLeftAtABreak).IsEmpty();

        await Assert.That(Ids(found: tryAndCatch))
            .IsEquivalentTo(expected: ["MC1004"], ordering: CollectionOrdering.Matching);

        await Assert.That(outArgument).IsEmpty();
        await Assert.That(writtenInALambda).IsEmpty();
    }

    // A write in a finally block and a write through a ref local can occur out of the order of the graph.
    // Thus, the variable gives no value, and no report. A lambda can read a variable of the continuation at
    // any time. Thus, it gets a value only from a variable with one write. In the last sample, that write
    // is the result of a different handle. That sample does not use the handle, which is MC1006.
    [Test]
    public async Task AVariableThatCanChangeOutOfOrderIsNotFollowed()
    {
        IReadOnlyList<Diagnostic> finallyWrite =
            await AnalyzeBody(
                    statements: $$"""
                                  Constructed<Thing> made = {{Samples.Foreign}};

                                  try
                                  {
                                      made = {{Samples.Foreign}};
                                  }
                                  finally
                                  {
                                      made = construct.Construct({{Samples.Make}});
                                  }

                                  return made;
                                  """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> refAlias =
            await AnalyzeBody(
                    statements: $"""
                                 Constructed<Thing> made = {Samples.Foreign};
                                 ref Constructed<Thing> alias = ref made;
                                 alias = construct.Construct({Samples.Make});

                                 return made;
                                 """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> readInALambdaAfterTwoWrites =
            await AnalyzeBody(
                    statements: $$"""
                                  Constructed<Thing> made = construct.Construct({{Samples.Make}});

                                  if (Use.Flag)
                                  {
                                      made = {{Samples.Foreign}};
                                  }

                                  return Transaction.Run(f: () => made);
                                  """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> readInALambdaAfterOneWrite =
            await AnalyzeBody(
                    statements: $"""
                                 Constructed<Thing> made = {Samples.Foreign};

                                 return Transaction.Run(f: () => made);
                                 """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(finallyWrite).IsEmpty();
        await Assert.That(refAlias).IsEmpty();
        await Assert.That(readInALambdaAfterTwoWrites).IsEmpty();

        await Assert.That(Ids(found: readInALambdaAfterOneWrite))
            .IsEquivalentTo(expected: ["MC1006", "MC1004"], ordering: CollectionOrdering.Matching);
    }

    // A deconstruction writes each variable in its tuple. Thus, the declaration does not get to the return. A
    // write through a ref local can occur out of the order of the graph. That is also true when a ref
    // reassignment or a ref conditional makes the ref local. In the last sample, the writes go to other
    // variables and to an element of an array. Thus, the declaration gets to the return.
    [Test]
    public async Task EachKindOfWriteReplacesTheValueOrMakesItUnknown()
    {
        IReadOnlyList<Diagnostic> deconstruction =
            await AnalyzeBody(
                    statements: $"""
                                 Constructed<Thing> made = {Samples.Foreign};
                                 int number;
                                 (made, number) = (construct.Construct({Samples.Make}), 1);

                                 return made;
                                 """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> refReassignment =
            await AnalyzeBody(
                    statements: $"""
                                 Constructed<Thing> made = {Samples.Foreign};
                                 Constructed<Thing> spare = {Samples.Foreign};
                                 ref Constructed<Thing> alias = ref spare;
                                 alias = ref made;
                                 alias = construct.Construct({Samples.Make});

                                 return made;
                                 """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> refConditional =
            await AnalyzeBody(
                    statements: $"""
                                 Constructed<Thing> made = {Samples.Foreign};
                                 Constructed<Thing> spare = {Samples.Foreign};
                                 ref Constructed<Thing> either = ref Use.Flag ? ref made : ref spare;
                                 either = construct.Construct({Samples.Make});

                                 return made;
                                 """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> otherWrites =
            await AnalyzeBody(
                    statements: $$"""
                                  Constructed<Thing> made = {{Samples.Foreign}};
                                  int count = 0;
                                  count += 1;
                                  string? text = null;
                                  text ??= "a";
                                  int[] items = [count];
                                  Touch(value: ref items[0]);
                                  Take(value: out int fresh);

                                  if ((items, fresh) is ([_, ..] all, _))
                                  {
                                      _ = all;
                                  }

                                  return made;

                                  void Touch(ref int value) => value++;

                                  void Take(out int value) => value = 1;
                                  """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(deconstruction).IsEmpty();
        await Assert.That(refReassignment).IsEmpty();
        await Assert.That(refConditional).IsEmpty();

        await Assert.That(Ids(found: otherWrites))
            .IsEquivalentTo(expected: ["MC1006", "MC1004"], ordering: CollectionOrdering.Matching);
    }

    // Two variables that read each other in a loop end the search at a read that it found before. A write in a
    // try block can get to a catch block with a filter and to the code after the try statement. Thus, each
    // return can give the result of a different handle. A catch after a filter runs only when the filter gives
    // true. Thus, a write in the filter replaces the earlier value for the catch.
    [Test]
    public async Task ALoopOfTwoVariablesAndACatchWithAFilterAreFollowed()
    {
        IReadOnlyList<Diagnostic> loop =
            await AnalyzeBody(
                    statements: $$"""
                                  Constructed<Thing> made = {{Samples.Foreign}};

                                  while (Use.Flag)
                                  {
                                      Constructed<Thing> other = made;
                                      made = other;
                                  }

                                  return made;
                                  """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> filter =
            await AnalyzeBody(
                    statements: $$"""
                                  Constructed<Thing> made = construct.Construct({{Samples.Make}});

                                  try
                                  {
                                      made = {{Samples.Foreign}};
                                  }
                                  catch (InvalidOperationException) when (Use.Flag)
                                  {
                                      return made;
                                  }

                                  return made;
                                  """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> filterThatWrites =
            await AnalyzeBody(
                    statements: $$"""
                                  Constructed<Thing> made = {{Samples.Foreign}};

                                  try
                                  {
                                      Use.Flag = !Use.Flag;
                                  }
                                  catch (InvalidOperationException)
                                      when ((made = construct.Construct({{Samples.Make}})) is not null)
                                  {
                                      return made;
                                  }

                                  return construct.Construct({{Samples.Make}});
                                  """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Ids(found: loop))
            .IsEquivalentTo(expected: ["MC1006", "MC1004"], ordering: CollectionOrdering.Matching);

        await Assert.That(Covered(found: loop))
            .IsEquivalentTo(expected: ["construct", "made"], ordering: CollectionOrdering.Matching);

        await Assert.That(Ids(found: filter))
            .IsEquivalentTo(expected: ["MC1004", "MC1004"], ordering: CollectionOrdering.Matching);

        await Assert.That(filterThatWrites).IsEmpty();
    }

    // A local function has a graph of its own. A read of the handle in it is the handle. A write in it can occur
    // at any time, thus the variable gives no value. A write that cannot run does not get to a read. A
    // conditional access gives the selected handle or a null that only the graph has. The analyzer does not
    // follow a conditional access to the handle. Thus, the selected variable is not the handle, and the
    // handle goes to code that the analyzer cannot follow. A foreach gives its variable the current element,
    // which only the graph has. Thus, a variable that can get that element is not the handle. The handle in
    // its declaration then goes to code that the analyzer cannot follow.
    [Test]
    public async Task ALocalFunctionAWriteThatCannotRunAndAValueOfTheGraphAreRead()
    {
        IReadOnlyList<Diagnostic> readInALocalFunction =
            await AnalyzeBody(
                    statements: $$"""
                                  return Make();

                                  Constructed<Thing> Make()
                                  {
                                      Constructor<Values> next = construct;

                                      return next.Construct({{Samples.Make}});
                                  }
                                  """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> writtenInALocalFunction =
            await AnalyzeBody(
                    statements: $"""
                                 Constructed<Thing> made = construct.Construct({Samples.Make});
                                 Reset();

                                 return made;

                                 void Reset() => made = {Samples.Foreign};
                                 """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> writeThatCannotRun =
            await AnalyzeBody(
                    statements: $$"""
                                  Constructed<Thing> made = construct.Construct({{Samples.Make}});

                                  while (true)
                                  {
                                      return made;
                                      made = {{Samples.Foreign}};
                                  }
                                  """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> conditionalAccess =
            await AnalyzeBody(
                    statements: $"""
                                 Constructor<Values>? maybe = construct;
                                 Constructor<Values>? selected = maybe?.Select(selector: static values => values);

                                 return selected!.Construct({Samples.Make});
                                 """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> elementOfAForeach =
            await AnalyzeBody(
                    statements: $$"""
                                  Constructor<Values> next = construct;
                                  Constructor<Values>[] handles = [construct];

                                  foreach (Constructor<Values> item in handles)
                                  {
                                      next = item;
                                  }

                                  return next.Construct({{Samples.Make}});
                                  """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(readInALocalFunction).IsEmpty();
        await Assert.That(writtenInALocalFunction).IsEmpty();
        await Assert.That(writeThatCannotRun).IsEmpty();

        await Assert.That(Ids(found: conditionalAccess))
            .IsEquivalentTo(expected: ["MC1007"], ordering: CollectionOrdering.Matching);

        await Assert.That(Covered(found: conditionalAccess))
            .IsEquivalentTo(expected: ["maybe"], ordering: CollectionOrdering.Matching);

        await Assert.That(Ids(found: elementOfAForeach))
            .IsEquivalentTo(expected: ["MC1007", "MC1007"], ordering: CollectionOrdering.Matching);
    }

    // A local variable is the handle at a read only where each value that can get to the read is the
    // handle. Thus, a write that replaces the handle with a different handle ends the rules for the handle.
    // A write that is not a declaration can start them. A write to the variable and an out argument do
    // not use the handle, thus MC1007 does not report them. Before, the declaration alone made a variable
    // the handle, at each read.
    [Test]
    public async Task AVariableIsTheHandleOnlyWhereEachValueIsTheHandle()
    {
        IReadOnlyList<Diagnostic> replacedThenValue =
            await AnalyzeBody(
                    statements: $"""
                                 Constructor<Values> next = construct;
                                 next = Constructor.From(values: new Values());
                                 _ = next.Construct({Samples.Make}).Value;

                                 return construct.Construct({Samples.Make});
                                 """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> replacedThenDiscarded =
            await AnalyzeBody(
                    statements: $"""
                                 Constructor<Values> next = construct;
                                 next = Constructor.From(values: new Values());
                                 next.Construct({Samples.Make});

                                 return construct.Construct({Samples.Make});
                                 """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> replacedByAnOutArgument =
            await AnalyzeBody(
                    statements: $"""
                                 Constructor<Values> next = construct;
                                 Swap(handle: out next);
                                 _ = next.Construct({Samples.Make}).Value;

                                 return construct.Construct({Samples.Make});

                                 static void Swap(out Constructor<Values> handle) => handle = Constructor.From(values: new Values());
                                 """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> replacedOnOnePath =
            await AnalyzeBody(
                    statements: $$"""
                                  Constructor<Values> next = construct;

                                  if (Use.Flag)
                                  {
                                      next = Constructor.From(values: new Values());
                                  }

                                  _ = next.Construct({{Samples.Make}}).Value;

                                  return construct.Construct({{Samples.Make}});
                                  """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> assignedThenValue =
            await AnalyzeBody(
                    statements: $"""
                                 Constructor<Values> next;
                                 next = construct;
                                 _ = next.Construct({Samples.Make}).Value;

                                 return construct.Construct({Samples.Make});
                                 """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> selectedInALoop =
            await AnalyzeBody(
                    statements: $$"""
                                  Constructor<Values> next = construct;

                                  for (int step = 0; step < 3; step++)
                                  {
                                      next = next.Select(selector: static values => values);
                                  }

                                  _ = next.Construct({{Samples.Make}}).Value;

                                  return construct.Construct({{Samples.Make}});
                                  """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(replacedThenValue).IsEmpty();
        await Assert.That(replacedThenDiscarded).IsEmpty();
        await Assert.That(replacedByAnOutArgument).IsEmpty();

        await Assert.That(Ids(found: replacedOnOnePath))
            .IsEquivalentTo(expected: ["MC1007"], ordering: CollectionOrdering.Matching);

        await Assert.That(Covered(found: replacedOnOnePath))
            .IsEquivalentTo(expected: ["construct"], ordering: CollectionOrdering.Matching);

        await Assert.That(Ids(found: assignedThenValue))
            .IsEquivalentTo(expected: ["MC1001"], ordering: CollectionOrdering.Matching);

        await Assert.That(Ids(found: selectedInALoop))
            .IsEquivalentTo(expected: ["MC1001"], ordering: CollectionOrdering.Matching);
    }

    // MC1004 also follows a local variable that holds a handle, or a lambda that a returned call runs. In
    // the first sample, the handle in the variable is from Constructor.From on one path. Thus, the read of
    // the variable is not the handle, and the write of the handle into it is MC1007. In the second, a
    // subsequent write replaces the handle from Constructor.From. The read is the handle, thus no rule
    // reports. In the third sample, a subsequent write replaces the lambda that returns the result of the
    // handle. The first lambda holds the handle and is not an argument, which is MC1007.
    [Test]
    public async Task AHandleAndALambdaInAVariableAreFollowedToEachValue()
    {
        IReadOnlyList<Diagnostic> handleOnOnePath =
            await AnalyzeBody(
                    statements: $$"""
                                  Constructor<Values> next = Constructor.From(values: new Values());

                                  if (Use.Flag)
                                  {
                                      next = construct;
                                  }

                                  return next.Construct({{Samples.Make}});
                                  """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> handleReplaced =
            await AnalyzeBody(
                    statements: $"""
                                 Constructor<Values> next = Constructor.From(values: new Values());
                                 next = construct;

                                 return next.Construct({Samples.Make});
                                 """)
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> lambdaReplaced =
            await AnalyzeBody(
                    statements: $"""
                                 Func<Constructed<Thing>> run = () => construct.Construct({Samples.Make});
                                 run = () => {Samples.Foreign};

                                 return Transaction.Run(f: run);
                                 """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Ids(found: handleOnOnePath))
            .IsEquivalentTo(expected: ["MC1007", "MC1004"], ordering: CollectionOrdering.Matching);

        await Assert.That(handleReplaced).IsEmpty();

        await Assert.That(Ids(found: lambdaReplaced))
            .IsEquivalentTo(expected: ["MC1007", "MC1004"], ordering: CollectionOrdering.Matching);

        await Assert.That(Covered(found: lambdaReplaced))
            .IsEquivalentTo(expected: ["construct", Samples.Foreign], ordering: CollectionOrdering.Matching);
    }

    // A lambda in an inner continuation is also in the outer continuation. Before, the two continuations
    // took its return as their result. Thus, each value had two reports, and one of them named the handle
    // of the outer continuation.
    [Test]
    public async Task AValueInAnInnerContinuationIsReportedOnceForItsOwnHandle()
    {
        IReadOnlyList<Diagnostic> nullReturned =
            await Analyze(source: Samples.OfStaged(continuation: InnerContinuation(otherPath: "null!")))
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> foreignReturned =
            await Analyze(source: Samples.OfStaged(continuation: InnerContinuation(otherPath: Samples.Foreign)))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Ids(found: nullReturned))
            .IsEquivalentTo(expected: ["MC1003"], ordering: CollectionOrdering.Matching);

        await Assert.That(nullReturned[0].GetMessage())
            .IsEqualTo(expected: "The continuation returns null: return the result of its handle construct");

        await Assert.That(Ids(found: foreignReturned))
            .IsEquivalentTo(expected: ["MC1004"], ordering: CollectionOrdering.Matching);

        await Assert.That(foreignReturned[0].GetMessage())
            .IsEqualTo(
                expected:
                "The continuation returns the result of a handle from Constructor.From, and not the result of its "
                + "handle construct");
    }

    // Each lambda here returns a Constructed, but its value does not get to the result. Two are in
    // variables, and one goes to a call whose value the continuation discards. Before, the analyzer
    // examined the return of each lambda in the body that returns a Constructed.
    [Test]
    public async Task ALambdaWhoseValueIsNotTheResultIsNotExamined()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    source: Samples.OfBase(
                        continuation: $$"""
                                        (output, construct) =>
                                        {
                                            Func<Constructed<Thing>> later = () => null!;
                                            Func<Constructed<Thing>> other = () => {{Samples.Foreign}};
                                            _ = Helpers.Now(f: () => {{Samples.Foreign}});

                                            return construct.Construct({{Samples.Make}});
                                        }
                                        """))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found).IsEmpty();
    }

    // A call that the continuation returns can give the value of a lambda as the result. This is also
    // true when a local variable holds the lambda or the value of the call. A lambda in a variable also
    // gets MC1007, because the analyzer does not follow the handle into it.
    [Test]
    public async Task ALambdaThatAReturnedCallRunsIsExaminedThroughALocalVariable()
    {
        IReadOnlyList<Diagnostic> lambdaInAVariable =
            await Analyze(
                    source: Samples.OfBase(
                        continuation: $$"""
                                        (output, construct) =>
                                        {
                                            Func<Constructed<Thing>> run =
                                                () => Use.Flag ? construct.Construct({{Samples.Make}}) : {{Samples.Foreign}};

                                            return Transaction.Run(f: run);
                                        }
                                        """))
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> valueInAVariable =
            await Analyze(
                    source: Samples.OfBase(
                        continuation: $$"""
                                        (output, construct) =>
                                        {
                                            Constructed<Thing> made =
                                                Transaction.Run(f: () => Use.Flag ? construct.Construct({{Samples.Make}}) : null!);

                                            return made;
                                        }
                                        """))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Ids(found: lambdaInAVariable))
            .IsEquivalentTo(expected: ["MC1007", "MC1004"], ordering: CollectionOrdering.Matching);

        await Assert.That(Ids(found: valueInAVariable))
            .IsEquivalentTo(expected: ["MC1003"], ordering: CollectionOrdering.Matching);
    }

    // A helper method makes the Constructed here. The analyzer cannot prove where it comes from, thus
    // MC1004 does not report it. The continuation does not use its handle, which is MC1006.
    [Test]
    public async Task AResultFromAHelperIsNotReportedAsTheResultOfADifferentHandle()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    source: Samples.OfBase(
                        continuation: "(output, construct) => Done.Of(value: new Thing(values: new Values()))"))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Ids(found: found))
            .IsEquivalentTo(expected: ["MC1006"], ordering: CollectionOrdering.Matching);
    }

    // MC1005. The function for Construct with a discard, and with a parameter that it does not read.
    [Test]
    public async Task AFunctionThatDoesNotReadTheValuesOfTheBaseIsReported()
    {
        IReadOnlyList<Diagnostic> discard =
            await Analyze(
                    source: Samples.OfBase(
                        continuation:
                        "(output, construct) => construct.Construct(constructor: static _ => new Thing(values: new Values()))"))
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> named =
            await Analyze(
                    source: Samples.OfBase(
                        continuation:
                        "(output, construct) => construct.Construct(constructor: static unused => new Thing(values: new Values()))"))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Ids(found: discard))
            .IsEquivalentTo(expected: ["MC1005"], ordering: CollectionOrdering.Matching);

        await Assert.That(Covered(found: discard))
            .IsEquivalentTo(expected: ["_"], ordering: CollectionOrdering.Matching);

        await Assert.That(Covered(found: named))
            .IsEquivalentTo(expected: ["unused"], ordering: CollectionOrdering.Matching);

        await Assert.That(named[0].GetMessage())
            .IsEqualTo(
                expected:
                "The function given to Construct does not read its parameter unused, so its result does not hold the "
                + "values of the base");
    }

    // MC1006. The handle as a discard, and the handle with a name that the continuation does not read.
    // The second sample also returns the result of Constructor.From, which is MC1004.
    [Test]
    public async Task AHandleThatIsNotUsedIsReported()
    {
        IReadOnlyList<Diagnostic> discard =
            await Analyze(
                    source: Samples.OfBase(
                        continuation: "(output, _) => Done.Of(value: new Thing(values: new Values()))"))
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> named =
            await Analyze(source: Samples.OfBase(continuation: $"(output, construct) => {Samples.Foreign}"))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Ids(found: discard))
            .IsEquivalentTo(expected: ["MC1006"], ordering: CollectionOrdering.Matching);

        await Assert.That(Covered(found: discard))
            .IsEquivalentTo(expected: ["_"], ordering: CollectionOrdering.Matching);

        await Assert.That(discard[0].GetMessage())
            .IsEqualTo(
                expected:
                "The continuation returns a value and does not use its handle _: call Construct or Advance on the "
                + "handle and return the result");

        await Assert.That(Ids(found: named))
            .IsEquivalentTo(expected: ["MC1006", "MC1004"], ordering: CollectionOrdering.Matching);

        await Assert.That(Covered(found: named)[0]).IsEqualTo(expected: "construct");
    }

    // MC1007. The handle given to a helper method, to a type that is not a stage or a handle, and to a
    // field. Each is correct code here, and the rule says only that the analyzer stops.
    [Test]
    public async Task AHandleThatGoesToCodeTheAnalyzerDoesNotReadIsReported()
    {
        IReadOnlyList<Diagnostic> method =
            await Analyze(
                    source: Samples.OfBase(continuation: "(output, construct) => Helpers.Finish(handle: construct)"))
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> type =
            await Analyze(
                    source: Samples.OfBase(
                        continuation: $$"""
                                        (output, construct) =>
                                        {
                                            Holder holder = new(handle: construct);

                                            return holder.Handle.Construct({{Samples.Make}});
                                        }
                                        """))
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> field =
            await Analyze(
                    source: Samples.OfBase(
                        continuation: $$"""
                                        (output, construct) =>
                                        {
                                            Helpers.Kept = construct;

                                            return Helpers.Kept.Construct({{Samples.Make}});
                                        }
                                        """))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Ids(found: method))
            .IsEquivalentTo(expected: ["MC1007"], ordering: CollectionOrdering.Matching);

        await Assert.That(Covered(found: method))
            .IsEquivalentTo(expected: ["construct"], ordering: CollectionOrdering.Matching);

        await Assert.That(method[0].Severity).IsEqualTo(expected: DiagnosticSeverity.Info);

        await Assert.That(method[0].GetMessage())
            .IsEqualTo(expected: "The handle construct is given to Finish, and the analyzer cannot follow it there");

        await Assert.That(Ids(found: type)).IsEquivalentTo(expected: ["MC1007"], ordering: CollectionOrdering.Matching);
        await Assert.That(type[0].GetMessage()).Contains(expected: "is given to the constructor of Holder");

        await Assert.That(Ids(found: field))
            .IsEquivalentTo(expected: ["MC1007"], ordering: CollectionOrdering.Matching);

        await Assert.That(field[0].GetMessage()).Contains(expected: "is stored in Helpers.Kept");
    }

    // MC1007. A lambda holds the handle, and nothing says that its receiver calls it before the
    // receiver returns. The receiver is a method without InstantHandle, a function of a cell, and a
    // variable.
    [Test]
    public async Task AHandleInALambdaThatCanRunLaterIsReported()
    {
        IReadOnlyList<Diagnostic> method =
            await Analyze(
                    source: Samples.OfBase(
                        continuation:
                        $"(output, construct) => Helpers.Later(f: () => construct.Construct({Samples.Make}))"))
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> cell =
            await Analyze(
                    source: Samples.OfBase(
                        continuation: $$"""
                                        (output, construct) =>
                                        {
                                            Constructed<Thing>? made = null;

                                            _ = Cell.Constant(1)
                                                .Map(
                                                    value =>
                                                    {
                                                        made ??= construct.Construct({{Samples.Make}});

                                                        return value;
                                                    });

                                            return made ?? throw new InvalidOperationException();
                                        }
                                        """))
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> variable =
            await Analyze(
                    source: Samples.OfBase(
                        continuation: $$"""
                                        (output, construct) =>
                                        {
                                            Func<Constructed<Thing>> later = () => construct.Construct({{Samples.Make}});

                                            return later();
                                        }
                                        """))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Ids(found: method))
            .IsEquivalentTo(expected: ["MC1007"], ordering: CollectionOrdering.Matching);

        await Assert.That(method[0].GetMessage())
            .IsEqualTo(
                expected: "The handle construct is in a lambda that is given to Later, which can call it after the "
                          + "continuation returns, and the analyzer cannot follow it there");

        await Assert.That(Ids(found: cell)).IsEquivalentTo(expected: ["MC1007"], ordering: CollectionOrdering.Matching);
        await Assert.That(cell[0].GetMessage()).Contains(expected: "is in a lambda that is given to Map");

        await Assert.That(Ids(found: variable))
            .IsEquivalentTo(expected: ["MC1007"], ordering: CollectionOrdering.Matching);

        await Assert.That(variable[0].GetMessage()).Contains(expected: "is in a lambda that is not an argument");
    }

    // MC1007. A local function runs where the code uses it. A call by its name runs it there, and a
    // delegate made from it runs where the delegate goes, as a lambda does.
    [Test]
    public async Task AHandleInALocalFunctionIsFollowedToEachUseOfTheFunction()
    {
        const string make = $"Constructed<Thing> Make() => construct.Construct({Samples.Make});";

        IReadOnlyList<Diagnostic> later =
            await AnalyzeBody(statements: $"{make}\nreturn Helpers.Later(f: Make);")
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> variable =
            await AnalyzeBody(statements: $"{make}\nFunc<Constructed<Thing>> kept = Make;\nreturn kept();")
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> inALambda =
            await AnalyzeBody(statements: $"{make}\nreturn Helpers.Later(f: () => Make());")
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> byName =
            await AnalyzeBody(statements: $"{make}\nreturn Make();")
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> now =
            await AnalyzeBody(statements: $"{make}\nreturn Helpers.Now(f: Make);")
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> recursive =
            await AnalyzeBody(
                    statements: $"""
                                 Constructed<Thing> Make(int count) =>
                                     count > 0 ? Make(count: count - 1) : construct.Construct({Samples.Make});

                                 return Make(count: 2);
                                 """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Ids(found: later))
            .IsEquivalentTo(expected: ["MC1007"], ordering: CollectionOrdering.Matching);

        await Assert.That(Covered(found: later))
            .IsEquivalentTo(expected: ["construct"], ordering: CollectionOrdering.Matching);

        await Assert.That(later[0].GetMessage())
            .IsEqualTo(
                expected: "The handle construct is in the local function Make, which Later gets and can call after "
                          + "the continuation returns, and the analyzer cannot follow it there");

        await Assert.That(Ids(found: variable))
            .IsEquivalentTo(expected: ["MC1007"], ordering: CollectionOrdering.Matching);

        await Assert.That(variable[0].GetMessage())
            .Contains(expected: "is in the local function Make, which is used as a value that is not an argument");

        await Assert.That(Ids(found: inALambda))
            .IsEquivalentTo(expected: ["MC1007"], ordering: CollectionOrdering.Matching);

        await Assert.That(inALambda[0].GetMessage()).Contains(expected: "is in a lambda that is given to Later");
        await Assert.That(byName).IsEmpty();
        await Assert.That(now).IsEmpty();
        await Assert.That(recursive).IsEmpty();
    }

    // MC1007 and MC1004. A pattern at the top of a test that declares a variable stores the value that it
    // tests in the variable, as a declaration does. A pattern that declares no variable only tests the value.
    [Test]
    public async Task APatternThatDeclaresAVariableStoresTheValueThatItTests()
    {
        const string fail = "throw new InvalidOperationException();";

        IReadOnlyList<Diagnostic> isVar =
            await AnalyzeBody(statements: $"if (construct is var h)\n{{\nreturn Helpers.Finish(handle: h);\n}}\n{fail}")
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> property =
            await AnalyzeBody(
                    statements: $"if (construct is {{ }} h)\n{{\nreturn Helpers.Finish(handle: h);\n}}\n{fail}")
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> both =
            await AnalyzeBody(
                    statements:
                    $"if (construct is var h and not null)\n{{\nreturn Helpers.Finish(handle: h);\n}}\n{fail}")
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> switchExpression =
            await AnalyzeBody(statements: "return construct switch { var h => Helpers.Finish(handle: h) };")
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> switchStatement =
            await AnalyzeBody(
                    statements: $"switch (construct)\n{{\ncase var h:\nreturn Helpers.Finish(handle: h);\n}}\n{fail}")
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> kept =
            await AnalyzeBody(
                    statements:
                    $"if (construct is var h)\n{{\nreturn h.Construct({Samples.Make});\n}}\n{fail}")
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> keptBySwitch =
            await AnalyzeBody(statements: $"return construct switch {{ var h => h.Construct({Samples.Make}) }};")
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> tested =
            await AnalyzeBody(
                    statements:
                    $"if (construct is null)\n{{\n{fail}\n}}\nreturn construct.Construct({Samples.Make});")
                .ConfigureAwait(continueOnCapturedContext: false);

        IReadOnlyList<Diagnostic> foreign =
            await AnalyzeBody(
                    statements: $$"""
                                  if (Constructor.From(values: new Thing(values: new Values())) is var other)
                                  {
                                      return other.Construct(constructor: static thing => thing);
                                  }

                                  return construct.Construct({{Samples.Make}});
                                  """)
                .ConfigureAwait(continueOnCapturedContext: false);

        foreach (IReadOnlyList<Diagnostic> found in new[] { isVar, property, both, switchExpression, switchStatement })
        {
            await Assert.That(Ids(found: found))
                .IsEquivalentTo(expected: ["MC1007"], ordering: CollectionOrdering.Matching);

            await Assert.That(Covered(found: found))
                .IsEquivalentTo(expected: ["h"], ordering: CollectionOrdering.Matching);

            await Assert.That(found[0].GetMessage())
                .IsEqualTo(
                    expected: "The handle construct is given to Finish, and the analyzer cannot follow it there");
        }

        await Assert.That(kept).IsEmpty();
        await Assert.That(keptBySwitch).IsEmpty();
        await Assert.That(tested).IsEmpty();

        await Assert.That(Ids(found: foreign))
            .IsEquivalentTo(expected: ["MC1004"], ordering: CollectionOrdering.Matching);
    }

    // The attribute is conditional, and a library that does not define its symbol does not have it in
    // its assembly. The analyzer then cannot read it, and it reports the lambda. This is why it knows
    // the members of SodaFlow by name.
    [Test]
    public async Task InstantHandleInACompiledLibraryIsNotRead()
    {
        IReadOnlyList<Diagnostic> found =
            await AnalyzerHarness.Analyze(
                    analyzer: new ContinuationAnalyzer(),
                    source: Samples.OfBase(
                        continuation:
                        $"(output, construct) => Library.Now(f: () => construct.Construct({Samples.Make}))"),
                    library: """
                             using System;
                             using JetBrains.Annotations;

                             public static class Library
                             {
                                 public static T Now<T>([InstantHandle] Func<T> f) => f();
                             }
                             """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Ids(found: found))
            .IsEquivalentTo(expected: ["MC1007"], ordering: CollectionOrdering.Matching);

        await Assert.That(found[0].GetMessage()).Contains(expected: "is in a lambda that is given to Now");
    }

    // Six rules are warnings. The seventh reports where the analyzer stops, and it is information.
    [Test]
    public async Task TheRulesHaveTheirSeverities()
    {
        Dictionary<string, DiagnosticSeverity> severities =
            new ContinuationAnalyzer().SupportedDiagnostics.ToDictionary(
                keySelector: static rule => rule.Id,
                elementSelector: static rule => rule.DefaultSeverity);

        await Assert.That(severities.Keys.OrderBy(keySelector: static id => id))
            .IsEquivalentTo(
                expected: ["MC1001", "MC1002", "MC1003", "MC1004", "MC1005", "MC1006", "MC1007"],
                ordering: CollectionOrdering.Matching);

        await Assert.That(
                severities.Where(predicate: static pair => pair.Value == DiagnosticSeverity.Info)
                    .Select(selector: static pair => pair.Key))
            .IsEquivalentTo(expected: ["MC1007"], ordering: CollectionOrdering.Matching);

        await Assert.That(severities.Values.Count(predicate: static severity => severity == DiagnosticSeverity.Warning))
            .IsEqualTo(expected: 6);
    }

    // Each sample has a local variable that reads its own value in its declaration. That does not compile,
    // but an editor analyzes the code while the user types it. The search for the source of the handle must
    // stop. The time limit makes the test fail if the search does not stop. The continuation does not use
    // its handle, which is MC1006.
    [Test]
    [Timeout(timeoutInMilliseconds: 60_000)]
    [Arguments("Constructor<Values> other = other.Select(selector: static values => values);")]
    [Arguments("Constructor<Values> other = other;")]
    public async Task ALocalVariableThatReadsItselfEndsTheAnalysis(
        string declaration,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Diagnostic> found =
            await AnalyzerHarness.Analyze(
                    analyzer: new ContinuationAnalyzer(),
                    source: Samples.OfBase(
                        continuation:
                        $"(output, construct) => {{ {declaration} return other.Construct({Samples.Make}); }}"),
                    errors: ["CS0165"],
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Ids(found: found))
            .IsEquivalentTo(expected: ["MC1006"], ordering: CollectionOrdering.Matching);
    }

    // A stage whose continuation advances it. Through the function of Transaction.Run, the continuation of
    // the stage returns the result of its own handle on one path. On the other path, it returns the given
    // value.
    private static string InnerContinuation(string otherPath) =>
        $"""
         (output, stage) =>
             stage.Advance(
                 input: 1,
                 continuation: static (second, construct) =>
                     Transaction.Run(f: () => second.Length > 0 ? construct.Construct({Samples.Make}) : {otherPath}))
         """;

    private static Task<IReadOnlyList<Diagnostic>> Analyze(string source) =>
        AnalyzerHarness.Analyze(analyzer: new ContinuationAnalyzer(), source: source);

    // A continuation of a base whose body is the statements.
    private static Task<IReadOnlyList<Diagnostic>> AnalyzeBody(string statements) =>
        Analyze(source: Samples.OfBase(continuation: $"(output, construct) =>\n{{\n{statements}\n}}"));

    private static List<string> Ids(IEnumerable<Diagnostic> found) =>
    [
        .. found.Select(selector: static diagnostic => diagnostic.Id)
    ];

    // The text that each report covers, in the order of the source.
    private static List<string> Covered(IEnumerable<Diagnostic> found) =>
    [
        .. found.Select(
            selector: static diagnostic =>
                diagnostic.Location.SourceTree?.GetText().ToString(span: diagnostic.Location.SourceSpan)
                ?? string.Empty)
    ];
}
