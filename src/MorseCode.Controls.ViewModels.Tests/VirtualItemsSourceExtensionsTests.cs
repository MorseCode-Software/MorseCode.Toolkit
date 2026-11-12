using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MorseCode.Mvvm;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Collections;
using SodaFlow.Functional;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Controls.ViewModels.Tests;

/// <summary>
///     Checks the sources that <c>CreateVirtualItemsSource</c> makes, without a control. Each test makes
///     its source inside a view model, as production code does, and disposes of the view model.
/// </summary>
public sealed class VirtualItemsSourceExtensionsTests
{
    [Test]
    public async Task TheCellsAndSinksOfTheViewModelAreTheValuesOfTheSource()
    {
        CellSink<VirtualRows> rows =
            Cell.CreateSink(
                initialValue: new VirtualRows(TotalCount: 10, Offset: 2, Items: [new VirtualRow(Position: 2)]));

        CellSink<ReportedRange> range = Cell.CreateSink(initialValue: ReportedRange.Empty);
        StreamSink<int> requests = Stream.CreateSink<int>();

        (IVirtualItemsSource source, ViewModelBase viewModel) =
            Make(
                make: output =>
                    output.CreateVirtualItemsSource(rows: rows, reportedRange: range, scrollRequests: requests));

        using ViewModelBase owner = viewModel;

        await Assert.That(source.Rows.Value).IsSameReferenceAs(expected: rows.Sample());
        await Assert.That(source.Rows.Value.TotalCount).IsEqualTo(expected: 10);

        rows.Send(a: rows.Sample() with { TotalCount = 12 });
        source.Range.Value = Reported(first: 3, count: 4);

        await Assert.That(source.Rows.Value.TotalCount).IsEqualTo(expected: 12);
        await Assert.That(range.Sample()).IsEqualTo(expected: Reported(first: 3, count: 4));
    }

    [Test]
    public async Task ARequestReachesTheSourceAndTheReportOfTheControlClearsIt()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();

        (IVirtualItemsSource source, ViewModelBase viewModel) =
            Make(
                make: output =>
                    output.CreateVirtualItemsSource(
                        rows: Cell.Constant(VirtualRows.Empty),
                        reportedRange: Cell.CreateSink(initialValue: ReportedRange.Empty),
                        scrollRequests: requests));

        using ViewModelBase owner = viewModel;

        await Assert.That(source.ScrollRequest.Value).IsEqualTo(expected: Maybe<int>.None);

        requests.Send(a: 5);

        await Assert.That(source.ScrollRequest.Value).IsEqualTo(expected: Maybe<int>.Some(value: 5));

        source.ScrollHandled.Value = Maybe<int>.Some(value: 5);

        await Assert.That(source.ScrollRequest.Value).IsEqualTo(expected: Maybe<int>.None);
    }

    [Test]
    public async Task AClearOfTheViewModelWithdrawsARequest()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        StreamSink<Unit> clears = Stream.CreateSink<Unit>();

        (IVirtualItemsSource source, ViewModelBase viewModel) =
            Make(
                make: output =>
                    output.CreateVirtualItemsSource(
                        rows: Cell.Constant(VirtualRows.Empty),
                        reportedRange: Cell.CreateSink(initialValue: ReportedRange.Empty),
                        scrollRequests: requests,
                        clears: clears));

        using ViewModelBase owner = viewModel;

        requests.Send(a: 5);
        clears.Send(a: Unit.Value);

        await Assert.That(source.ScrollRequest.Value).IsEqualTo(expected: Maybe<int>.None);
    }

    // A list that has all of its rows. The count is the count of the list, and the rows start at position zero.
    // The list goes into the rows without a copy. A report from the control changes none of them.
    [Test]
    public async Task AWholeListGivesItsRowsFromPositionZeroWithItsCount()
    {
        IReadOnlyList<VirtualRow> first =
            [new(Position: 0), new(Position: 1), new(Position: 2)];

        CellSink<IReadOnlyList<VirtualRow>> rows = Cell.CreateSink(initialValue: first);

        (IVirtualItemsSource source, ViewModelBase viewModel) =
            Make(make: output => output.CreateVirtualItemsSource(rows: rows));

        using ViewModelBase owner = viewModel;

        await Assert.That(source.Rows.Value.TotalCount).IsEqualTo(expected: 3);
        await Assert.That(source.Rows.Value.Offset).IsEqualTo(expected: 0);
        await Assert.That(source.Rows.Value.Items).IsSameReferenceAs(expected: first);

        IReadOnlyList<VirtualRow> second =
        [
            .. Enumerable.Range(start: 0, count: 5)
                .Select(selector: static position => new VirtualRow(Position: position))
        ];

        rows.Send(a: second);
        source.Range.Value = Reported(first: 1, count: 2);

        await Assert.That(source.Rows.Value.TotalCount).IsEqualTo(expected: 5);
        await Assert.That(source.Rows.Value.Offset).IsEqualTo(expected: 0);
        await Assert.That(source.Rows.Value.Items).IsSameReferenceAs(expected: second);
    }

    // The stream of scroll positions is optional for a list that has all of its rows. Without one, the source
    // never asks for a scroll.
    [Test]
    public async Task AWholeListAsksForAScrollOnlyWithAStreamOfPositions()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        Cell<IReadOnlyList<VirtualRow>> rows = Cell.Constant<IReadOnlyList<VirtualRow>>([new VirtualRow(Position: 0)]);

        (IVirtualItemsSource withRequests, ViewModelBase first) =
            Make(make: output => output.CreateVirtualItemsSource(rows: rows, scrollRequests: requests));

        (IVirtualItemsSource withoutRequests, ViewModelBase second) =
            Make(make: output => output.CreateVirtualItemsSource(rows: rows));

        using ViewModelBase firstOwner = first;
        using ViewModelBase secondOwner = second;

        requests.Send(a: 0);

        await Assert.That(withRequests.ScrollRequest.Value).IsEqualTo(expected: Maybe<int>.Some(value: 0));
        await Assert.That(withoutRequests.ScrollRequest.Value).IsEqualTo(expected: Maybe<int>.None);
    }

    // The source of a list that has all of its rows has the type of the rows. The view model reads the
    // report of the control, and the rows of the list at the positions of the report.
    [Test]
    public async Task AWholeListSourceGivesTheViewModelTheReportAndItsRows()
    {
        IReadOnlyList<VirtualRow> rows = Rows(count: 100);

        (IVirtualItemsSource<VirtualRow> source, ViewModelBase viewModel) =
            Make(make: output => output.CreateVirtualItemsSource(rows: Cell.Constant(rows)));

        using ViewModelBase owner = viewModel;

        await Assert.That(source.ConstructedRows.Sample()).IsEmpty();
        await Assert.That(source.VisibleRows.Sample()).IsEmpty();

        ReportedRange reported =
            new(
                Visible: new ItemRange(First: 10, Count: 5),
                Constructed: new ItemRange(First: 5, Count: 15),
                FirstVisibilityAmount: 0.25,
                LastVisibilityAmount: 0.75);

        source.Range.Value = reported;

        await Assert.That(source.Reported.Sample()).IsEqualTo(expected: reported);
        await Assert.That(source.FirstVisibilityAmount.Sample()).IsEqualTo(expected: 0.25);
        await Assert.That(source.LastVisibilityAmount.Sample()).IsEqualTo(expected: 0.75);

        // The control has each row. The constructed rows are only the rows at the positions of the report.
        await Assert.That(source.Rows.Value.Items).IsSameReferenceAs(expected: rows);

        await Assert.That(source.ConstructedRows.Sample().Select(selector: static row => row.Position))
            .IsEquivalentTo(expected: Enumerable.Range(start: 5, count: 15), ordering: CollectionOrdering.Matching);

        await Assert.That(source.VisibleRows.Sample().Select(selector: static row => row.Position))
            .IsEquivalentTo(expected: [10, 11, 12, 13, 14], ordering: CollectionOrdering.Matching);

        // The rows are the objects of the list, and not copies.
        await Assert.That(source.ConstructedRows.Sample()[index: 0]).IsSameReferenceAs(expected: rows[index: 5]);
        await Assert.That(source.VisibleRows.Sample()[index: 0]).IsSameReferenceAs(expected: rows[index: 10]);
    }

    // The control reports at each step of a scroll, and the list can change at a position that the report
    // does not have. The rows of the report change only when a different row is at one of its positions.
    // The cell of an amount changes only when the amount is a different number.
    [Test]
    public async Task TheRowsOfAWholeListChangeOnlyWhenADifferentRowIsAtAPositionOfTheReport()
    {
        CellSink<IReadOnlyList<VirtualRow>> rows = Cell.CreateSink(initialValue: Rows(count: 100));

        (IVirtualItemsSource<VirtualRow> source, ViewModelBase viewModel) =
            Make(make: output => output.CreateVirtualItemsSource(rows: rows));

        using ViewModelBase owner = viewModel;

        ItemRange visible = new(First: 10, Count: 5);
        ItemRange constructed = new(First: 5, Count: 15);
        source.Range.Value = Reported(visible: visible, constructed: constructed);

        List<IReadOnlyList<VirtualRow>> constructedValues = [];
        List<IReadOnlyList<VirtualRow>> visibleValues = [];
        List<double> amounts = [];
        List<double> lastAmounts = [];
        IListener constructedListener = source.ConstructedRows.Updates().Listen(handler: constructedValues.Add);
        IListener visibleListener = source.VisibleRows.Updates().Listen(handler: visibleValues.Add);
        IListener amountListener = source.FirstVisibilityAmount.Updates().Listen(handler: amounts.Add);
        IListener lastAmountListener = source.LastVisibilityAmount.Updates().Listen(handler: lastAmounts.Add);

        // A different amount of the first row shows. The cell of the amount changes, and the rows do not.
        source.Range.Value =
            Reported(visible: visible, constructed: constructed) with { FirstVisibilityAmount = 0.5 };

        // A new list, with a different row at a position that the report does not have.
        rows.Send(a: WithANewRowAt(rows: rows.Sample(), position: 50));

        await Assert.That(constructedValues).IsEmpty();
        await Assert.That(visibleValues).IsEmpty();

        // The control wants rows for more positions, and the rows that show stay. The amount is the same
        // number.
        source.Range.Value =
            Reported(visible: visible, constructed: new ItemRange(First: 4, Count: 17)) with
            {
                FirstVisibilityAmount = 0.5
            };

        await Assert.That(constructedValues.Count).IsEqualTo(expected: 1);
        await Assert.That(visibleValues).IsEmpty();
        await Assert.That(amounts).IsEquivalentTo(expected: [0.5], ordering: CollectionOrdering.Matching);
        await Assert.That(lastAmounts).IsEmpty();

        // A new list, with a different row at a position that shows. A row is a record, and the new row
        // is equal to the row that it replaces. Thus, this fails if the cells compare the rows as values.
        rows.Send(a: WithANewRowAt(rows: rows.Sample(), position: 12));

        constructedListener.Unlisten();
        visibleListener.Unlisten();
        amountListener.Unlisten();
        lastAmountListener.Unlisten();

        await Assert.That(constructedValues.Count).IsEqualTo(expected: 2);
        await Assert.That(visibleValues.Count).IsEqualTo(expected: 1);
        await Assert.That(visibleValues[0][index: 2]).IsSameReferenceAs(expected: rows.Sample()[index: 12]);
        await Assert.That(constructedValues[1][index: 8]).IsSameReferenceAs(expected: rows.Sample()[index: 12]);
    }

    // The report of the control and the list can be from different instants. A report of positions that
    // the list does not have gives the rows that there are.
    [Test]
    public async Task TheRowsOfAWholeListStopAtTheEndOfTheList()
    {
        CellSink<IReadOnlyList<VirtualRow>> rows = Cell.CreateSink(initialValue: Rows(count: 12));

        (IVirtualItemsSource<VirtualRow> source, ViewModelBase viewModel) =
            Make(make: output => output.CreateVirtualItemsSource(rows: rows));

        using ViewModelBase owner = viewModel;

        source.Range.Value =
            Reported(visible: new ItemRange(First: 10, Count: 5), constructed: new ItemRange(First: 5, Count: 15));

        await Assert.That(source.ConstructedRows.Sample().Select(selector: static row => row.Position))
            .IsEquivalentTo(expected: [5, 6, 7, 8, 9, 10, 11], ordering: CollectionOrdering.Matching);

        await Assert.That(source.VisibleRows.Sample().Select(selector: static row => row.Position))
            .IsEquivalentTo(expected: [10, 11], ordering: CollectionOrdering.Matching);

        // The list loses rows at its end before the control reports again. The rows that stay are the
        // same objects. Thus, the new rows of the report are the first rows of the old ones, and only
        // the count is different.
        rows.Send(a: [.. rows.Sample().Take(count: 7)]);

        await Assert.That(source.ConstructedRows.Sample().Select(selector: static row => row.Position))
            .IsEquivalentTo(expected: [5, 6], ordering: CollectionOrdering.Matching);

        await Assert.That(source.VisibleRows.Sample()).IsEmpty();
    }

    [Test]
    public async Task ACollectionGivesItsCountAndOnlyTheRowsOfTheRangeAtTheirOffset()
    {
        List<int> built = [];

        (IVirtualItemsSource source, ViewModelBase viewModel) =
            Make(
                make: output =>
                    output.CreateVirtualItemsSource(
                        collection: Collection(total: 100),
                        project: key =>
                        {
                            built.Add(item: key);

                            return new VirtualRow(Position: key);
                        },
                        scrollRequests: Stream.CreateSink<int>()));

        using ViewModelBase owner = viewModel;

        await Assert.That(source.Rows.Value.TotalCount).IsEqualTo(expected: 100);
        await Assert.That(source.Rows.Value.Items).IsEmpty();
        await Assert.That(built).IsEmpty();

        source.Range.Value = Reported(first: 10, count: 5);

        await Assert.That(source.Rows.Value.Offset).IsEqualTo(expected: 10);

        await Assert.That(source.Rows.Value.Items.OfType<VirtualRow>().Select(selector: static row => row.Position))
            .IsEquivalentTo(expected: [10, 11, 12, 13, 14], ordering: CollectionOrdering.Matching);

        await Assert.That(built)
            .IsEquivalentTo(expected: [10, 11, 12, 13, 14], ordering: CollectionOrdering.Matching);
    }

    [Test]
    public async Task ARowThatStaysInTheViewIsNotBuiltAgain()
    {
        List<int> built = [];

        (IVirtualItemsSource source, ViewModelBase viewModel) =
            Make(
                make: output =>
                    output.CreateVirtualItemsSource(
                        collection: Collection(total: 100),
                        project: key =>
                        {
                            built.Add(item: key);

                            return new VirtualRow(Position: key);
                        },
                        scrollRequests: Stream.CreateSink<int>()));

        using ViewModelBase owner = viewModel;

        source.Range.Value = Reported(first: 10, count: 5);
        source.Range.Value = Reported(first: 12, count: 5);

        await Assert.That(built)
            .IsEquivalentTo(expected: [10, 11, 12, 13, 14, 15, 16], ordering: CollectionOrdering.Matching);
    }

    // The position of a key comes from the collection at the instant of the request. The keys here are not
    // the positions, thus the test can tell the two apart.
    [Test]
    public async Task ARequestByKeyBecomesARequestForThePositionOfTheKey()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();

        (IVirtualItemsSource source, ViewModelBase viewModel) =
            Make(
                make: output =>
                    output.CreateVirtualItemsSourceByKey(
                        collection: Collection(keys: [10, 20, 30, 40]),
                        project: static key => new VirtualRow(Position: key),
                        scrollRequests: requests));

        using ViewModelBase owner = viewModel;

        requests.Send(a: 30);

        await Assert.That(source.ScrollRequest.Value).IsEqualTo(expected: Maybe<int>.Some(value: 2));

        source.ScrollHandled.Value = Maybe<int>.Some(value: 2);
        requests.Send(a: 10);

        await Assert.That(source.ScrollRequest.Value).IsEqualTo(expected: Maybe<int>.Some(value: 0));
    }

    [Test]
    public async Task ARequestByAKeyThatTheCollectionDoesNotHaveMakesNoRequest()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();

        (IVirtualItemsSource source, ViewModelBase viewModel) =
            Make(
                make: output =>
                    output.CreateVirtualItemsSourceByKey(
                        collection: Collection(keys: [10, 20, 30]),
                        project: static key => new VirtualRow(Position: key),
                        scrollRequests: requests));

        using ViewModelBase owner = viewModel;

        requests.Send(a: 25);

        await Assert.That(source.ScrollRequest.Value).IsEqualTo(expected: Maybe<int>.None);
    }

    [Test]
    public async Task ANullArgumentOfTheMethodByKeyIsRefusedAndNamed()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        ReactiveCollection<int, int, int> collection = Collection(total: 1);

        // ReSharper disable NullableWarningSuppressionIsUsed - The null is the input under test.
        Func<int, VirtualRow> noProject = null!;
        ReactiveCollection<int, int, int> noCollection = null!;
        Stream<int> noRequests = null!;
        ViewModelBase.IOutput noOutput = null!;

        // ReSharper restore NullableWarningSuppressionIsUsed

        Exception[] refused =
        [
            Catch(
                action: () => noOutput.CreateVirtualItemsSourceByKey(
                    collection: collection,
                    project: static key => new VirtualRow(Position: key),
                    scrollRequests: requests)),
            Catch(action: () => MakeAndRefuse(make: output => output.CreateVirtualItemsSourceByKey(
                collection: noCollection,
                project: static key => new VirtualRow(Position: key),
                scrollRequests: requests))),
            Catch(action: () => MakeAndRefuse(make: output => output.CreateVirtualItemsSourceByKey(
                collection: collection,
                project: noProject,
                scrollRequests: requests))),
            Catch(action: () => MakeAndRefuse(make: output => output.CreateVirtualItemsSourceByKey(
                collection: collection,
                project: static key => new VirtualRow(Position: key),
                scrollRequests: noRequests)))
        ];

        await Assert.That(refused.Select(selector: static exception => (exception as ArgumentNullException)?.ParamName ?? string.Empty))
            .IsEquivalentTo(
                expected: ["output", "collection", "project", "scrollRequests"],
                ordering: CollectionOrdering.Matching);
    }

    // The caller does not dispose of the rows. The output of the view model does it.
    [Test]
    public async Task DisposingOfTheViewModelReleasesTheRowsThatTheSourceHolds()
    {
        List<int> evicted = [];

        (IVirtualItemsSource source, ViewModelBase viewModel) =
            Make(
                make: output =>
                    output.CreateVirtualItemsSource(
                        collection: Collection(total: 100),
                        project: static key => new VirtualRow(Position: key),
                        scrollRequests: Stream.CreateSink<int>(),
                        onEvicted: row => evicted.Add(item: row.Position)));

        source.Range.Value = Reported(first: 10, count: 3);

        await Assert.That(evicted).IsEmpty();

        viewModel.Dispose();

        await Assert.That(evicted.OrderBy(keySelector: static position => position))
            .IsEquivalentTo(expected: [10, 11, 12], ordering: CollectionOrdering.Matching);
    }

    // The control wants rows for positions after the end of the list. The slice has those positions,
    // thus the row of a key that the collection gets there comes in the value that has the new count.
    [Test]
    public async Task ARowAtAPositionThatTheControlWantsAfterTheEndComesWithTheCount()
    {
        StreamSink<CollectionEdit<int, int, int>> edits = Stream.CreateSink<CollectionEdit<int, int, int>>();

        ReactiveCollection<int, int, int> collection =
            ReactiveCollection<int, int, int>.Create(
                keySelector: static identity => identity,
                initialItems:
                [
                    .. Enumerable.Range(start: 0, count: 5)
                        .Select(selector: static key => new Item<int, int>(identity: key, state: 0))
                ],
                edits);

        (IVirtualItemsSource source, ViewModelBase viewModel) =
            Make(
                make: output =>
                    output.CreateVirtualItemsSource(
                        collection: collection,
                        project: static key => new VirtualRow(Position: key),
                        scrollRequests: Stream.CreateSink<int>()));

        using ViewModelBase owner = viewModel;

        source.Range.Value = Reported(first: 0, count: 5) with { Ahead = 2 };

        List<VirtualRows> values = [];
        IListener listener = source.Rows.Cell.Updates().Listen(handler: values.Add);

        edits.Send(a: CollectionEdit<int, int, int>.Add(new Item<int, int>(identity: 5, state: 0)));
        listener.Unlisten();

        await Assert.That(values.Count).IsEqualTo(expected: 1);
        await Assert.That(values[0].TotalCount).IsEqualTo(expected: 6);

        await Assert.That(values[0].Items.OfType<VirtualRow>().Select(selector: static row => row.Position))
            .IsEquivalentTo(expected: [0, 1, 2, 3, 4, 5], ordering: CollectionOrdering.Matching);
    }

    // The control also reports when only the positions that show change. The slice is the same then.
    [Test]
    public async Task AReportThatChangesOnlyThePositionsThatShowBringsNoRows()
    {
        (IVirtualItemsSource source, ViewModelBase viewModel) =
            Make(
                make: static output =>
                    output.CreateVirtualItemsSource(
                        collection: Collection(total: 100),
                        project: static key => new VirtualRow(Position: key),
                        scrollRequests: Stream.CreateSink<int>()));

        using ViewModelBase owner = viewModel;

        ItemRange constructed = new(First: 5, Count: 15);
        source.Range.Value = Reported(visible: new ItemRange(First: 10, Count: 5), constructed: constructed);

        List<VirtualRows> values = [];
        IListener listener = source.Rows.Cell.Updates().Listen(handler: values.Add);

        // A different row shows, and then a different amount of a row.
        ReportedRange next = Reported(visible: new ItemRange(First: 11, Count: 5), constructed: constructed);
        source.Range.Value = next;
        source.Range.Value = next with { FirstVisibilityAmount = 0.5 };
        listener.Unlisten();

        await Assert.That(values).IsEmpty();
    }

    // The source of a collection has the type of the rows. The view model reads the report of the
    // control, the rows that the control wants, and the rows that show.
    [Test]
    public async Task ACollectionSourceGivesTheViewModelTheReportAndItsRows()
    {
        (IVirtualItemsSource<VirtualRow> source, ViewModelBase viewModel) =
            Make(
                make: static output =>
                    output.CreateVirtualItemsSource(
                        collection: Collection(total: 100),
                        project: static key => new VirtualRow(Position: key),
                        scrollRequests: Stream.CreateSink<int>()));

        (IVirtualItemsSource<VirtualRow> keySource, ViewModelBase keyViewModel) =
            Make(
                make: static output =>
                    output.CreateVirtualItemsSourceByKey(
                        collection: Collection(total: 100),
                        project: static key => new VirtualRow(Position: key),
                        scrollRequests: Stream.CreateSink<int>()));

        using ViewModelBase owner = viewModel;
        using ViewModelBase keyOwner = keyViewModel;

        ReportedRange reported =
            new(
                Visible: new ItemRange(First: 10, Count: 5),
                Constructed: new ItemRange(First: 5, Count: 15),
                FirstVisibilityAmount: 0.25,
                LastVisibilityAmount: 0.75);

        source.Range.Value = reported;
        keySource.Range.Value = reported;

        await Assert.That(source.Reported.Sample()).IsEqualTo(expected: reported);
        await Assert.That(keySource.Reported.Sample()).IsEqualTo(expected: reported);
        await Assert.That(source.FirstVisibilityAmount.Sample()).IsEqualTo(expected: 0.25);
        await Assert.That(source.LastVisibilityAmount.Sample()).IsEqualTo(expected: 0.75);
        await Assert.That(keySource.FirstVisibilityAmount.Sample()).IsEqualTo(expected: 0.25);
        await Assert.That(keySource.LastVisibilityAmount.Sample()).IsEqualTo(expected: 0.75);

        // The constructed rows are the list that the control gets, and not a copy.
        await Assert.That(source.ConstructedRows.Sample()).IsSameReferenceAs(expected: source.Rows.Value.Items);

        await Assert.That(source.ConstructedRows.Sample().Select(selector: static row => row.Position))
            .IsEquivalentTo(expected: Enumerable.Range(start: 5, count: 15), ordering: CollectionOrdering.Matching);

        await Assert.That(source.VisibleRows.Sample().Select(selector: static row => row.Position))
            .IsEquivalentTo(expected: [10, 11, 12, 13, 14], ordering: CollectionOrdering.Matching);

        await Assert.That(keySource.VisibleRows.Sample().Select(selector: static row => row.Position))
            .IsEquivalentTo(expected: [10, 11, 12, 13, 14], ordering: CollectionOrdering.Matching);
    }

    // The control reports at each step of a scroll. The rows that show change only when a different
    // row shows.
    [Test]
    public async Task TheRowsThatShowChangeOnlyWhenADifferentRowShows()
    {
        (IVirtualItemsSource<VirtualRow> source, ViewModelBase viewModel) =
            Make(
                make: static output =>
                    output.CreateVirtualItemsSource(
                        collection: Collection(total: 100),
                        project: static key => new VirtualRow(Position: key),
                        scrollRequests: Stream.CreateSink<int>()));

        using ViewModelBase owner = viewModel;

        ItemRange visible = new(First: 10, Count: 5);
        source.Range.Value = Reported(visible: visible, constructed: new ItemRange(First: 5, Count: 15));

        List<IReadOnlyList<VirtualRow>> values = [];
        List<double> amounts = [];
        List<double> lastAmounts = [];
        IListener listener = source.VisibleRows.Updates().Listen(handler: values.Add);
        IListener amountListener = source.FirstVisibilityAmount.Updates().Listen(handler: amounts.Add);
        IListener lastAmountListener = source.LastVisibilityAmount.Updates().Listen(handler: lastAmounts.Add);

        // A different amount of the first row shows. The cell of the amount changes, and the rows do not.
        source.Range.Value =
            Reported(visible: visible, constructed: new ItemRange(First: 5, Count: 15)) with
            {
                FirstVisibilityAmount = 0.5
            };

        // The slice moves, and the rows that show stay. The amount is the same number.
        source.Range.Value =
            Reported(visible: visible, constructed: new ItemRange(First: 4, Count: 17)) with
            {
                FirstVisibilityAmount = 0.5
            };

        await Assert.That(values).IsEmpty();
        await Assert.That(amounts).IsEquivalentTo(expected: [0.5], ordering: CollectionOrdering.Matching);
        await Assert.That(lastAmounts).IsEmpty();

        // A different row shows.
        source.Range.Value =
            Reported(visible: new ItemRange(First: 11, Count: 5), constructed: new ItemRange(First: 4, Count: 17));

        listener.Unlisten();
        amountListener.Unlisten();
        lastAmountListener.Unlisten();

        await Assert.That(values.Count).IsEqualTo(expected: 1);

        await Assert.That(values[0].Select(selector: static row => row.Position))
            .IsEquivalentTo(expected: [11, 12, 13, 14, 15], ordering: CollectionOrdering.Matching);
    }

    // The report of the control and the rows of the slice can be from different instants. A report of
    // positions that the rows do not have gives the rows that there are.
    [Test]
    public async Task TheRowsThatShowStopAtTheRowsThatTheSourceHas()
    {
        (IVirtualItemsSource<VirtualRow> source, ViewModelBase viewModel) =
            Make(
                make: static output =>
                    output.CreateVirtualItemsSource(
                        collection: Collection(total: 12),
                        project: static key => new VirtualRow(Position: key),
                        scrollRequests: Stream.CreateSink<int>()));

        using ViewModelBase owner = viewModel;

        source.Range.Value =
            Reported(visible: new ItemRange(First: 10, Count: 5), constructed: new ItemRange(First: 5, Count: 15));

        await Assert.That(source.VisibleRows.Sample().Select(selector: static row => row.Position))
            .IsEquivalentTo(expected: [10, 11], ordering: CollectionOrdering.Matching);

        // A view model can make a report in a test, and the positions that show can start before the
        // rows. Positions 3 and 4 have no row then.
        source.Range.Value =
            Reported(visible: new ItemRange(First: 3, Count: 5), constructed: new ItemRange(First: 5, Count: 7));

        await Assert.That(source.VisibleRows.Sample().Select(selector: static row => row.Position))
            .IsEquivalentTo(expected: [5, 6, 7], ordering: CollectionOrdering.Matching);
    }

    [Test]
    public async Task ANullArgumentIsRefusedAndNamed()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        ReactiveCollection<int, int, int> collection = Collection(total: 1);
        CellSink<ReportedRange> range = Cell.CreateSink(initialValue: ReportedRange.Empty);
        Cell<IReadOnlyList<VirtualRow>> wholeList = Cell.Constant<IReadOnlyList<VirtualRow>>([]);

        // The nulls are typed variables and not arguments with a null-forgiving operator, which the
        // named argument order check of MorseCode.Analyzers cannot place in the middle of a call.
        // ReSharper disable NullableWarningSuppressionIsUsed - The null is the input under test.
        Func<int, VirtualRow> noProject = null!;
        ReactiveCollection<int, int, int> noCollection = null!;
        Cell<VirtualRows> noRows = null!;
        CellSink<ReportedRange> noRange = null!;
        Stream<int> noRequests = null!;
        ViewModelBase.IOutput noOutput = null!;
        Cell<IReadOnlyList<VirtualRow>> noWholeList = null!;

        // ReSharper restore NullableWarningSuppressionIsUsed

        // Each call has one null, which is the input under test: the method must refuse it at run time.
        // The output closes when the view model is built, so the calls that need a live one run inside Make.
        Exception[] refused =
        [
            Catch(
                action: () => noOutput.CreateVirtualItemsSource(
                    collection: collection,
                    project: static key => new VirtualRow(Position: key),
                    scrollRequests: requests)),
            Catch(action: () => MakeAndRefuse(make: output => output.CreateVirtualItemsSource(
                collection: noCollection,
                project: static key => new VirtualRow(Position: key),
                scrollRequests: requests))),
            Catch(action: () => MakeAndRefuse(make: output => output.CreateVirtualItemsSource(
                collection: collection,
                project: noProject,
                scrollRequests: requests))),
            Catch(action: () => MakeAndRefuse(make: output => output.CreateVirtualItemsSource(
                collection: collection,
                project: static key => new VirtualRow(Position: key),
                scrollRequests: noRequests))),
            Catch(action: () => MakeAndRefuse(make: output => output.CreateVirtualItemsSource(
                rows: noRows,
                reportedRange: range,
                scrollRequests: requests))),
            Catch(action: () => MakeAndRefuse(make: output => output.CreateVirtualItemsSource(
                rows: Cell.Constant(VirtualRows.Empty),
                reportedRange: noRange,
                scrollRequests: requests))),
            Catch(action: () => noOutput.CreateVirtualItemsSource(rows: wholeList)),
            Catch(action: () => MakeAndRefuse(make: output => output.CreateVirtualItemsSource(rows: noWholeList)))
        ];

        await Assert.That(refused.Select(selector: static exception => (exception as ArgumentNullException)?.ParamName ?? string.Empty))
            .IsEquivalentTo(
                expected:
                [
                    "output", "collection", "project", "scrollRequests", "rows", "reportedRange", "output", "rows"
                ],
                ordering: CollectionOrdering.Matching);
    }

    // A report of a control with no positions on each side: it wants rows for the positions that show.
    private static ReportedRange Reported(int first, int count) =>
        Reported(
            visible: new ItemRange(First: first, Count: count),
            constructed: new ItemRange(First: first, Count: count));

    // A report in which each row that shows is there in full.
    private static ReportedRange Reported(ItemRange visible, ItemRange constructed) =>
        new(Visible: visible, Constructed: constructed, FirstVisibilityAmount: 1.0, LastVisibilityAmount: 1.0);

    // The value that the construction returns carries the result and the view model out.
    private static (T Result, ViewModelBase ViewModel) Make<T>(Func<ViewModelBase.IOutput, T> make) =>
        ViewModelBase.CreateBase(
            bindingScheduler: BindingScheduler.Immediate,
            continuation: (output, construct) =>
                construct.Construct(constructor: viewModelBase => (make(output), viewModelBase)));

    // The exception of the make function leaves the construction, and the base releases its registrations.
    private static void MakeAndRefuse(Func<ViewModelBase.IOutput, IVirtualItemsSource> make) =>
        Make(make: make).ViewModel.Dispose();

    // A list that has all of its rows. The row of a position has that position.
    private static IReadOnlyList<VirtualRow> Rows(int count) =>
    [
        .. Enumerable.Range(start: 0, count: count)
            .Select(selector: static position => new VirtualRow(Position: position))
    ];

    // The same rows in a new list, with a new object for the row of one position.
    private static IReadOnlyList<VirtualRow> WithANewRowAt(IEnumerable<VirtualRow> rows, int position) =>
        [.. rows.Select(selector: row => row.Position == position ? new VirtualRow(Position: position) : row)];

    private static ReactiveCollection<int, int, int> Collection(int total) =>
        Collection(keys: Enumerable.Range(start: 0, count: total));

    private static ReactiveCollection<int, int, int> Collection(IEnumerable<int> keys) =>
        ReactiveCollection<int, int, int>.Create(
            keySelector: static identity => identity,
            initialItems: [.. keys.Select(selector: static key => new Item<int, int>(identity: key, state: 0))],
            Stream.CreateSink<CollectionEdit<int, int, int>>());

    private static Exception Catch(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new InvalidOperationException(message: "The action did not throw an exception.");
    }
}
