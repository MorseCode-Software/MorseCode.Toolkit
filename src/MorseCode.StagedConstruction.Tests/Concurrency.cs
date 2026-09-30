using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MorseCode.StagedConstruction.Tests;

internal static class Concurrency
{
    public const int Threads = 16;

    public const int Succeeded = 1;

    public const int Refused = 2;

    // Each thread starts, adds one to the count, and waits for the start signal. The test sends the
    // signal when the count is equal to the number of threads. Thus, the calls overlap and do not occur
    // in sequence.
    public static async Task<int[]> RunTogether(Action call)
    {
        int ready = 0;
        TaskCompletionSource<bool> start = new();

        Task<int>[] calls =
        [
            .. Enumerable
                .Range(start: 0, count: Threads)
                .Select(_ =>
                    Task.Factory.StartNew(
                        function: () =>
                        {
                            Interlocked.Increment(location: ref ready);
                            start.Task.Wait();

                            try
                            {
                                call();

                                return Succeeded;
                            }
                            catch (InvalidOperationException)
                            {
                                return Refused;
                            }
                        },
                        creationOptions: TaskCreationOptions.LongRunning))
        ];

        SpinWait.SpinUntil(condition: () => Volatile.Read(location: ref ready) == Threads);
        start.SetResult(result: true);

        return await Task.WhenAll(tasks: calls);
    }
}
