using System;
using System.ComponentModel;
using JetBrains.Annotations;

namespace MorseCode.Mvvm;

[PublicAPI]
public interface IViewModel : IDisposable, INotifyPropertyChanged;
