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

            public static int Suppressed(int a, string? b, int c, int d = 0) => a + c + d;

            public static int Texts(string? first, string? second, string? third, string? fourth = null) => 0;

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

    // The report covers the argument list and not the whole call. The receiver of a call and the initializer
    // of a new are not what the rule is about, and a mark on them is noise.
    [Test]
    public async Task TheReportCoversOnlyTheArgumentList()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    body: """
                          Three(c: 3, a: 1, b: 2);
                          _ = Math.Abs(value: -1).Add(b: 3, a: 2);
                          _ = new Pair(b: 2, a: 1) { };
                          """,
                    baseArguments: "b: 2, a: 1")
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(
                found.Select(
                    selector: static diagnostic =>
                        diagnostic.Location.SourceTree?.GetText().ToString(span: diagnostic.Location.SourceSpan)
                        ?? string.Empty))
            .IsEquivalentTo(
                expected:
                [
                    "(b: 2, a: 1)", "(b: 2, a: 1)", "(b: 2, a: 1)", "(c: 3, a: 1, b: 2)", "(b: 3, a: 2)",
                    "(b: 2, a: 1)"
                ],
                ordering: CollectionOrdering.Matching);
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

    // The null-forgiving operator changes the syntax that Roslyn gives to the operation of an argument. The
    // arguments must keep their place, wherever the suppressed one is, also when it is not an expression of null.
    [Test]
    public async Task ASuppressedArgumentKeepsItsPositionWhereverItIs()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    body: """
                          string text = "x";
                          string? maybe = null;
                          _ = Suppressed(a: 1, b: null!, c: 3);
                          _ = Suppressed(a: 1, b: text!, c: 3);
                          _ = Suppressed(a: 1, b: maybe!, c: 3);
                          _ = Suppressed(a: 1, b: null!, c: Three(a: 1, b: 2, c: 3));
                          _ = Suppressed(a: 1!, b: text, c: 3!);
                          _ = Suppressed(a: 1, b: text, c: 3!);
                          _ = Texts(first: null!, second: text, third: text);
                          _ = Texts(first: text, second: null!, third: text);
                          _ = Texts(first: text, second: text, third: null!);
                          _ = Texts(first: null!, second: null!, third: null!, fourth: null!);
                          """,
                    baseArguments: "a: 1, b: 2")
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found).IsEmpty();
    }

    // A suppressed argument that is out of order is still a report, and the report is on the call.
    [Test]
    public async Task ASuppressedArgumentThatIsOutOfOrderGivesAReport()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    body: """
                          string text = "x";
                          _ = Suppressed(a: 1, c: 3, b: null!);
                          _ = Suppressed(b: null!, a: 1, c: 3);
                          _ = Suppressed(c: 3, b: text!, a: 1);
                          _ = Texts(second: text, first: null!, third: text);
                          _ = Texts(first: text, third: null!, second: text);
                          """,
                    baseArguments: "a: 1, b: 2")
                .ConfigureAwait(continueOnCapturedContext: false);

        int run = Declarations.Split(separator: '\n').Length;

        await Assert.That(
                found.Select(selector: static diagnostic => diagnostic.Location.GetLineSpan().StartLinePosition.Line))
            .IsEquivalentTo(expected: [run + 1, run + 2, run + 3, run + 4, run + 5], ordering: CollectionOrdering.Matching);
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

    // The constructor arguments of an attribute, in order and out of order. A name with an equals sign sets a
    // property, which is not a parameter, thus it does not count. A value past the last parameter goes into
    // the params array. Each report covers the argument list of the attribute.
    [Test]
    public async Task TheConstructorArgumentsOfAnAttributeAreChecked()
    {
        IReadOnlyList<Diagnostic> found =
            await AnalyzerHarness.Analyze(
                    analyzer: new NamedArgumentOrderAnalyzer(),
                    source: """
                            using System;

                            [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
                            public sealed class MarkAttribute(int a, int b) : Attribute
                            {
                                public int A { get; } = a;

                                public int B { get; } = b;

                                public int Extra { get; set; }
                            }

                            [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
                            public sealed class TagsAttribute(int first, params int[] rest) : Attribute
                            {
                                public int First { get; } = first;

                                public int[] Rest { get; } = rest;
                            }

                            [Mark(a: 1, b: 2)]
                            [Mark(1, b: 2)]
                            [Mark(a: 1, b: 2, Extra = 3)]
                            [Tags(1, 2, 3)]
                            [Mark(b: 2, a: 1)]
                            [Mark(b: 2, a: 1, Extra = 3)]
                            [Tags(rest: [2], first: 1)]
                            public sealed class Marked;
                            """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Covered(found: found))
            .IsEquivalentTo(
                expected: ["(b: 2, a: 1)", "(b: 2, a: 1, Extra = 3)", "(rest: [2], first: 1)"],
                ordering: CollectionOrdering.Matching);

        await Assert.That(found[0].GetMessage())
            .IsEqualTo(
                expected:
                "The arguments of MarkAttribute.MarkAttribute(int, int) are not in the order of its parameters: a, b");
    }

    // An attribute that a different assembly declares, as a package does. The compiler reads its constructor
    // from metadata.
    [Test]
    public async Task AnAttributeFromADifferentAssemblyIsChecked()
    {
        IReadOnlyList<Diagnostic> found =
            await AnalyzerHarness.Analyze(
                    analyzer: new NamedArgumentOrderAnalyzer(),
                    source: """
                            [Range(maximum: 9, minimum: 1)]
                            public sealed class Bounded;
                            """,
                    library: """
                             public sealed class RangeAttribute(int minimum, int maximum) : System.Attribute
                             {
                                 public int Minimum { get; } = minimum;

                                 public int Maximum { get; } = maximum;
                             }
                             """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Covered(found: found))
            .IsEquivalentTo(expected: ["(maximum: 9, minimum: 1)"], ordering: CollectionOrdering.Matching);
    }

    // An attribute without parentheses has no argument list. An empty pair of parentheses has a list with no
    // argument. Neither gives a report.
    [Test]
    public async Task AnAttributeWithoutArgumentsGivesNoReport()
    {
        IReadOnlyList<Diagnostic> found =
            await AnalyzerHarness.Analyze(
                    analyzer: new NamedArgumentOrderAnalyzer(),
                    source: """
                            using System;

                            [Serializable]
                            [Obsolete()]
                            public sealed class Plain;
                            """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found).IsEmpty();
    }

    // An editor runs the analyzer while the code has errors. An attribute that does not bind to one constructor
    // gives no report: a type that is not declared, and a name that no parameter has. An exception in the
    // analyzer gives AD0001, and the test fails on it.
    [Test]
    public async Task AnAttributeThatDoesNotBindGivesNoReport()
    {
        IReadOnlyList<Diagnostic> found =
            await AnalyzerHarness.Analyze(
                    analyzer: new NamedArgumentOrderAnalyzer(),
                    source: """
                            using System;

                            public sealed class MarkAttribute(int a, int b) : Attribute
                            {
                                public int A { get; } = a;

                                public int B { get; } = b;
                            }

                            [Missing(b: 2, a: 1)]
                            [Mark(c: 2, a: 1)]
                            public sealed class Marked;
                            """,
                    errors: ["CS0246", "CS0246", "CS1739"])
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found).IsEmpty();
    }

    // The arguments of an indexer, in order and out of order, and through a conditional access. A property
    // without parameters has no argument list. Each report covers the brackets.
    [Test]
    public async Task TheArgumentsOfAnIndexerAreChecked()
    {
        IReadOnlyList<Diagnostic> found =
            await AnalyzerHarness.Analyze(
                    analyzer: new NamedArgumentOrderAnalyzer(),
                    source: """
                            public sealed class Grid
                            {
                                public int this[int row, int column] => (row * 10) + column;

                                public int Count => 1;
                            }

                            public static class Use
                            {
                                public static int Read(Grid grid, Grid? maybe) =>
                                    grid[row: 1, column: 2]
                                    + grid[1, column: 2]
                                    + grid.Count
                                    + grid[column: 2, row: 1]
                                    + (maybe?[column: 2, row: 1] ?? 0);
                            }
                            """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Covered(found: found))
            .IsEquivalentTo(
                expected: ["[column: 2, row: 1]", "[column: 2, row: 1]"],
                ordering: CollectionOrdering.Matching);

        await Assert.That(found[0].GetMessage())
            .IsEqualTo(
                expected: "The arguments of Grid.this[int, int] are not in the order of its parameters: row, column");
    }

    // The text that each report covers, in the order of the source.
    private static List<string> Covered(IEnumerable<Diagnostic> found) =>
    [
        .. found.Select(
            selector: static diagnostic =>
                diagnostic.Location.SourceTree?.GetText().ToString(span: diagnostic.Location.SourceSpan)
                ?? string.Empty)
    ];

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
