using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     Checks the value that a <see cref="VirtualizingItemsControl" /> reports, without a control.
/// </summary>
public sealed class ReportedRangeTests
{
    private static readonly ReportedRange Reported =
        new(Visible: new ItemRange(First: 10, Count: 3), Constructed: new ItemRange(First: 0, Count: 23));

    [Test]
    public async Task AValueWithTheTwoRangesOnlyShowsEachVisiblePositionInFull()
    {
        await Assert.That(Reported.VisibilityOf(position: 9)).IsEqualTo(expected: 0.0);
        await Assert.That(Reported.VisibilityOf(position: 10)).IsEqualTo(expected: 1.0);
        await Assert.That(Reported.VisibilityOf(position: 12)).IsEqualTo(expected: 1.0);
        await Assert.That(Reported.VisibilityOf(position: 13)).IsEqualTo(expected: 0.0);
    }

    [Test]
    public async Task AValueWithPartsGivesThePartOfEachVisiblePosition()
    {
        ReportedRange withParts = Reported.WithVisibility(parts: [0.25, 1.0, 0.5]);

        await Assert.That(withParts.VisibilityOf(position: 9)).IsEqualTo(expected: 0.0);
        await Assert.That(withParts.VisibilityOf(position: 10)).IsEqualTo(expected: 0.25);
        await Assert.That(withParts.VisibilityOf(position: 11)).IsEqualTo(expected: 1.0);
        await Assert.That(withParts.VisibilityOf(position: 12)).IsEqualTo(expected: 0.5);
        await Assert.That(withParts.VisibilityOf(position: 13)).IsEqualTo(expected: 0.0);
        await Assert.That(withParts.Visible).IsEqualTo(expected: Reported.Visible);
        await Assert.That(withParts.Constructed).IsEqualTo(expected: Reported.Constructed);
    }

    // The source drops a report that is equal to the last one. Thus, two values with the same numbers
    // must be equal, and the equality of an array is not enough for that.
    [Test]
    public async Task TwoValuesAreEqualWhenTheirPartsAreTheSameNumbers()
    {
        await Assert.That(Reported.WithVisibility(parts: [0.25, 1.0, 0.5]))
            .IsEqualTo(expected: Reported.WithVisibility(parts: [0.25, 1.0, 0.5]));

        await Assert.That(Reported.WithVisibility(parts: [0.25, 1.0, 0.5]))
            .IsNotEqualTo(notExpected: Reported.WithVisibility(parts: [0.25, 1.0, 0.75]));

        await Assert.That(Reported.WithVisibility(parts: [0.25, 1.0, 0.5])).IsNotEqualTo(notExpected: Reported);

        // Each position in full is the value with the two ranges only.
        await Assert.That(Reported.WithVisibility(parts: [1.0, 1.0, 1.0])).IsEqualTo(expected: Reported);
    }

    [Test]
    public async Task PartsThatDoNotFitTheVisiblePositionsAreRefused()
    {
        await Assert.That(static () => Reported.WithVisibility(parts: [1.0, 1.0])).Throws<ArgumentException>();
        await Assert.That(static () => Reported.WithVisibility(parts: [1.0, 1.5, 1.0])).Throws<ArgumentException>();
        await Assert.That(static () => Reported.WithVisibility(parts: [1.0, -0.1, 1.0])).Throws<ArgumentException>();

        await Assert.That(static () => Reported.WithVisibility(parts: [1.0, double.NaN, 1.0]))
            .Throws<ArgumentException>();

        // ReSharper disable once NullableWarningSuppressionIsUsed - The null is the input under test.
        await Assert.That(static () => Reported.WithVisibility(parts: null!)).Throws<ArgumentNullException>();
    }
}
