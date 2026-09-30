---
title: MorseCode Toolkit for .NET
layout: landing
---

<div class="sf-hero">
<img class="sf-hero-logo" src="images/morsecode-wordmark.png" alt="MorseCode Software">
<p class="sf-hero-tagline">Applications on SodaFlow, built one way.</p>
<p class="sf-hero-lede">SodaFlow is a general-purpose library and leaves the shape of your application to you. This toolkit holds the opinions MorseCode Software builds its own applications with: how a view model is put together, how its graph is constructed, and how it releases what it built.</p>
<p class="sf-actions">
<a class="sf-button sf-button-primary" href="docs/getting-started.md">Get started</a>
<a class="sf-button" href="docs/staged-construction.md">Staged construction</a>
<a class="sf-button" href="api/index.md">API reference</a>
</p>
</div>

```csharp
public static CounterViewModel Create(IBindingScheduler bindingScheduler) =>
    Transaction.Run(() =>
        ViewModelBase.CreateBase(bindingScheduler, (output, construct) =>
        {
            StreamSink<Unit> increments = Stream.CreateSink<Unit>();
            Cell<int> count = increments.Accum(0, (_, total) => total + 1);

            IOneWayBindableValue<int> countValue = output.BindableFactory.CreateOneWay(count);
            IBindableAction increment = output.BindableFactory.CreateBindableAction(increments);

            return construct.Construct(viewModelBase =>
                new CounterViewModel(viewModelBase, countValue, increment));
        }));
```

The packages are published under the `MorseCode.*` prefix and version independently. They are
pre-1.0, and their APIs will change as the applications built on them teach us what they need.

## Start here

<div class="sf-cards">
<a class="sf-card" href="docs/getting-started.md">
<i class="bi bi-play-circle"></i>
<span class="sf-card-title">Getting started</span>
<span class="sf-card-text">Install a package and build your first view model.</span>
</a>
<a class="sf-card" href="docs/packages.md">
<i class="bi bi-box-seam"></i>
<span class="sf-card-title">Which package do I install?</span>
<span class="sf-card-text">The two packages, what each one holds, and how they depend on each other.</span>
</a>
<a class="sf-card" href="docs/staged-construction.md">
<i class="bi bi-layers"></i>
<span class="sf-card-title">Staged construction</span>
<span class="sf-card-text">Build an immutable object without any access to a partly built one.</span>
</a>
<a class="sf-card" href="docs/view-models.md">
<i class="bi bi-window-stack"></i>
<span class="sf-card-title">View models</span>
<span class="sf-card-text">ViewModelBase, and the shape of a view model that uses it.</span>
</a>
<a class="sf-card" href="docs/disposal.md">
<i class="bi bi-hourglass-split"></i>
<span class="sf-card-title">Disposal and lifetimes</span>
<span class="sf-card-text">One Dispose releases everything the view model made.</span>
</a>
<a class="sf-card" href="api/index.md">
<i class="bi bi-braces"></i>
<span class="sf-card-title">API reference</span>
<span class="sf-card-text">Generated from the source.</span>
</a>
</div>

## Elsewhere

- **SodaFlow** — the functional reactive library this toolkit builds on:
  <https://github.com/MorseCode-Software/SodaFlow>. Its
  [documentation site](https://morsecode-software.github.io/SodaFlow/) explains streams, cells,
  and transactions, which these pages assume.
- **Issues** — <https://github.com/MorseCode-Software/MorseCode.Toolkit/issues>
