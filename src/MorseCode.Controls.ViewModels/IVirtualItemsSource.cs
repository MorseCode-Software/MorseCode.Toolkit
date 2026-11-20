using JetBrains.Annotations;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Controls.ViewModels;

/// <summary>
///     The values that a virtualizing control reads from a view model, and the actions that it runs
///     to report to it. The view model gives them to the control as one object, and the control
///     attaches to all of them.
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
///     <para>
///         <c>VirtualizingItemsControl</c> is the control of this toolkit, for Avalonia. A control of
///         a different package can use a source too. The remarks of each member tell what that
///         control must do. The control removes its handlers from the two values when it leaves the
///         visual tree. Thus, a view model that outlives its view does not keep the view. The
///         control reads the two values again when it goes back into the tree.
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
    ///     <para>
    ///         The count travels with the rows. Thus, the control applies a change of both in one
    ///         update, and it never shows a count with the rows of a different instant.
    ///     </para>
    ///     <para>
    ///         The control shows the row at index <c>p - Offset</c> of the items at position
    ///         <c>p</c>. It shows a placeholder at each position that the items do not cover. The rows
    ///         can answer an earlier report. Thus, they do not always cover the range of the last
    ///         report. The view model does not change a list after it gives it.
    ///     </para>
    /// </remarks>
    IOneWayBindableValue<VirtualRows> Rows { get; }

    /// <summary>
    ///     Gets the action that the control runs with its report. The report has the positions that
    ///     show, and the positions that the control wants rows for.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The control runs the action at each step of a scroll, and only with a report that is
    ///         different from the last one. The action does not compare the reports, thus the
    ///         control does. A source starts with <see cref="ReportedRange.Empty" />. Thus, with a new
    ///         source, the control compares with that value, and not with its last report.
    ///     </para>
    ///     <para>
    ///         Each range of the report stops at the count of <see cref="Rows" />. Thus, the control
    ///         reports again when the count changes, also if no container changes. Otherwise, the
    ///         row of a new position comes only at the next scroll. The control puts the positions
    ///         that it wants after the end of the list in <see cref="ReportedRange.Ahead" />.
    ///     </para>
    ///     <para>
    ///         The control does not report a smaller range only because it has no containers for a
    ///         short time, as after a change of the items. Otherwise, the view model removes rows
    ///         that show. The rows then change again, and the cycle does not stop. The control keeps
    ///         the last report until its containers come back. Only a list that is empty, or that
    ///         was empty, gets a new report then.
    ///     </para>
    ///     <para>
    ///         The view model builds the rows in the transaction of the report, on the thread that
    ///         runs the action. A view model that makes its own rows calms
    ///         <see cref="ReportedRange.Constructed" />, builds the rows for it, and stops at its own
    ///         count.
    ///     </para>
    /// </remarks>
    IBindableAction<ReportedRange> RangeReported { get; }

    /// <summary>
    ///     Gets the request of the view model to show a position, or none.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The view model keeps the request until the control reports it. Thus, a request made
    ///         before the control is bound or laid out is not lost. Each request has a number of its
    ///         own, thus a second request for a position is a different value.
    ///     </para>
    ///     <para>
    ///         The control scrolls one time for each value, and then runs
    ///         <see cref="ScrollHandled" /> with it. A position after the end of the list waits until
    ///         the count gets it. A negative position can never apply. Thus, the control reports it
    ///         and does not scroll.
    ///     </para>
    /// </remarks>
    IOneWayBindableValue<Maybe<ScrollRequest>> ScrollRequest { get; }

    /// <summary>
    ///     Gets the action that the control runs with a request that it handled.
    /// </summary>
    /// <remarks>
    ///     The control runs the action one time for each request that it handles. The view model
    ///     clears the request only if the request is still that one. The view model can clear or
    ///     change the request while the action runs. Thus, the control records that it handled the
    ///     request before it runs the action.
    /// </remarks>
    IBindableAction<ScrollRequest> ScrollHandled { get; }
}
