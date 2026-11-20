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
