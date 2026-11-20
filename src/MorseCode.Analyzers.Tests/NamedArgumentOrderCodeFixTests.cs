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

            public static int Use(int a, Func<int> b) => a + b();

            public static int Store(int a, out int b)
            {
                b = a;
                return a;
            }
        }

        public sealed class Grid
        {
            public int Rows = 2;

            public int this[int row, int column] => row * column;

            public int Size() => this.Rows;
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

    // An argument in a wrapper keeps its wrapper, and goes to the position of its parameter. A call in order
    // with such an argument gets no fix. Before, the analyzer gave the wrapped argument the last parameter.
    // Thus, the fix moved a call in order out of order, or put a positional argument after a named one, which
    // does not compile.
    [Test]
    public async Task AnArgumentInsideAWrapperGoesToThePositionOfItsParameter()
    {
        const string body =
            """
                    result = Calls.Three(b: 2, a: (x), c: 3);
                    result = Calls.Three(1, c: checked(3 * 1), b: (2));
                    result = Calls.Three(a: (x), b: 2, c: 3);
                    result = Calls.Three((x), b: 2, c: 3);
            """;

        IReadOnlyList<CodeFixHarness.Offer> offers =
            await Offers(body: body).ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(offers.Select(selector: static offer => offer.Result))
            .IsEquivalentTo(
                expected:
                [
                    Source(body: body.Replace(oldValue: "Three(b: 2, a: (x), c: 3)", newValue: "Three(a: (x), b: 2, c: 3)")),
                    Source(
                        body: body.Replace(
                            oldValue: "Three(1, c: checked(3 * 1), b: (2))",
                            newValue: "Three(1, b: (2), c: checked(3 * 1))"))
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
    // constructor. An indexer in an object initializer is also one. An attribute keeps the assignments of its
    // properties after its constructor arguments.
    [Test]
    public async Task EachKindOfArgumentListIsFixed()
    {
        const string extra =
            """

            public sealed class Derived() : Base(b: 2, a: 1);

            [Mark(b: 2, a: 1, Extra = 3)]
            public sealed class Marked;

            public sealed class Board
            {
                public int this[int row, int column] { get => row * column; set { } }

                public static Board Make() => new() { [column: 2, row: 1] = 3 };
            }
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
                    source.Replace(
                        oldValue: "[Mark(b: 2, a: 1, Extra = 3)]",
                        newValue: "[Mark(a: 1, b: 2, Extra = 3)]"),
                    source.Replace(oldValue: "[column: 2, row: 1] = 3", newValue: "[row: 1, column: 2] = 3")
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

    // A directive between two arguments guards the argument at its position. Before, the fix moved the arguments
    // and left the directive. Thus, the directive guarded a different argument. The provider offers no fix for
    // such a list. The second call has no directive, and gets its fix. The analyzer reports the two calls.
    [Test]
    public async Task NoFixIsOfferedWhereADirectiveIsBetweenTheArguments()
    {
        const string body =
            """
                    result = Calls.Pair(
            #if !NOT_DEFINED
                        b: 2,
            #endif
                        a: 1);
                    result = Calls.Pair(b: 2, a: 1);
            """;

        IReadOnlyList<CodeFixHarness.Offer> offers =
            await Offers(body: body).ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(offers.Select(selector: static offer => offer.Result))
            .IsEquivalentTo(
                expected: [Source(body: body.Replace(oldValue: "Pair(b: 2, a: 1)", newValue: "Pair(a: 1, b: 2)"))],
                ordering: CollectionOrdering.Matching);
    }

    // A comment goes with its argument, and the whitespace stays at its position. That is so for a comment in the
    // argument, for a comment on the line above it, and for a comment after its comma. Before, each comment
    // stayed at its position and then described a different argument. A comment to the end of the line cannot
    // go before a closing parenthesis on the same line. Thus, the provider offers no fix for the third call.
    [Test]
    public async Task CommentsMoveWithTheirArguments()
    {
        const string body =
            """
                    result = Calls.Pair(b: 2 /* second */, a: 1 /* first */);
                    result = Calls.Three(
                        // the third
                        c: 3, // three
                        a: 1, // one
                        b: 2
                    );
                    result = Calls.Pair(
                        b: 2, // two
                        a: 1);
            """;

        IReadOnlyList<CodeFixHarness.Offer> offers =
            await Offers(body: body).ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(offers.Select(selector: static offer => offer.Result))
            .IsEquivalentTo(
                expected:
                [
                    Source(
                        body: body.Replace(
                            oldValue: "Pair(b: 2 /* second */, a: 1 /* first */)",
                            newValue: "Pair(a: 1 /* first */, b: 2 /* second */)")),
                    Source(
                        body: body.Replace(
                            oldValue: """
                                      Three(
                                                  // the third
                                                  c: 3, // three
                                                  a: 1, // one
                                                  b: 2
                                              );
                                      """,
                            newValue: """
                                      Three(
                                                  a: 1, // one
                                                  b: 2,
                                                  // the third
                                                  c: 3 // three
                                              );
                                      """))
                ],
                ordering: CollectionOrdering.Matching);
    }

    // A cast that can throw, such as an unbox, is an effect. With a second effect, the new order can change which
    // exception the call throws, thus the title says that the order of evaluation changes. Before, the fix read
    // the cast as its operand, which is a constant here, and kept the title that says it does not.
    [Test]
    public async Task ACastThatCanThrowIsAnEffect()
    {
        IReadOnlyList<CodeFixHarness.Offer> offers =
            await Offers(body: """        result = Calls.Pair(b: Calls.Next(), a: (int)(object)"text");""")
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(offers.Select(selector: static offer => offer.Title))
            .IsEquivalentTo(expected: [InOrderAndChangesEvaluation], ordering: CollectionOrdering.Matching);

        await Assert.That(offers.Select(selector: static offer => offer.Key ?? string.Empty))
            .IsEquivalentTo(expected: [ChangesEvaluationKey], ordering: CollectionOrdering.Matching);
    }

    // A delegate from a method of an instance throws ArgumentException when the instance is null, and a field read
    // through that instance throws NullReferenceException. Thus, the new order can change the type of the exception.
    // The delegate is an effect, with a method group, with new, and with a cast. A delegate from a static method,
    // or from a method of a value, cannot throw. Two field reads can only throw NullReferenceException in each order,
    // thus they keep the title that says the order stays. Before, the fix read a delegate with new or with a cast as
    // a read, and kept that title. It read each method group as an effect, also a static method and a value.
    [Test]
    public async Task ADelegateFromAMethodOfAnInstanceIsAnEffect()
    {
        const string body =
            """
                    result = Calls.Use(b: grid.Size, a: grid.Rows);
                    result = Calls.Use(b: new Func<int>(grid.Size), a: grid.Rows);
                    result = Calls.Use(b: (Func<int>)grid.Size, a: grid.Rows);
                    result = Calls.Use(b: Calls.Next, a: grid.Rows);
                    result = Calls.Use(b: x.GetHashCode, a: grid.Rows);
                    result = Calls.Pair(b: grid.Rows, a: grid.Rows);
            """;

        IReadOnlyList<CodeFixHarness.Offer> offers =
            await Offers(body: body).ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(offers.Select(selector: static offer => offer.Title))
            .IsEquivalentTo(
                expected:
                [
                    InOrderAndChangesEvaluation, InOrderAndChangesEvaluation, InOrderAndChangesEvaluation, InOrder,
                    InOrder, InOrder
                ],
                ordering: CollectionOrdering.Matching);

        await Assert.That(offers.Select(selector: static offer => offer.Key ?? string.Empty))
            .IsEquivalentTo(
                expected:
                [
                    ChangesEvaluationKey, ChangesEvaluationKey, ChangesEvaluationKey, KeepsEvaluationKey,
                    KeepsEvaluationKey, KeepsEvaluationKey
                ],
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
