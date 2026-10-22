using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace MorseCode.Toolkit.Sample;

/// <summary>
///     The application. It shows the window that <see cref="CompositionRoot" /> builds.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of Application does not say what this class shows.
internal sealed class App : Application
{
    private readonly Func<Window> createMainWindow;

    /// <summary>
    ///     Makes the application.
    /// </summary>
    /// <param name="createMainWindow">The function that builds the main window.</param>
    // ReSharper disable once InheritdocConsiderUsage - The summary of the base constructor does not say what this one takes.
    public App(Func<Window> createMainWindow) => this.createMainWindow = createMainWindow;

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(obj: this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = this.createMainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
