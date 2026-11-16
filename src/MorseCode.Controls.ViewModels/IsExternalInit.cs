// The compiler needs this type for an init accessor and for a record. .NET 5 and later have it, and
// netstandard2.0 and net472 do not. The compiler finds the type by its name, thus no code names it.

#if !NET5_0_OR_GREATER
using JetBrains.Annotations;

// ReSharper disable once CheckNamespace - The compiler looks for this type in this namespace only.
namespace System.Runtime.CompilerServices;

[UsedImplicitly]
internal static class IsExternalInit;
#endif
