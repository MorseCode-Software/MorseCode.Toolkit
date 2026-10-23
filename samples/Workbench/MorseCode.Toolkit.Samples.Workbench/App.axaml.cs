using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace MorseCode.Toolkit.Samples.Workbench;

/// <summary>
///     The application. It builds the view model with the function that it receives, gives the view
///     model to the window as its data context, and releases the view model when the application
///     exits.
/// </summary>
/// <remarks>
///     The window does not build its own view model. A view that builds the object that it binds to
///     knows the concrete type and the function that makes it, and a bind to
///     <see cref="IWorkbenchViewModel" /> prevents that. The window receives its data context, and
///     this code does the composition.
/// </remarks>
// ReSharper disable once InheritdocConsiderUsage - The summary of Application does not say what this class shows.
internal sealed class App : Application
{
    private readonly Func<IWorkbenchViewModel> createViewModel;

    /// <summary>
    ///     Makes the application.
    /// </summary>
    /// <param name="createViewModel">The function that builds the view model of the main window.</param>
    // ReSharper disable once InheritdocConsiderUsage - The summary of the base constructor does not say what this one takes.
    public App(Func<IWorkbenchViewModel> createViewModel) => this.createViewModel = createViewModel;

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(obj: this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            IWorkbenchViewModel viewModel = this.createViewModel();

            // This code assigns the window and does not show it. The lifetime shows the window after
            // this method returns, thus the window has its data context before it is on the screen.
            desktop.MainWindow = new MainWindow { DataContext = viewModel };

            // The view model holds subscriptions into the FRP graph, and the code that built it
            // releases it.
            desktop.Exit += (_, _) => viewModel.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
