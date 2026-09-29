using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Mvvm.Tests;

public sealed class ActionDisposableTests
{
    [Test]
    public async Task NullActionFailsAtTheCall()
    {
        // ReSharper disable once NullableWarningSuppressionIsUsed - The null action is the input under test: FromAction must refuse it at run time.
        Exception caught = Catch(action: () => _ = Disposable.FromAction(onDispose: null!));

        await Assert.That(caught).IsTypeOf<ArgumentNullException>();
        await Assert.That(((ArgumentNullException)caught).ParamName).IsEqualTo(expected: "onDispose");
    }

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
