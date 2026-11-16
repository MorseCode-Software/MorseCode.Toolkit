using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using MorseCode.Mvvm;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     Checks the scroll protocol of a <see cref="VirtualizingItemsControl" /> when the binding thread
///     is behind the graph. The source is the one that <c>CreateVirtualItemsSource</c> makes, with
///     a binding scheduler that holds its posts until the test runs them.
/// </summary>
public sealed class VirtualizingItemsControlLateBindingThreadTests
{
    // The report of the first request clears the request in the graph. The second request for the same
    // position comes before the binding thread ran that clear. A binding thread that keeps only the
    // last value of the two then shows the control a request, and never none. The number of the
    // request makes it a different value, thus the control scrolls again.
    [Test]
    public Task ARequestMadeAgainBeforeTheBindingThreadRunsScrollsAgain() =>
        TestApplication.Run(
            test: static async () =>
            {
                HeldScheduler scheduler = new();
                StreamSink<int> requests = Stream.CreateSink<int>();

                Cell<IReadOnlyList<VirtualRow>> rows =
                    Cell.Constant<IReadOnlyList<VirtualRow>>(
                    [
                        .. Enumerable.Range(start: 0, count: 1000)
                            .Select(selector: static position => new VirtualRow(Position: position))
                    ]);

                (ViewModelBase viewModel, IVirtualItemsSource<VirtualRow> source) =
                    ViewModelBase.CreateBase(
                        bindingScheduler: scheduler,
                        continuation: (output, construct) =>
                        {
                            IVirtualItemsSource<VirtualRow> made =
                                output.CreateVirtualItemsSource(rows: rows, scrollRequests: requests);

                            return construct.Construct(constructor: viewModelBase => (viewModelBase, made));
                        });

                using ViewModelBase owner = viewModel;
                using ShownVirtual shown = new(model: source, buffer: 0);

                Drain(scheduler: scheduler, shown: shown);

                requests.Send(a: 700);
                scheduler.Run();
                shown.Settle();

                await Assert.That(shown.Shown().Any(predicate: static pair => pair.Text == "Row 700")).IsTrue();
                await Assert.That(source.ScrollRequest.Cell.Sample()).IsEqualTo(expected: Maybe<ScrollRequest>.None);

                // The user scrolls away. The scheduler holds the clear of the request.
                shown.Scroller.Offset = new Vector(x: 0, y: 0);
                shown.Settle();

                await Assert.That(shown.Shown()[0].Text).IsEqualTo(expected: "Row 0");

                requests.Send(a: 700);
                Drain(scheduler: scheduler, shown: shown);

                await Assert.That(shown.Shown().Any(predicate: static pair => pair.Text == "Row 700")).IsTrue();
                await Assert.That(source.ScrollRequest.Cell.Sample()).IsEqualTo(expected: Maybe<ScrollRequest>.None);
            });

    // A post of the scheduler can make work for the dispatcher, and that work can post again.
    private static void Drain(HeldScheduler scheduler, ShownVirtual shown)
    {
        for (int pass = 0; pass < 6; pass++)
        {
            scheduler.Run();
            shown.Settle();
        }
    }

    // ReSharper disable once InheritdocConsiderUsage - The summary of IBindingScheduler does not say that this one holds its posts.
    private sealed class HeldScheduler : IBindingScheduler
    {
        private readonly Queue<Action> held = new();

        /// <inheritdoc />
        public bool CheckAccess() => true;

        /// <inheritdoc />
        public void Post(Action action) => this.held.Enqueue(item: action);

        public void Run()
        {
            while (this.held.Count > 0)
            {
                this.held.Dequeue()();
            }
        }
    }
}
