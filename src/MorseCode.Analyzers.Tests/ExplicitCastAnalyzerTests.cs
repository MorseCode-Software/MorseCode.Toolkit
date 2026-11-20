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
///     Compiles samples of code with <see cref="ExplicitCastAnalyzer" />, and reads
///     what it reports.
/// </summary>
internal sealed class ExplicitCastAnalyzerTests
{
    // The types that the samples convert. Each sample is the body of Calls.Run, after them.
    private const string Declarations =
        """
        using System;

        public enum Color { Red, Green }

        public readonly struct Money
        {
            public static explicit operator Money(decimal value) => default;

            public static implicit operator decimal(Money value) => 0m;
        }

        public interface IShape
        {
            int Sides { get; }
        }

        public class Square : IShape
        {
            int IShape.Sides => 4;
        }

        public static class Calls
        {
            public static void Run(object obj, long big, int? maybe, IShape shape, Color color)
            {
        """;

    private const string End =
        """
            }
        }
        """;

    // One cast of each kind of conversion. Each one gives one report, and the report covers the type in parentheses.
    [Test]
    public async Task EachKindOfExplicitCastGivesOneReport()
    {
        IReadOnlyList<string> found =
            await Casts(
                    body: """
                          _ = (string)obj;
                          _ = (int)obj;
                          _ = (int)big;
                          _ = (byte)big;
                          _ = (int)color;
                          _ = (Color)1;
                          _ = (int)maybe;
                          _ = (Square)shape;
                          _ = (Money)1.5m;
                          _ = (decimal)new Money();
                          _ = ((string)obj).Length;
                          Func<object, string> convert = value => (string)value;
                          """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found)
            .IsEquivalentTo(
                expected:
                [
                    "(string)",
                    "(int)",
                    "(int)",
                    "(byte)",
                    "(int)",
                    "(Color)",
                    "(int)",
                    "(Square)",
                    "(Money)",
                    "(decimal)",
                    "(string)",
                    "(string)"
                ],
                ordering: CollectionOrdering.Matching);
    }

    // The ways to avoid a cast, and the syntax that looks like one. None of these gives a report.
    [Test]
    public async Task ConversionsThatAreNotExplicitCastsGiveNoReport()
    {
        IReadOnlyList<string> found =
            await Casts(
                    body: """
                          _ = obj as string;
                          _ = obj is string;
                          _ = obj is int number ? number : 0;
                          _ = obj switch { string text => text.Length, _ => 0 };
                          long widened = 3;
                          object boxed = widened;
                          decimal converted = new Money();
                          int? lifted = 3;
                          _ = (big);
                          _ = (big) + 1;
                          _ = (-1);
                          _ = typeof(int);
                          _ = default(int);
                          _ = checked(big + 1);
                          _ = (first: 1, second: 2);
                          """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found).IsEmpty();
    }

    // A cast on a long expression marks the type only. The value after it, with its own casts and calls, is
    // outside the report, and a cast inside it has a report of its own.
    [Test]
    public async Task TheReportMarksTheTypeAndNotTheValueAfterIt()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    body: "_ = (int)Math.Abs(value: (long)obj);")
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found.Select(selector: static diagnostic => diagnostic.Location.SourceSpan.Length))
            .IsEquivalentTo(expected: [5, 6], ordering: CollectionOrdering.Matching);

        await Assert.That(found.Select(selector: static diagnostic => Covered(diagnostic: diagnostic)))
            .IsEquivalentTo(expected: ["(int)", "(long)"], ordering: CollectionOrdering.Matching);
    }

    // A tuple type in the parentheses has its own commas and parentheses. The report still covers the whole type.
    [Test]
    public async Task ACastToATupleTypeGivesOneReportForTheType()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    body: """
                          _ = ((int, int))obj;
                          _ = ((int first, int second))(big, big);
                          _ = (ValueTuple<int, int>)obj;
                          """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found.Select(selector: static diagnostic => Covered(diagnostic: diagnostic)))
            .IsEquivalentTo(
                expected: ["((int, int))", "((int first, int second))", "(ValueTuple<int, int>)"],
                ordering: CollectionOrdering.Matching);

        await Assert.That(found.Select(selector: static diagnostic => diagnostic.GetMessage().Split(':')[0]))
            .IsEquivalentTo(
                expected:
                [
                    "Avoid the explicit cast to (int, int)",
                    "Avoid the explicit cast to (int first, int second)",
                    "Avoid the explicit cast to (int, int)"
                ],
                ordering: CollectionOrdering.Matching);
    }

    // A cast to or from dynamic is a cast, and the report names it. The compiler classes a conversion from
    // dynamic as implicit, because int number = unknown compiles. But the cast runs an explicit conversion when
    // the code runs, and the code without it runs an implicit one. A long in unknown converts with the cast and
    // throws without it. Thus, the message does not say that the cast is not needed. Before, it did.
    [Test]
    public async Task ACastFromDynamicGivesOneReportThatDoesNotSayItIsNotNeeded()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    body: """
                          dynamic unknown = obj;
                          _ = (int)unknown;
                          _ = (dynamic)obj;
                          """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found.Select(selector: static diagnostic => diagnostic.GetMessage()))
            .IsEquivalentTo(
                expected:
                [
                    "Avoid the explicit cast to int: change the code so that the conversion is not necessary",
                    "Avoid the explicit cast to dynamic: an implicit conversion exists, thus the cast is not needed"
                ],
                ordering: CollectionOrdering.Matching);
    }

    // An implicit conversion exists for each of these casts, but each one chooses something. The cast to double
    // makes the division one of doubles. The casts to object make == compare the references. The cast to
    // the interface reaches a member that the class implements explicitly. Without the cast, each one does a
    // different thing or does not compile. Thus, the message tells how to keep the type without a cast. Before,
    // it said that the cast is not needed.
    [Test]
    public async Task ACastThatChoosesAnOperationIsNotCalledNotNeeded()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    body: """
                          int sum = 7;
                          int count = 2;
                          string left = "a";
                          string right = "a";
                          Square square = new();
                          _ = (double)sum / count;
                          _ = (object)left == (object)right;
                          _ = ((IShape)square).Sides;
                          """)
                .ConfigureAwait(continueOnCapturedContext: false);

        const string chooses =
            "an implicit conversion exists: put the value in a variable of that type in place of the cast";

        await Assert.That(found.Select(selector: static diagnostic => diagnostic.GetMessage()))
            .IsEquivalentTo(
                expected:
                [
                    $"Avoid the explicit cast to double: {chooses}",
                    $"Avoid the explicit cast to object: {chooses}",
                    $"Avoid the explicit cast to object: {chooses}",
                    $"Avoid the explicit cast to IShape: {chooses}"
                ],
                ordering: CollectionOrdering.Matching);
    }

    // The message says that the cast is not needed only where the value goes directly to a destination of the
    // type of the cast. Each destination is one of these: the initializer of a variable of that declared type, an
    // assignment, or a return of that type. A variable with var gets its type from the cast, and the return of a
    // lambda can get its type from it too. Thus, those get the other message.
    [Test]
    public async Task ACastToTheTypeOfItsDestinationIsNotNeeded()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    body: """
                          object boxed = (object)big;
                          IShape asShape = (IShape)new Square();
                          long wide = 0;
                          wide = (long)big;
                          object Box() => (object)big;
                          var guessed = (object)big;
                          Func<object> later = () => (object)big;
                          _ = boxed;
                          _ = asShape;
                          _ = wide;
                          _ = Box();
                          _ = guessed;
                          _ = later;
                          """)
                .ConfigureAwait(continueOnCapturedContext: false);

        const string notNeeded = "an implicit conversion exists, thus the cast is not needed";

        const string chooses =
            "an implicit conversion exists: put the value in a variable of that type in place of the cast";

        await Assert.That(found.Select(selector: static diagnostic => diagnostic.GetMessage()))
            .IsEquivalentTo(
                expected:
                [
                    $"Avoid the explicit cast to object: {notNeeded}",
                    $"Avoid the explicit cast to IShape: {notNeeded}",
                    $"Avoid the explicit cast to long: {notNeeded}",
                    $"Avoid the explicit cast to object: {notNeeded}",
                    $"Avoid the explicit cast to object: {chooses}",
                    $"Avoid the explicit cast to object: {chooses}"
                ],
                ordering: CollectionOrdering.Matching);
    }

    // The initializer of a property with a declared type is a destination too. It is a member and not a
    // statement, thus it has a sample of its own.
    [Test]
    public async Task ACastInThePropertyInitializerOfItsTypeIsNotNeeded()
    {
        IReadOnlyList<Diagnostic> found =
            await AnalyzerHarness.Analyze(
                    analyzer: new ExplicitCastAnalyzer(),
                    source: """
                            public static class Values
                            {
                                public static long Wide { get; } = (long)1;

                                public static object Boxed { get; } = (long)1;
                            }
                            """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found.Select(selector: static diagnostic => diagnostic.GetMessage()))
            .IsEquivalentTo(
                expected:
                [
                    "Avoid the explicit cast to long: an implicit conversion exists, thus the cast is not needed",
                    "Avoid the explicit cast to long: an implicit conversion exists: put the value in a variable of that type in place of the cast"
                ],
                ordering: CollectionOrdering.Matching);
    }

    // A pointer cast is a cast too. It needs an unsafe context, thus it has a sample of its own.
    [Test]
    public async Task APointerCastGivesOneReportForTheType()
    {
        IReadOnlyList<Diagnostic> found =
            await AnalyzerHarness.Analyze(
                    analyzer: new ExplicitCastAnalyzer(),
                    source: """
                            public static unsafe class Calls
                            {
                                public static void Run(int* pointer, long address)
                                {
                                    _ = (long)pointer;
                                    _ = (byte*)pointer;
                                    _ = (int*)address;
                                }
                            }
                            """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found.Select(selector: static diagnostic => Covered(diagnostic: diagnostic)))
            .IsEquivalentTo(expected: ["(long)", "(byte*)", "(int*)"], ordering: CollectionOrdering.Matching);
    }

    // The message names the type that the cast converts to, and gives the way out that suits the conversion.
    [Test]
    public async Task TheReportNamesTheTypeAndTheWayOut()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    body: """
                          _ = (string)obj;
                          _ = (int)obj;
                          _ = (byte)big;
                          _ = (Money)1.5m;
                          _ = (object)big;
                          """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found.Select(selector: static diagnostic => diagnostic.GetMessage()))
            .IsEquivalentTo(
                expected:
                [
                    "Avoid the explicit cast to string: match the type with a pattern, or keep the value in a variable of the type that you need",
                    "Avoid the explicit cast to int: match the type with a pattern, or keep the value in a variable of the type that you need",
                    "Avoid the explicit cast to byte: keep the value in a type of the right size, or convert it in one place with a name",
                    "Avoid the explicit cast to Money: call a method that converts the value, with a name that says what it does",
                    "Avoid the explicit cast to object: an implicit conversion exists, thus the cast is not needed"
                ],
                ordering: CollectionOrdering.Matching);

        await Assert.That(found.Select(selector: static diagnostic => diagnostic.Id).Distinct())
            .IsEquivalentTo(expected: ["MC0002"], ordering: CollectionOrdering.Matching);

        await Assert.That(found.Select(selector: static diagnostic => diagnostic.Severity).Distinct())
            .IsEquivalentTo(expected: [DiagnosticSeverity.Warning], ordering: CollectionOrdering.Matching);
    }

    // A foreach with a type makes the conversion that a cast makes, for each element, and the source has no cast.
    // The report marks the type of the loop variable. A loop with var, with the type of the elements, or with a
    // type that the elements convert to implicitly, gives no report.
    [Test]
    public async Task AForeachThatConvertsEachElementExplicitlyIsReported()
    {
        IReadOnlyList<Diagnostic> found =
            await AnalyzerHarness.Analyze(
                    analyzer: new ExplicitCastAnalyzer(),
                    source: """
                            using System.Collections;
                            using System.Collections.Generic;

                            public static class Loops
                            {
                                public static void Run(
                                    IEnumerable<object> objects,
                                    IEnumerable untyped,
                                    IEnumerable<long> longs,
                                    IEnumerable<string> strings)
                                {
                                    foreach (string text in objects) { _ = text; }
                                    foreach (string text in untyped) { _ = text; }
                                    foreach (int number in longs) { _ = number; }
                                    foreach (var text in objects) { _ = text; }
                                    foreach (string text in strings) { _ = text; }
                                    foreach (object value in strings) { _ = value; }
                                    foreach (long number in longs) { _ = number; }
                                }
                            }
                            """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found.Select(selector: static diagnostic => Covered(diagnostic: diagnostic)))
            .IsEquivalentTo(expected: ["string", "string", "int"], ordering: CollectionOrdering.Matching);

        await Assert.That(found.Select(selector: static diagnostic => diagnostic.GetMessage()))
            .IsEquivalentTo(
                expected:
                [
                    "Avoid the explicit cast to string: the foreach casts each element; use a sequence of that type, or filter it with OfType",
                    "Avoid the explicit cast to string: the foreach casts each element; use a sequence of that type, or filter it with OfType",
                    "Avoid the explicit cast to int: the foreach casts each element; keep the elements in a type of the right size, or convert them in one place with a name"
                ],
                ordering: CollectionOrdering.Matching);
    }

    // The way out of a foreach follows the kind of the conversion, as the way out of a cast does. A conversion
    // that the user wrote gets the advice for a method. A conversion of a tuple, and a conversion from dynamic,
    // get the last advice.
    [Test]
    public async Task AForeachGivesTheWayOutThatSuitsItsConversion()
    {
        IReadOnlyList<Diagnostic> found =
            await AnalyzerHarness.Analyze(
                    analyzer: new ExplicitCastAnalyzer(),
                    source: """
                            using System.Collections.Generic;

                            public readonly struct Meters
                            {
                                public static explicit operator Meters(double value) => default;
                            }

                            public static class Loops
                            {
                                public static void Run(
                                    IEnumerable<double> lengths,
                                    IEnumerable<(long, long)> pairs,
                                    IEnumerable<dynamic> values)
                                {
                                    foreach (Meters length in lengths) { _ = length; }
                                    foreach ((int, int) pair in pairs) { _ = pair; }
                                    foreach (int value in values) { _ = value; }
                                }
                            }
                            """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found.Select(selector: static diagnostic => diagnostic.GetMessage()))
            .IsEquivalentTo(
                expected:
                [
                    "Avoid the explicit cast to Meters: the foreach casts each element; call a method that converts each element, with a name that says what it does",
                    "Avoid the explicit cast to (int, int): the foreach casts each element; change the code so that the conversion is not necessary",
                    "Avoid the explicit cast to int: the foreach casts each element; change the code so that the conversion is not necessary"
                ],
                ordering: CollectionOrdering.Matching);
    }

    // A range variable with a type makes the query call Cast for each element, and the source has no cast. The
    // report marks the type in a from clause and in a join clause. A range variable with no type, or with a
    // type that the elements convert to implicitly, gives no report.
    [Test]
    public async Task AQueryRangeVariableThatConvertsEachElementExplicitlyIsReported()
    {
        IReadOnlyList<Diagnostic> found =
            await AnalyzerHarness.Analyze(
                    analyzer: new ExplicitCastAnalyzer(),
                    source: """
                            using System.Collections;
                            using System.Collections.Generic;
                            using System.Linq;

                            public static class Queries
                            {
                                public static void Run(IEnumerable<object> objects, IEnumerable untyped, IEnumerable<string> strings)
                                {
                                    _ = from string text in objects select text.Length;
                                    _ = from string text in untyped select text.Length;
                                    _ = from text in strings join string other in objects on text equals other select text;
                                    _ = from text in strings select text.Length;
                                    _ = from object value in strings select value;
                                }
                            }
                            """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found.Select(selector: static diagnostic => Covered(diagnostic: diagnostic)))
            .IsEquivalentTo(expected: ["string", "string", "string"], ordering: CollectionOrdering.Matching);

        await Assert.That(found.Select(selector: static diagnostic => diagnostic.GetMessage()).Distinct())
            .IsEquivalentTo(
                expected:
                [
                    "Avoid the explicit cast to string: the query casts each element with Cast; use a sequence of that type, or filter it with OfType"
                ],
                ordering: CollectionOrdering.Matching);
    }

    // A type with two IEnumerable<T> has no one type of element. Cast reads each element through the
    // IEnumerable that is not generic, thus it converts from object. The analyzer reports a range variable of a
    // value type, which unboxes each element. A range variable of type object gives no report.
    [Test]
    public async Task AQueryOverATypeWithTwoTypesOfElementConvertsFromObject()
    {
        IReadOnlyList<Diagnostic> found =
            await AnalyzerHarness.Analyze(
                    analyzer: new ExplicitCastAnalyzer(),
                    source: """
                            using System.Collections;
                            using System.Collections.Generic;
                            using System.Linq;

                            public sealed class Both : IEnumerable<int>, IEnumerable<long>
                            {
                                IEnumerator<int> IEnumerable<int>.GetEnumerator() => new List<int>().GetEnumerator();

                                IEnumerator<long> IEnumerable<long>.GetEnumerator() => new List<long>().GetEnumerator();

                                IEnumerator IEnumerable.GetEnumerator() => new List<int>().GetEnumerator();
                            }

                            public static class Queries
                            {
                                public static void Run(Both both)
                                {
                                    _ = from byte number in both select number;
                                    _ = from int number in both select number;
                                    _ = from object value in both select value;
                                }
                            }
                            """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found.Select(selector: static diagnostic => diagnostic.GetMessage()))
            .IsEquivalentTo(
                expected:
                [
                    "Avoid the explicit cast to byte: the query casts each element with Cast; use a sequence of that type, or filter it with OfType",
                    "Avoid the explicit cast to int: the query casts each element with Cast; use a sequence of that type, or filter it with OfType"
                ],
                ordering: CollectionOrdering.Matching);
    }

    // An editor analyzes code before it compiles. A cast, a foreach, or a range variable whose type has an
    // error gives no report. A query over a sequence with an error also gives no report.
    [Test]
    public async Task CodeWithAnErrorInTheTypeGivesNoReport()
    {
        IReadOnlyList<Diagnostic> found =
            await AnalyzerHarness.Analyze(
                    analyzer: new ExplicitCastAnalyzer(),
                    source: """
                            using System.Collections.Generic;
                            using System.Linq;

                            public static class Broken
                            {
                                public static void Run(object value, IEnumerable<object> objects)
                                {
                                    _ = (Missing)value;
                                    _ = from int number in missing select number;
                                    _ = from Missing item in objects select item;
                                    foreach (Missing item in objects) { _ = item; }
                                }
                            }
                            """,
                    errors: ["CS0246", "CS0103", "CS0246", "CS0246"])
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found).IsEmpty();
    }

    // A suppression is how the few places that need a cast say so. The reason goes next to it. The compiler
    // does not give a diagnostic that a pragma suppresses, thus only the cast after the pragma is left.
    [Test]
    public async Task APragmaSuppressesTheReport()
    {
        IReadOnlyList<string> found =
            await Casts(
                    body: """
                          #pragma warning disable MC0002 // The value is a string by the contract of the caller.
                          _ = (string)obj;
                          #pragma warning restore MC0002
                          _ = (int)big;
                          """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found).IsEquivalentTo(expected: ["(int)"], ordering: CollectionOrdering.Matching);
    }

    // The attribute is the other way to suppress the rule, for a whole method.
    [Test]
    public async Task ASuppressMessageAttributeSuppressesTheReport()
    {
        IReadOnlyList<Diagnostic> found =
            await AnalyzerHarness.Analyze(
                    analyzer: new ExplicitCastAnalyzer(),
                    source: """
                            using System.Diagnostics.CodeAnalysis;

                            public static class Calls
                            {
                                [SuppressMessage("Style", "MC0002", Justification = "The caller proved the type.")]
                                public static string Suppressed(object obj) => (string)obj;

                                public static string Reported(object obj) => (string)obj;
                            }
                            """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found.Select(selector: static diagnostic => diagnostic.Location.GetLineSpan().StartLinePosition.Line))
            .IsEquivalentTo(expected: [7], ordering: CollectionOrdering.Matching);
    }

    // The analyzer leaves generated code alone, because its writer cannot change it.
    [Test]
    public async Task GeneratedCodeGivesNoReport()
    {
        IReadOnlyList<Diagnostic> found =
            await AnalyzerHarness.Analyze(
                    analyzer: new ExplicitCastAnalyzer(),
                    source: "// <auto-generated />\n" + Sample(body: "_ = (string)obj;"))
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found).IsEmpty();
    }

    // The text of each cast that the analyzer reports, in the order of the source.
    private static async Task<IReadOnlyList<string>> Casts(string body)
    {
        IReadOnlyList<Diagnostic> found = await Analyze(body: body).ConfigureAwait(continueOnCapturedContext: false);

        return [.. found.Select(selector: static diagnostic => Covered(diagnostic: diagnostic))];
    }

    // The text that a report marks.
    private static string Covered(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree?.GetText().ToString(span: diagnostic.Location.SourceSpan) ?? string.Empty;

    private static Task<IReadOnlyList<Diagnostic>> Analyze(string body) =>
        AnalyzerHarness.Analyze(analyzer: new ExplicitCastAnalyzer(), source: Sample(body: body));

    private static string Sample(string body) => Declarations + "\n" + body + "\n" + End;
}
