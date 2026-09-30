# Documentation site

The MorseCode Toolkit documentation site. Built with [DocFX](https://dotnet.github.io/docfx/)
and published to GitHub Pages by [`.github/workflows/docs.yml`](../.github/workflows/docs.yml)
on every push to `main`.

## Build it locally

DocFX is pinned as a local .NET tool, so restore it once:

```bash
dotnet tool restore
```

Then, from the repository root:

```bash
dotnet docfx docs/docfx.json --serve
```

That extracts API metadata, builds the site into `docs/_site`, and serves it at
<http://localhost:8080>. Drop `--serve` to build without serving.

Metadata extraction compiles the projects, so the first run takes a while and needs a working
`dotnet restore`.

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
`@MorseCode.StagedConstruction.IConstruct\`1` or `<xref:MorseCode.Mvvm.ViewModelBase>` rather than
a hand-written URL, so the link survives refactoring. Backtick-N is the arity suffix for generic
types.

**Links inside raw HTML.** `index.md` writes its hero and its cards as HTML, because Markdown
cannot put a class on an element. DocFX rewrites and validates `href` and `src` in that HTML
exactly as it does for Markdown links, so they point at the **source** file, `docs/docs/packages.md`
and not its built path. Writing the built path instead is a warning, and CI builds the site with
`--warningsAsErrors`.

## Adding a project to the API reference

Add its `.csproj` to the `metadata.src.files` list in `docfx.json`, and make sure the project sets
`GenerateDocumentationFile`.
