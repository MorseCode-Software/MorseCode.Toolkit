using System;

namespace MorseCode.StagedConstruction.Tests;

internal static class Catching
{
    public static Exception Catch(Action action)
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
