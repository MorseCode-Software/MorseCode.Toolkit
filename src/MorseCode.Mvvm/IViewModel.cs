using System;
using System.ComponentModel;
using JetBrains.Annotations;

namespace MorseCode.Mvvm;

/// <summary>
///     The interface that a view model shows to its view.
/// </summary>
/// <remarks>
///     <see cref="IDisposable.Dispose" /> releases the subscriptions that the construction of the view
///     model made. The properties of a view model do not change, and the event of
///     <see cref="INotifyPropertyChanged" /> does not occur. The interface is here only to prevent a
///     memory leak in WPF. <see cref="ViewModelBase.PropertyChanged" /> gives the cause.
/// </remarks>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - The summaries of IDisposable and INotifyPropertyChanged do not say that the event of a view model does not occur.
public interface IViewModel : IDisposable, INotifyPropertyChanged;
