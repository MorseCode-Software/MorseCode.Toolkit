using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     The report of a <see cref="VirtualizingItemsControl" /> to its source. It has the positions
///     that show, how much of each one shows, and the positions that the control wants rows for.
/// </summary>
/// <remarks>
///     <para>
///         The parts of this value come in one write, thus they are always from the same instant.
///     </para>
///     <para>
///         The two ranges are inside the list as the control knew it at the report. The count of
///         the view model can be different by then. Thus, a view model that builds its own rows
///         stops at its own count.
///     </para>
///     <para>
///         The control makes a new value at each step of a scroll, because the part of a row that
///         shows changes then. <see cref="Constructed" /> changes less often. Thus, a view model
///         that builds its own rows calms <see cref="Constructed" /> before it builds from it.
///     </para>
/// </remarks>
/// <param name="Visible">
///     The positions that Avalonia has a container for. With the default panel, these are the
///     positions that show, and a row that shows in part is one of them.
/// </param>
/// <param name="Constructed">
///     The positions that the control wants rows for. These are the positions of
///     <paramref name="Visible" />, with more positions on each side so that a scroll finds its rows.
///     The control sets how many.
/// </param>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - The summary of ValueType does not say what the control reports.
public readonly record struct ReportedRange(ItemRange Visible, ItemRange Constructed)
{
    /// <summary>
    ///     Gets the report with no position.
    /// </summary>
    public static ReportedRange Empty { get; } = new(Visible: ItemRange.Empty, Constructed: ItemRange.Empty);

    // The number of positions after the end of the list that the control also wants rows for, when
    // the list gets them. A range in this value cannot hold them, because each range stays inside the
    // list. Only the slice of a collection source reads this number.
    internal int Ahead { get; init; }

    // How much of each position of Visible shows.
    internal VisibleParts Parts { get; init; }

    /// <summary>
    ///     Gets how much of the row at a position shows.
    /// </summary>
    /// <param name="position">The position in the whole list.</param>
    /// <returns>
    ///     The part of the area of the row that is in the viewport. The number is 1.0 for a row that
    ///     shows in full, and 0.0 for a row that does not show. A row that shows in part gives a
    ///     number between the two.
    /// </returns>
    /// <remarks>
    ///     The viewport is the part of the list that no control above it clips. A window that covers
    ///     the list does not change the number. A position outside <see cref="Visible" /> gives 0.0.
    /// </remarks>
    public double VisibilityOf(int position)
    {
        int index = position - this.Visible.First;

        return index < 0 || index >= this.Visible.Count ? 0.0 : this.Parts.At(index: index);
    }

    /// <summary>
    ///     Makes a copy of this value that says how much of each position of <see cref="Visible" />
    ///     shows. The control makes its reports this way, and a test of a view model can do the same.
    /// </summary>
    /// <param name="parts">
    ///     One number from 0.0 to 1.0 for each position of <see cref="Visible" />, in sequence.
    /// </param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parts" /> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     The count of <paramref name="parts" /> is not the count of <see cref="Visible" />, or a
    ///     number is not from 0.0 to 1.0.
    /// </exception>
    public ReportedRange WithVisibility(IReadOnlyList<double> parts)
    {
        ArgumentNullException.ThrowIfNull(argument: parts);

        if (parts.Count != this.Visible.Count)
        {
            throw new ArgumentException(
                message: $"The value has {this.Visible.Count} visible positions and got {parts.Count} numbers.",
                paramName: nameof(parts));
        }

        if (parts.Any(predicate: static part => part is not (>= 0.0 and <= 1.0)))
        {
            throw new ArgumentException(message: "Each number must be from 0.0 to 1.0.", paramName: nameof(parts));
        }

        return this with { Parts = VisibleParts.From(parts: [.. parts]) };
    }

    // How much of each visible position shows, in sequence. The equality of an array is its reference,
    // and the source drops a report only when it is equal to the last one. Thus, this type compares
    // the numbers.
    // ReSharper disable once InheritdocConsiderUsage - The summary of ValueType does not say what the numbers are.
    internal readonly struct VisibleParts : IEquatable<VisibleParts>
    {
        // No array means that each position shows in full. That is the usual state of a list at rest. It
        // is also the state of a value that a view model makes with the two ranges only.
        private readonly double[]? parts;

        private VisibleParts(double[]? parts) => this.parts = parts;

        // Takes the array, and does not copy it. The caller must not change it.
        public static VisibleParts From(double[] parts) =>
            new(parts: parts.All(predicate: static part => part >= 1.0) ? null : parts);

        public double At(int index) => this.parts is null || index >= this.parts.Length ? 1.0 : this.parts[index];

        public bool Equals(VisibleParts other) =>
            ReferenceEquals(objA: this.parts, objB: other.parts)
            || (this.parts is not null
                && other.parts is not null
                && this.parts.AsSpan().SequenceEqual(other: other.parts));

        public override bool Equals(object? obj) => obj is VisibleParts other && this.Equals(other: other);

        // Equal values have arrays of the same length.
        public override int GetHashCode() => this.parts?.Length ?? 0;
    }
}
