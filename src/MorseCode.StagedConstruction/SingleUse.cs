using System;
using System.Threading;

namespace MorseCode.StagedConstruction;

/// <summary>
///     Makes sure that a caller uses a handle one time only.
/// </summary>
internal static class SingleUse
{
    // The claim occurs before the handle calls the code of the caller. Thus, a call that fails also uses
    // the handle, and a second call cannot run a constructor or a stage again.
    public static void Claim(ref int used, string member)
    {
        if (Interlocked.Exchange(location1: ref used, value: 1) != 0)
        {
            throw new InvalidOperationException(
                message: $"{member} was already called on this handle. A handle can be used one time only.");
        }
    }
}
