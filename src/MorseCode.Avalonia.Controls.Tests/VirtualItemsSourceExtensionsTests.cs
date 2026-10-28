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

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     Checks the sources that <c>CreateVirtualItemsSource</c> makes, without a control. Each test makes
///     its source inside a view model, as production code does, and disposes of the view model.
/// </summary>
public sealed class VirtualItemsSourceExtensionsTests
{
    [Test]
    public async Task TheCellsAndSinksOfTheViewModelAreTheValuesOfTheSource()
    {
        CellSink<int> count = Cell.CreateSink(initialValue: 10);
        VirtualRows rows = new(Offset: 2, Items: [new VirtualRow(Position: 2)]);
        CellSink<ItemRange> range = Cell.CreateSink(initialValue: ItemRange.Empty);
        StreamSink<int> requests = Stream.CreateSink<int>();

        (IVirtualItemsSource source, ViewModelBase viewModel) =
            Make(
                make: output =>
                    output.CreateVirtualItemsSource(
                        totalCount: count,
                        rows: Cell.Constant(rows),
                        visibleRange: range,
                        scrollRequests: requests));

        using ViewModelBase owner = viewModel;

        await Assert.That(source.TotalCount.Value).IsEqualTo(expected: 10);
        await Assert.That(source.Rows.Value).IsSameReferenceAs(expected: rows);

        count.Send(a: 12);
        source.VisibleRange.Value = new ItemRange(First: 3, Count: 4);

        await Assert.That(source.TotalCount.Value).IsEqualTo(expected: 12);
        await Assert.That(range.Sample()).IsEqualTo(expected: new ItemRange(First: 3, Count: 4));
    }

    [Test]
    public async Task ARequestReachesTheSourceAndTheReportOfTheControlClearsIt()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();

        (IVirtualItemsSource source, ViewModelBase viewModel) =
            Make(
                make: output =>
                    output.CreateVirtualItemsSource(
                        totalCount: Cell.Constant(1),
                        rows: Cell.Constant(VirtualRows.Empty),
                        visibleRange: Cell.CreateSink(initialValue: ItemRange.Empty),
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
                        totalCount: Cell.Constant(1),
                        rows: Cell.Constant(VirtualRows.Empty),
                        visibleRange: Cell.CreateSink(initialValue: ItemRange.Empty),
                        scrollRequests: requests,
                        clears: clears));

        using ViewModelBase owner = viewModel;

        requests.Send(a: 5);
        clears.Send(a: Unit.Value);

        await Assert.That(source.ScrollRequest.Value).IsEqualTo(expected: Maybe<int>.None);
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

        await Assert.That(source.TotalCount.Value).IsEqualTo(expected: 100);
        await Assert.That(source.Rows.Value.Items).IsEmpty();
        await Assert.That(built).IsEmpty();

        source.VisibleRange.Value = new ItemRange(First: 10, Count: 5);

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

        source.VisibleRange.Value = new ItemRange(First: 10, Count: 5);
        source.VisibleRange.Value = new ItemRange(First: 12, Count: 5);

        await Assert.That(built)
            .IsEquivalentTo(expected: [10, 11, 12, 13, 14, 15, 16], ordering: CollectionOrdering.Matching);
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

        source.VisibleRange.Value = new ItemRange(First: 10, Count: 3);

        await Assert.That(evicted).IsEmpty();

        viewModel.Dispose();

        await Assert.That(evicted.Order())
            .IsEquivalentTo(expected: [10, 11, 12], ordering: CollectionOrdering.Matching);
    }

    [Test]
    public async Task ANullArgumentIsRefusedAndNamed()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        ReactiveCollection<int, int, int> collection = Collection(total: 1);
        CellSink<ItemRange> range = Cell.CreateSink(initialValue: ItemRange.Empty);

        // The nulls are typed variables and not arguments with a null-forgiving operator, which the
        // named argument order check of MorseCode.Analyzers cannot place in the middle of a call.
        // ReSharper disable NullableWarningSuppressionIsUsed - The null is the input under test.
        Func<int, VirtualRow> noProject = null!;
        ReactiveCollection<int, int, int> noCollection = null!;
        Cell<int> noCount = null!;
        Cell<VirtualRows> noRows = null!;
        CellSink<ItemRange> noRange = null!;
        Stream<int> noRequests = null!;
        ViewModelBase.IOutput noOutput = null!;

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
                totalCount: noCount,
                rows: Cell.Constant(VirtualRows.Empty),
                visibleRange: range,
                scrollRequests: requests))),
            Catch(action: () => MakeAndRefuse(make: output => output.CreateVirtualItemsSource(
                totalCount: Cell.Constant(0),
                rows: noRows,
                visibleRange: range,
                scrollRequests: requests))),
            Catch(action: () => MakeAndRefuse(make: output => output.CreateVirtualItemsSource(
                totalCount: Cell.Constant(0),
                rows: Cell.Constant(VirtualRows.Empty),
                visibleRange: noRange,
                scrollRequests: requests)))
        ];

        await Assert.That(refused.Select(selector: static exception => (exception as ArgumentNullException)?.ParamName ?? string.Empty))
            .IsEquivalentTo(
                expected: ["output", "collection", "project", "scrollRequests", "totalCount", "rows", "visibleRange"],
                ordering: CollectionOrdering.Matching);
    }

    // The value that the construction returns carries the result and the view model out.
    private static (T Result, ViewModelBase ViewModel) Make<T>(Func<ViewModelBase.IOutput, T> make) =>
        ViewModelBase.CreateBase(
            bindingScheduler: BindingScheduler.Immediate,
            continuation: (output, construct) =>
                construct.Construct(constructor: viewModelBase => (make(output), viewModelBase)));

    // The exception of the make function leaves the construction, and the base releases its registrations.
    private static void MakeAndRefuse(Func<ViewModelBase.IOutput, IVirtualItemsSource> make) =>
        Make(make: make).ViewModel.Dispose();

    private static ReactiveCollection<int, int, int> Collection(int total) =>
        ReactiveCollection<int, int, int>.Create(
            keySelector: static identity => identity,
            initialItems:
            [
                .. Enumerable.Range(start: 0, count: total)
                    .Select(selector: static key => new Item<int, int>(identity: key, state: 0))
            ],
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
