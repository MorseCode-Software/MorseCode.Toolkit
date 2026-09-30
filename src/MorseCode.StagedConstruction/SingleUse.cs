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
    //
    // usedBy holds the name of the member that used the handle. A handle can be used by more than one
    // member - Construct or Select - so the message names the member that used it, and not the member
    // that was called second.
    public static void Claim(ref string? usedBy, string member)
    {
        string? previous = Interlocked.CompareExchange(location1: ref usedBy, value: member, comparand: null);

        if (previous is not null)
        {
            throw new InvalidOperationException(
                message: $"{previous} was already called on this handle. A handle can be used one time only.");
        }
    }
}
