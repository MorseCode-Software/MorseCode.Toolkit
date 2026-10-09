using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using MorseCode.Avalonia.Controls.Tests;

[assembly: AvaloniaTestApplication(appBuilderEntryPointType: typeof(TestApplication))]

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     The application that the tests run in, with no screen. It has the Fluent
///     theme, thus a combo box has its usual template.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of Application does not say what the tests need.
internal sealed class TestApplication : Application
{
    /// <summary>
    ///     Builds the application. Avalonia.Headless finds this method by its name.
    /// </summary>
    /// <returns>The builder of the application, on the headless platform.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApplication>()
            .UseHeadless(opts: new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });

    /// <summary>
    ///     Runs a test on the user interface thread of the headless application.
    /// </summary>
    /// <param name="test">The test.</param>
    /// <returns>The task of the test.</returns>
    public static Task Run(Func<Task> test) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(assembly: typeof(TestApplication).Assembly)
            .Dispatch(
                action: async () =>
                {
                    await test();

                    return true;
                },
                cancellationToken: CancellationToken.None);

    /// <inheritdoc />
    public override void Initialize() => this.Styles.Add(item: new FluentTheme());
}
