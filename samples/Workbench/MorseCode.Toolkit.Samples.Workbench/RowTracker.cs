using SodaFlow;

namespace MorseCode.Toolkit.Samples.Workbench;

/// <summary>
///     The tracker of the row view models. Each count is a fold over the changes that rows send.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of IRowTracker does not say how the counts come from the changes.
internal sealed class RowTracker : IRowTracker
{
    private readonly StreamSink<int> changes;

    private RowTracker(StreamSink<int> changes, Cell<int> built, Cell<int> alive)
    {
        this.changes = changes;
        this.Built = built;
        this.Alive = alive;
    }

    /// <inheritdoc />
    public Cell<int> Built { get; }

    /// <inheritdoc />
    public Cell<int> Alive { get; }

    /// <summary>
    ///     Makes a tracker with no rows.
    /// </summary>
    /// <returns>The tracker.</returns>
    public static IRowTracker Create() =>
        Transaction.Run(
            f: static () =>
            {
                StreamSink<int> changes = Stream.CreateSink<int>();

                return new RowTracker(
                    changes: changes,
                    built: changes.Filter(predicate: static change => change > 0)
                        .Accum(initialState: 0, f: static (change, total) => total + change),
                    alive: changes.Accum(initialState: 0, f: static (change, total) => total + change));
            });

    /// <inheritdoc />
    public void RowBuilt() => this.Post(change: 1);

    /// <inheritdoc />
    public void RowReleased() => this.Post(change: -1);

    // A map builds a row inside a transaction, and a send is not legal there. Thus, the change goes
    // out when that transaction closes.
    private void Post(int change) => Transaction.Post(action: () => this.changes.Send(a: change));
}
