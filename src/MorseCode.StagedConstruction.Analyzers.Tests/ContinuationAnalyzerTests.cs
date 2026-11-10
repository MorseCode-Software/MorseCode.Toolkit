using System.Collections.Generic;
using System.Linq;
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

    private static Task<IReadOnlyList<Diagnostic>> Analyze(string source) =>
        AnalyzerHarness.Analyze(analyzer: new ContinuationAnalyzer(), source: source);

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
