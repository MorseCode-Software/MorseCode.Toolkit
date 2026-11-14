using JetBrains.Annotations;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     The values that a <see cref="VirtualizingItemsControl" /> reads from a view model, and the
///     actions that it runs to report to it. The view model gives them to the control as one object,
///     and the control attaches to all of them.
/// </summary>
/// <remarks>
///     <para>
///         Make a source with <c>CreateVirtualItemsSource</c>. It builds the members so that they
///         agree: the rows that answer the range, and the request that the report clears.
///     </para>
///     <para>
///         The members go in two pairs. The control runs <see cref="RangeReported" />, and the view
///         model answers with <see cref="Rows" />. The view model makes a <see cref="ScrollRequest" />,
///         and the control answers with <see cref="ScrollHandled" />.
///     </para>
///     <para>
///         A report of the control is an action and not a value that the control writes. An action
///         does nothing after the disposal of the view model, and a write to a two-way value throws
///         then. Thus, a view that outlives its view model can report with no effect.
///     </para>
/// </remarks>
[PublicAPI]
public interface IVirtualItemsSource
{
    /// <summary>
    ///     Gets the rows for some of the positions, with the position of the first and the number of
    ///     positions in the whole list.
    /// </summary>
    /// <remarks>
    ///     The count travels with the rows. Thus, the control applies a change of both in one update,
    ///     and it never shows a count with the rows of a different instant.
    /// </remarks>
    IOneWayBindableValue<VirtualRows> Rows { get; }

    /// <summary>
    ///     Gets the action that the control runs with its report. The report has the positions that
    ///     show, and the positions that the control wants rows for.
    /// </summary>
    /// <remarks>
    ///     The control runs the action at each step of a scroll, and only with a report that is
    ///     different from the last one. A source starts with <see cref="ReportedRange.Empty" />. A view
    ///     model that makes its own rows calms <see cref="ReportedRange.Constructed" />, builds the rows
    ///     for it, and stops at its own count.
    /// </remarks>
    IBindableAction<ReportedRange> RangeReported { get; }

    /// <summary>
    ///     Gets the request of the view model to show a position, or none.
    /// </summary>
    /// <remarks>
    ///     The view model keeps the request until the control reports it. Thus, a request made before
    ///     the control is bound or laid out is not lost. Each request has a number of its own, thus a
    ///     second request for a position is a different value.
    /// </remarks>
    IOneWayBindableValue<Maybe<ScrollRequest>> ScrollRequest { get; }

    /// <summary>
    ///     Gets the action that the control runs with a request that it handled.
    /// </summary>
    /// <remarks>
    ///     The control runs the action one time for each request that it handles. The view model
    ///     clears the request only if the request is still that one.
    /// </remarks>
    IBindableAction<ScrollRequest> ScrollHandled { get; }
}
