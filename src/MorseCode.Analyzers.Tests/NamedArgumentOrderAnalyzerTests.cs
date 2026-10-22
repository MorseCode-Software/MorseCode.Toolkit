using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Analyzers.Tests;

/// <summary>
///     Compiles samples of code with <see cref="NamedArgumentOrderAnalyzer" />, and
///     reads what it reports.
/// </summary>
internal sealed class NamedArgumentOrderAnalyzerTests
{
    // An interface that a different assembly declares, as a package does. The compiler reads its optional
    // parameter from metadata, which is a different path from the declarations below.
    private const string Library =
        """
        using System.Collections.Generic;

        public interface IFactory
        {
            List<T> Make<T>(T first, IEqualityComparer<T>? comparer = null);
        }
        """;

    // The declarations that the samples call. Each sample is the body of Calls.Run, after them. Each sample
    // also gives the arguments of the three calls of constructors that are not in Run: two of Pair, and one of Chain.
    private const string Declarations =
        """
        using System;
        using System.Collections.Generic;

        public class Pair
        {
            public Pair(int a, int b) { }
        }

        public class Derived : Pair
        {
            public Derived() : base(ARGUMENTS) { }
        }

        public class Primary(int value) : Pair(ARGUMENTS)
        {
            public int Value => value;
        }

        public class Chain
        {
            public Chain(int a, int b) { }

            public Chain() : this(ARGUMENTS) { }
        }

        public static class Extensions
        {
            public static int Add(this int value, int a, int b) => value + a + b;
        }

        public static class Calls
        {
            public static int Three(int a, int b, int c) => a + b + c;

            public static int Optional(int a, IEqualityComparer<int>? comparer = null) => a;

            public static int Sum(int start, params int[] rest) => start;

            public static int Tail(int a, int b, params int[] rest) => a + b;

            public static int Count<T>(List<T> items) => items.Count;

            public static IFactory? Factory { get; set; }

            public static void Run()
            {
        """;

    // Each one of these calls has its arguments in the order of the parameters, or has no names.
    [Test]
    public async Task ArgumentsInTheOrderOfTheParametersGiveNoReport()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    body: """
                          Three(a: 1, b: 2, c: 3);
                          Three(1, b: 2, c: 3);
                          Three(3, 2, 1);
                          _ = new Pair(a: 1, b: 2);
                          Pair pair = new(a: 1, b: 2);
                          _ = 1.Add(a: 2, b: 3);
                          int Local(int x, int y) => x + y;
                          Local(x: 1, y: 2);
                          _ = Sum(start: 1, rest: 5);
                          _ = Tail(a: 1, b: 2, 5, 6);
                          """,
                    baseArguments: "a: 1, b: 2")
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found).IsEmpty();
    }

    // Each kind of call that the analyzer examines, with its arguments out of order. Each one gives one
    // report, on its own line. The first three are the calls of constructors in the declarations.
    [Test]
    public async Task ArgumentsOutOfTheOrderOfTheParametersGiveOneReportForEachCall()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    body: """
                          Three(c: 3, a: 1, b: 2);
                          Three(1, c: 3, b: 2);
                          _ = new Pair(b: 2, a: 1);
                          Pair pair = new(b: 2, a: 1);
                          _ = 1.Add(b: 3, a: 2);
                          int Local(int x, int y) => x + y;
                          Local(y: 2, x: 1);
                          Func<int, int, int> add = (x, y) => x + y;
                          add(arg2: 2, arg1: 1);
                          _ = Sum(rest: 5, start: 1);
                          """,
                    baseArguments: "b: 2, a: 1")
                .ConfigureAwait(continueOnCapturedContext: false);

        int run = Declarations.Split(separator: '\n').Length;

        await Assert.That(
                found.Select(selector: static diagnostic => diagnostic.Location.GetLineSpan().StartLinePosition.Line))
            .IsEquivalentTo(
                expected: [10, 13, 22, run, run + 1, run + 2, run + 3, run + 4, run + 6, run + 8, run + 9],
                ordering: CollectionOrdering.Matching);

        await Assert.That(found.Select(selector: static diagnostic => diagnostic.Id).Distinct())
            .IsEquivalentTo(expected: ["MC0001"], ordering: CollectionOrdering.Matching);
    }

    // The message names the method and the order of its parameters, thus the user can correct the call.
    [Test]
    public async Task TheReportNamesTheMethodAndTheOrderOfItsParameters()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(body: "Three(c: 3, a: 1, b: 2);", baseArguments: "a: 1, b: 2")
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found.Select(selector: static diagnostic => diagnostic.GetMessage()))
            .IsEquivalentTo(
                expected:
                [
                    "The arguments of Calls.Three(int, int, int) are not in the order of its parameters: a, b, c"
                ],
                ordering: CollectionOrdering.Matching);
    }

    // The compiler adds the value of an optional parameter that a call leaves out, and an empty params array.
    // When the call is itself an argument, Roslyn gives such an added argument the syntax of the outer
    // argument. That syntax comes before the arguments of the call. The compiler also makes a call for an
    // interpolated string that goes to a handler, and gives it the provider in a different order. The analyzer
    // must not count those, because the source does not have them. The factory comes from metadata, as a
    // package does.
    [Test]
    public async Task OptionalParametersThatACallLeavesOutAndCallsOfTheCompilerGiveNoReport()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    body: """
                          Optional(a: 1);
                          _ = Count(items: Factory!.Make(first: 1));
                          _ = Three(a: Sum(start: 1), b: 2, c: 3);
                          int value = 3;
                          string text =
                              string.Create(
                                  provider: System.Globalization.CultureInfo.InvariantCulture,
                                  handler: $"{value} of {Three(a: 1, b: 2, c: 3)}");
                          """,
                    baseArguments: "a: 1, b: 2")
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found).IsEmpty();
    }

    // Compiles the declarations and the body, and gives what the analyzer reports. The sample must compile,
    // because the analyzer does not examine a call that does not bind.
    private static Task<IReadOnlyList<Diagnostic>> Analyze(string body, string baseArguments)
    {
        const string end =
            """
                }
            }
            """;

        string source =
            Declarations.Replace(oldValue: "ARGUMENTS", newValue: baseArguments)
            + "\n"
            + body
            + "\n"
            + end;

        return AnalyzerHarness.Analyze(analyzer: new NamedArgumentOrderAnalyzer(), source: source, library: Library);
    }
}
