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

        public interface IShape { }

        public class Square : IShape { }

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

    // One cast of each kind of conversion. Each one gives one report, and the report covers the whole cast.
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
                    "(string)obj",
                    "(int)obj",
                    "(int)big",
                    "(byte)big",
                    "(int)color",
                    "(Color)1",
                    "(int)maybe",
                    "(Square)shape",
                    "(Money)1.5m",
                    "(decimal)new Money()",
                    "(string)obj",
                    "(string)value"
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

        await Assert.That(found).IsEquivalentTo(expected: ["(int)big"], ordering: CollectionOrdering.Matching);
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

        return
        [
            .. found.Select(
                selector: static diagnostic =>
                    diagnostic.Location.SourceTree?.GetText().ToString(span: diagnostic.Location.SourceSpan)
                    ?? string.Empty)
        ];
    }

    private static Task<IReadOnlyList<Diagnostic>> Analyze(string body) =>
        AnalyzerHarness.Analyze(analyzer: new ExplicitCastAnalyzer(), source: Sample(body: body));

    private static string Sample(string body) => Declarations + "\n" + body + "\n" + End;
}
