# Documentation site

The MorseCode Toolkit documentation site. Built with [DocFX](https://dotnet.github.io/docfx/)
and published to GitHub Pages by [`.github/workflows/docs.yml`](../.github/workflows/docs.yml)
on every push to `main`.

## Build it locally

DocFX is pinned as a local .NET tool, so restore it once:

```bash
dotnet tool restore
```

DocFX reads the projects without building them, but every library in `src/` loads two analyzers,
`MorseCode.Analyzers` and the analyzer of `MorseCode.StagedConstruction`, and DocFX needs them
built first. Without them it reports a `FailedToResolveAnalyzer` warning for each. So, from the
repository root, build both in Release once, which is the configuration `docfx.json` reads:

```bash
dotnet build src/MorseCode.Analyzers/MorseCode.Analyzers.csproj --configuration Release
```

```bash
dotnet build src/MorseCode.StagedConstruction.Analyzers/MorseCode.StagedConstruction.Analyzers.csproj --configuration Release
```

Then build and serve the site:

```bash
dotnet docfx docs/docfx.json --serve
```

That extracts API metadata, builds the site into `docs/_site`, and serves it at
<http://localhost:8080>. Drop `--serve` to build without serving, and add `--warningsAsErrors` to
build it the way CI does. DocFX restores the projects itself.

## Layout

| Path | What it is |
| --- | --- |
| `docfx.json` | Site configuration: which projects to extract, how to build. |
| `index.md` | Landing page. |
| `toc.yml` | Top navigation bar. |
| `docs/` | Hand-written conceptual pages. |
| `api/index.md` | Hand-written API landing page. |
| `api/*.yml` | **Generated.** Git-ignored; produced by `docfx metadata`. |
| `template/` | The theme: `public/main.css` and `public/main.js`. |
| `images/` | Site images: the MorseCode Software mark (navbar and favicon) and wordmark (landing page). |
| `_site/` | **Generated.** Git-ignored build output. |

## Theme

`template/` is a DocFX template that carries two files, and `docfx.json` lists it after `default`
and `modern` so that they override the empty ones `modern` ships. `main.css` sets its colors once
as custom properties, for the light theme and again for the dark one. The colors are the blue of the
MorseCode Software logo plus neutrals mixed toward it. `main.js` holds the
GitHub and NuGet icon links in the navigation bar.

## Writing pages

Add a Markdown file under `docs/` and an entry in `docs/docs/toc.yml`. These conventions matter:

**Cross-references into the API.** Link to a type with
`@MorseCode.StagedConstruction.Constructor\`1` or `<xref:MorseCode.Mvvm.ViewModelBase>` rather than
a hand-written URL, so the link survives refactoring. Backtick-N is the arity suffix for generic
types.

**Links inside raw HTML.** `index.md` writes its hero and its cards as HTML, because Markdown
cannot put a class on an element. DocFX rewrites and validates `href` and `src` in that HTML
exactly as it does for Markdown links, so they point at the **source** file, `docs/docs/packages.md`
and not its built path. Writing the built path instead is a warning, and CI builds the site with
`--warningsAsErrors`.

## Adding a project to the API reference

- Add its `.csproj` to the `metadata.src.files` list in `docfx.json`.
- Add a `dotnet restore` of it to the "Restore projects" step of
  [`.github/workflows/docs.yml`](../.github/workflows/docs.yml), which names the same projects.
- Make sure the project sets `GenerateDocumentationFile`.
- Make sure it has a `net8.0` target. `docfx.json` pins `TargetFramework` to `net8.0` for every
  project it reads, because the projects target several frameworks and DocFX must see one.
- Describe the package in `api/index.md`.
