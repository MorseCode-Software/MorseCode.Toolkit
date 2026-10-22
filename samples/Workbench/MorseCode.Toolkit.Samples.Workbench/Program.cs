using System;
using Avalonia;
using SodaFlow.Bindable.ObjectModel;

namespace MorseCode.Toolkit.Samples.Workbench;

/// <summary>
///     The entry point of the application.
/// </summary>
file static class Program
{
    /// <summary>
    ///     Starts the application.
    /// </summary>
    /// <param name="args">The arguments of the command line.</param>
    [STAThread]
    public static void Main(string[] args) =>
        AppBuilder.Configure(
                // The scheduler is the one of the user interface thread. This function runs on that
                // thread when the framework is ready.
                static () => new App(
                    createMainWindow: static () =>
                        CompositionRoot.CreateMainWindow(
                            bindingScheduler: SynchronizationContextBindingScheduler.Capture())))
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args: args);
}
