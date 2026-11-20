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
///     Compiles samples of code with <see cref="PrimaryConstructorParameterAnalyzer" />, and reads
///     what it reports.
/// </summary>
internal sealed class PrimaryConstructorParameterAnalyzerTests
{
    // A parameter without in, in a class and in a struct. Each one gives one report, and the report
    // covers the name of the parameter.
    [Test]
    public async Task AParameterWithoutInGivesOneReportOnItsName()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    source: """
                            public class Holder(int value, string name)
                            {
                                public int Value { get; } = value;

                                public string Name { get; } = name;
                            }

                            public struct Pair(int first)
                            {
                                public int First { get; } = first;
                            }
                            """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Covered(found: found))
            .IsEquivalentTo(expected: ["value", "name", "first"], ordering: CollectionOrdering.Matching);

        await Assert.That(found[0].GetMessage())
            .IsEqualTo(
                expected: "The primary constructor parameter value of Holder is not in: write in, or copy the value "
                          + "to a field");
    }

    // A parameter by reference: ref, out, ref readonly, and scoped ref. The compiler refuses a member that uses
    // such a parameter (CS9109), thus it cannot become hidden state. A write through ref or out changes the
    // variable of the caller, which is what the modifier is for, and in changes that contract. Thus, the rule
    // does not report it. Before, it did, and only a suppression removed the report. The initializers use each
    // parameter, and the callers pass each one. The parameter by value gets its report as before.
    [Test]
    public async Task AParameterByReferenceIsNotReported()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    source: """
                            using System;

                            public class Doubled(ref int value)
                            {
                                public int Value { get; } = value *= 2;
                            }

                            public class Written(out int value)
                            {
                                public int Value { get; } = value = 7;
                            }

                            public class Copied(ref readonly int value)
                            {
                                public int Value { get; } = value;
                            }

                            public ref struct Window(scoped ref Span<int> values)
                            {
                                public int Length { get; } = values.Length;
                            }

                            public class Plain(int value)
                            {
                                public int Value { get; } = value;
                            }

                            public static class Callers
                            {
                                public static int Use()
                                {
                                    int number = 3;
                                    Span<int> values = stackalloc int[1];
                                    Doubled doubled = new(value: ref number);
                                    Written written = new(value: out int set);
                                    Copied copied = new(value: in number);
                                    Window window = new(values: ref values);

                                    return doubled.Value + written.Value + set + copied.Value + window.Length;
                                }
                            }
                            """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Covered(found: found)).IsEquivalentTo(expected: ["value"], ordering: CollectionOrdering.Matching);
        await Assert.That(found[0].GetMessage()).Contains(expected: "of Plain is not in");
    }

    // A parameter with in, a params parameter, a record, a record struct, and a type with an ordinary
    // constructor. None of these gives a report.
    [Test]
    public async Task InParamsRecordsAndOrdinaryConstructorsGiveNoReport()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    source: """
                            public class Holder(in int value)
                            {
                                public int Value { get; } = value;
                            }

                            public class Many(params int[] values)
                            {
                                public int Count { get; } = values.Length;
                            }

                            public record Point(int X, int Y);

                            public record struct Size(int Width, int Height);

                            public class Plain
                            {
                                public Plain(int value) => this.Value = value;

                                public int Value { get; }
                            }
                            """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found).IsEmpty();
    }

    // C# refuses a use of an attribute whose constructor has an in parameter (CS8358). Thus, the parameter of
    // an attribute cannot be in, and the rule does not report it. That is so for an attribute that derives
    // directly, for one that derives through a different attribute, and for an abstract attribute. The
    // sample applies the attributes, thus it does not compile if a parameter is in. A class whose name ends
    // in Attribute, and that does not derive from Attribute, gets the report, as each other class does.
    [Test]
    public async Task AParameterOfAnAttributeIsNotReported()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    source: """
                            using System;

                            [AttributeUsage(AttributeTargets.Class)]
                            public sealed class MarkAttribute(int level) : Attribute
                            {
                                public int Level { get; } = level;
                            }

                            public abstract class BaseLabelAttribute(int level) : Attribute
                            {
                                public int Level { get; } = level;
                            }

                            [AttributeUsage(AttributeTargets.Class)]
                            public sealed class LabelAttribute(int level, string text) : BaseLabelAttribute(level)
                            {
                                public string Text { get; } = text;
                            }

                            public class CountAttribute(int count)
                            {
                                public int Count { get; } = count;
                            }

                            public class Holder(int value)
                            {
                                public int Value { get; } = value;
                            }

                            [Mark(1)]
                            [Label(2, "two")]
                            public static class Marked;
                            """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Covered(found: found))
            .IsEquivalentTo(expected: ["count", "value"], ordering: CollectionOrdering.Matching);
    }

    // A by-value scoped parameter cannot be in and stay scoped: scoped in limits the reference, and the value is
    // then not scoped. The compiler refuses a member that uses a ref-like parameter (CS9110), and a field that a
    // scoped value initializes (CS8352). Thus, the rule does not report it. The caller returns a value that a
    // stackalloc span made, which compiles only while the parameter has scoped. A ref-like parameter without
    // scoped can be in, and it gets the report as before.
    [Test]
    public async Task AByValueScopedParameterIsNotReported()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    source: """
                            using System;

                            public ref struct Window(scoped ReadOnlySpan<int> values)
                            {
                                public int Length { get; } = values.Length;
                            }

                            public ref struct Open(ReadOnlySpan<int> values)
                            {
                                public int Length { get; } = values.Length;
                            }

                            public static class Callers
                            {
                                public static Window Make()
                                {
                                    Span<int> local = stackalloc int[1];

                                    return new Window(values: local);
                                }
                            }
                            """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Covered(found: found))
            .IsEquivalentTo(expected: ["values"], ordering: CollectionOrdering.Matching);

        await Assert.That(found[0].GetMessage())
            .IsEqualTo(
                expected: "The primary constructor parameter values of Open is not in: write scoped in, or copy the "
                          + "value to a field");
    }

    // An in parameter of the constructor of a ref struct lets the result capture the reference. Thus, a caller
    // that returns the result does not compile. Scoped in does not let it. Thus, the report on a parameter of a
    // ref struct says scoped in, also for a readonly ref struct. The report on a different struct says in.
    [Test]
    public async Task TheReportOnAParameterOfARefStructSaysScopedIn()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    source: """
                            using System;

                            public ref struct Number(int value)
                            {
                                public int Value { get; } = value;
                            }

                            public readonly ref struct Open(ReadOnlySpan<int> values)
                            {
                                public int Length { get; } = values.Length;
                            }

                            public struct Plain(int value)
                            {
                                public int Value { get; } = value;
                            }
                            """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(found.Select(selector: static diagnostic => diagnostic.GetMessage()))
            .IsEquivalentTo(
                expected:
                [
                    "The primary constructor parameter value of Number is not in: write scoped in, or copy the value "
                    + "to a field",
                    "The primary constructor parameter values of Open is not in: write scoped in, or copy the value "
                    + "to a field",
                    "The primary constructor parameter value of Plain is not in: write in, or copy the value to a field"
                ],
                ordering: CollectionOrdering.Matching);
    }

    // An editor can analyze code before it loads the references. Then the compilation has no System.Attribute,
    // and the rule reports a parameter as it does for each class that is not an attribute.
    [Test]
    public async Task WithNoCoreLibraryAParameterIsReported()
    {
        IReadOnlyList<Diagnostic> found =
            await AnalyzerHarness.Analyze(
                    analyzer: new PrimaryConstructorParameterAnalyzer(),
                    source: """
                            public class Count(int count)
                            {
                                public int Value { get; } = count;
                            }
                            """,
                    errors: ["CS0518", "CS0518", "CS1729", "CS0518", "CS0518"],
                    platform: false)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Covered(found: found)).IsEquivalentTo(expected: ["count"], ordering: CollectionOrdering.Matching);
    }

    // The parameter of a derived class that goes to the base is a primary constructor parameter too.
    [Test]
    public async Task AParameterThatGoesToTheBaseIsReported()
    {
        IReadOnlyList<Diagnostic> found =
            await Analyze(
                    source: """
                            public class Base(in int value)
                            {
                                public int Value { get; } = value;
                            }

                            public class Derived(int value) : Base(value);

                            public class DerivedWithIn(in int value) : Base(value);
                            """)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(Covered(found: found)).IsEquivalentTo(expected: ["value"], ordering: CollectionOrdering.Matching);
        await Assert.That(found[0].Location.GetLineSpan().StartLinePosition.Line).IsEqualTo(expected: 5);
    }

    private static Task<IReadOnlyList<Diagnostic>> Analyze(string source) =>
        AnalyzerHarness.Analyze(analyzer: new PrimaryConstructorParameterAnalyzer(), source: source);

    // The text that each report covers, in the order of the source.
    private static List<string> Covered(IEnumerable<Diagnostic> found) =>
    [
        .. found.Select(selector: static diagnostic => diagnostic.Location.SourceTree?.GetText().ToString(span: diagnostic.Location.SourceSpan) ?? string.Empty)
    ];
}
