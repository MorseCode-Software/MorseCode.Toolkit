# Samples

Each sample is a folder with its own solution. A sample is not part of the build under `src/`: it
references the published packages of this repository, at a pinned version, and not the projects. It
shows what a user gets when they install the packages.

| Sample | Packages it uses | What it shows |
| --- | --- | --- |
| [Workbench](Workbench) | `MorseCode.Mvvm`, `MorseCode.StagedConstruction`, `MorseCode.Avalonia.Controls`, `MorseCode.Analyzers` | `ViewModelBase`, staged construction, and `VirtualizingItemsControl` over a list of 500,000 items |

Open the solution beside the sample, `Workbench/MorseCode.Toolkit.Samples.Workbench.slnx`, and run
the app, or run it from the root of the repository:

```bash
dotnet run --project samples/Workbench/MorseCode.Toolkit.Samples.Workbench
```

The [sample application page](../docs/docs/sample-app.md) explains what to read and why.

## How a sample is built

- Each sample has its own `.slnx`, and a `.sln.DotSettings` beside it that is a byte-identical copy
  of `src/MorseCode.Toolkit.sln.DotSettings`. `dotnet cake` checks that the copy matches.
- `samples/Directory.Build.props` holds the rules that every sample project keeps. MSBuild reads the
  nearest `Directory.Build.props` and stops, so a sample does not read the one under `src/`.
- Each project restores through a committed `packages.lock.json`. Changing a pinned version means
  restoring and committing the lock file.
- The [samples workflow](../.github/workflows/samples.yml) builds, tests, and inspects a sample on every
  pull request and every push to `main`. It has no path filter, so a check of it can be required.
  Run the inspection yourself with `dotnet cake --target=Inspect-Sample --sample=Workbench`.
