using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using MorseCode.Analyzers.CodeFixes;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Analyzers.Tests;

/// <summary>
///     Applies <see cref="NamedArgumentOrderCodeFixProvider" /> to what
///     <see cref="NamedArgumentOrderAnalyzer" /> reports.
/// </summary>
internal sealed class NamedArgumentOrderCodeFixTests
{
    private const string InOrder = "Put the arguments in the order of the parameters";

    private const string InOrderAndChangesEvaluation =
        "Put the arguments in the order of the parameters, which changes the order in which they run";

    private const string KeepsEvaluationKey = "MC0001";

    private const string ChangesEvaluationKey = "MC0001.ChangesEvaluationOrder";

    // The declarations that the samples call. Each sample is the body of Sample.Run, after them.
    private const string Declarations =
        """
        using System;

        public static class Calls
        {
            public static int Pair(int a, int b) => a - b;

            public static int Three(int a, int b, int c) => a + b + c;

            public static int Rest(int first, params int[] rest) => first + rest.Length;

            public static int Next() => 0;

            public static int Store(int a, out int b)
            {
                b = a;
                return a;
            }
        }

        public sealed class Grid
        {
            public int this[int row, int column] => row * column;
        }

        public class Base(int a, int b)
        {
            public int Sum { get; } = a + b;
        }

        [AttributeUsage(AttributeTargets.All)]
        public sealed class MarkAttribute(int a, int b) : Attribute
        {
            public int A { get; } = a;

            public int B { get; } = b;

            public int Extra { get; set; }
        }

        public static class Sample
        {
            public static int Field;

            public static void Run(int x, Grid grid)
            {
                int result;

        """;

    private const string End =
        """
            }
        }
        """;

    // Arguments with no effect: constants, a parameter, and a field. A positional argument keeps its position,
    // and the values of a params array keep their order. Each fix keeps the evaluation, and its title says
    // only that it puts the arguments in order.
    [Test]
    public async Task ArgumentsWithNoEffectArePutInOrder()
    {
        const string body =
            """
                    result = Calls.Pair(b: 2, a: 1);
                    result = Calls.Pair(b: x, a: Field);
                    result = Calls.Three(1, c: 3, b: 2);
                    result = Calls.Rest(rest: [2, 3], first: 1);
            """;

        IReadOnlyList<CodeFixHarness.Offer> offers =
            await Offers(body: body).ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(offers.Select(selector: static offer => offer.Title))
            .IsEquivalentTo(expected: [InOrder, InOrder, InOrder, InOrder], ordering: CollectionOrdering.Matching);

        await Assert.That(offers.Select(selector: static offer => offer.Key ?? string.Empty))
            .IsEquivalentTo(
                expected: [KeepsEvaluationKey, KeepsEvaluationKey, KeepsEvaluationKey, KeepsEvaluationKey],
                ordering: CollectionOrdering.Matching);

        await Assert.That(offers.Select(selector: static offer => offer.Result))
            .IsEquivalentTo(
                expected:
                [
                    Source(body: body.Replace(oldValue: "Pair(b: 2, a: 1)", newValue: "Pair(a: 1, b: 2)")),
                    Source(body: body.Replace(oldValue: "Pair(b: x, a: Field)", newValue: "Pair(a: Field, b: x)")),
                    Source(body: body.Replace(oldValue: "Three(1, c: 3, b: 2)", newValue: "Three(1, b: 2, c: 3)")),
                    Source(
                        body: body.Replace(
                            oldValue: "Rest(rest: [2, 3], first: 1)",
                            newValue: "Rest(first: 1, rest: [2, 3])"))
                ],
                ordering: CollectionOrdering.Matching);
    }

    // One argument with an effect with only constants keeps the evaluation, because a constant cannot see the
    // effect. An effect with a read of a variable, or two effects, can change what runs first, and the title
    // says so, with a different key.
    [Test]
    public async Task TheTitleSaysWhenTheOrderOfEvaluationChanges()
    {
        const string body =
            """
                    result = Calls.Pair(b: Calls.Next(), a: 1);
                    result = Calls.Pair(b: Calls.Next(), a: x);
                    result = Calls.Pair(b: Calls.Next(), a: Calls.Next());
            """;

        IReadOnlyList<CodeFixHarness.Offer> offers =
            await Offers(body: body).ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(offers.Select(selector: static offer => offer.Title))
            .IsEquivalentTo(
                expected: [InOrder, InOrderAndChangesEvaluation, InOrderAndChangesEvaluation],
                ordering: CollectionOrdering.Matching);

        await Assert.That(offers.Select(selector: static offer => offer.Key ?? string.Empty))
            .IsEquivalentTo(
                expected: [KeepsEvaluationKey, ChangesEvaluationKey, ChangesEvaluationKey],
                ordering: CollectionOrdering.Matching);

        await Assert.That(offers.Select(selector: static offer => offer.Result))
            .IsEquivalentTo(
                expected:
                [
                    Source(
                        body: body.Replace(
                            oldValue: "Pair(b: Calls.Next(), a: 1)",
                            newValue: "Pair(a: 1, b: Calls.Next())")),
                    Source(
                        body: body.Replace(
                            oldValue: "Pair(b: Calls.Next(), a: x)",
                            newValue: "Pair(a: x, b: Calls.Next())")),
                    Source(
                        body: body.Replace(
                            oldValue: "Pair(b: Calls.Next(), a: Calls.Next())",
                            newValue: "Pair(a: Calls.Next(), b: Calls.Next())"))
                ],
                ordering: CollectionOrdering.Matching);
    }

    // The other kinds of call that the rule reads: an indexer, a new, and the base type of a primary
    // constructor. An attribute keeps the assignments of its properties after its constructor arguments.
    [Test]
    public async Task EachKindOfArgumentListIsFixed()
    {
        const string extra =
            """

            public sealed class Derived() : Base(b: 2, a: 1);

            [Mark(b: 2, a: 1, Extra = 3)]
            public sealed class Marked;
            """;

        const string source =
            Declarations
            + """
                      result = grid[column: 2, row: 1];
                      result = new Base(b: 2, a: 1).Sum;
              """
            + "\n"
            + End
            + extra;

        IReadOnlyList<CodeFixHarness.Offer> offers =
            await CodeFixHarness.Offers(
                    analyzer: new NamedArgumentOrderAnalyzer(),
                    provider: new NamedArgumentOrderCodeFixProvider(),
                    source: source)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(offers.Select(selector: static offer => offer.Result))
            .IsEquivalentTo(
                expected:
                [
                    source.Replace(oldValue: "grid[column: 2, row: 1]", newValue: "grid[row: 1, column: 2]"),
                    source.Replace(oldValue: "new Base(b: 2, a: 1)", newValue: "new Base(a: 1, b: 2)"),
                    source.Replace(oldValue: "Base(b: 2, a: 1);", newValue: "Base(a: 1, b: 2);"),
                    source.Replace(oldValue: "[Mark(b: 2, a: 1, Extra = 3)]", newValue: "[Mark(a: 1, b: 2, Extra = 3)]")
                ],
                ordering: CollectionOrdering.Matching);
    }

    // An argument list on more than one line keeps the line of each position, thus the result has the layout of
    // the source.
    [Test]
    public async Task TheLayoutOfTheListStays()
    {
        IReadOnlyList<CodeFixHarness.Offer> offers =
            await Offers(
                    body: """
                                  result = Calls.Three(
                                      c: 3,
                                      a: 1,
                                      b: 2);
                          """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(offers.Select(selector: static offer => Changed(result: offer.Result)))
            .IsEquivalentTo(
                expected: ["result = Calls.Three(\n            a: 1,\n            b: 2,\n            c: 3);"],
                ordering: CollectionOrdering.Matching);
    }

    // One argument declares a variable, and a different argument names it. A new order can put the use before the
    // declaration, thus the provider offers no fix.
    [Test]
    public async Task NoFixIsOfferedWhenAMoveWouldUseAVariableBeforeItIsDeclared()
    {
        IReadOnlyList<CodeFixHarness.Offer> offers =
            await Offers(body: "        result = Calls.Store(b: out int stored, a: stored = 1);")
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(offers).IsEmpty();
    }

    // A fix of all reports with the key that keeps the evaluation does not change a report that changes the
    // evaluation. It fixes an argument list in a different list in the same change. With the other key, it fixes all.
    [Test]
    public async Task AFixOfAllReportsTakesTheReportsOfItsKey()
    {
        const string body =
            """
                    result = Calls.Pair(b: Calls.Pair(b: 4, a: 3), a: 1);
                    result = Calls.Pair(b: Calls.Next(), a: x);
            """;

        string source = Source(body: body);

        string keeping =
            await CodeFixHarness.FixAll(
                    analyzer: new NamedArgumentOrderAnalyzer(),
                    provider: new NamedArgumentOrderCodeFixProvider(),
                    source: source,
                    equivalenceKey: KeepsEvaluationKey)
                .ConfigureAwait(continueOnCapturedContext: false);

        string changing =
            await CodeFixHarness.FixAll(
                    analyzer: new NamedArgumentOrderAnalyzer(),
                    provider: new NamedArgumentOrderCodeFixProvider(),
                    source: source,
                    equivalenceKey: ChangesEvaluationKey)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Changed(result: keeping))
            .IsEqualTo(
                expected: "result = Calls.Pair(a: 1, b: Calls.Pair(a: 3, b: 4));\n"
                          + "        result = Calls.Pair(b: Calls.Next(), a: x);");

        await Assert.That(Changed(result: changing))
            .IsEqualTo(
                expected: "result = Calls.Pair(a: 1, b: Calls.Pair(a: 3, b: 4));\n"
                          + "        result = Calls.Pair(a: x, b: Calls.Next());");

        await Assert.That(new NamedArgumentOrderCodeFixProvider().GetFixAllProvider().GetSupportedFixAllScopes())
            .IsEquivalentTo(
                expected: [FixAllScope.Document, FixAllScope.Project, FixAllScope.Solution],
                ordering: CollectionOrdering.Any);
    }

    private static Task<IReadOnlyList<CodeFixHarness.Offer>> Offers(string body) =>
        CodeFixHarness.Offers(
            analyzer: new NamedArgumentOrderAnalyzer(),
            provider: new NamedArgumentOrderCodeFixProvider(),
            source: Source(body: body));

    private static string Source(string body) => Declarations + body + "\n" + End;

    // The statements of Sample.Run in a result, without the indent of the first.
    private static string Changed(string result)
    {
        int start =
            result.IndexOf(value: "int result;", comparisonType: System.StringComparison.Ordinal)
            + "int result;".Length;

        int end = result.LastIndexOf(value: End, comparisonType: System.StringComparison.Ordinal);

        return result[start..end].Trim();
    }
}
