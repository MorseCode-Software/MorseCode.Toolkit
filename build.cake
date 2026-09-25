// Build script for the solution under src.
//
// This follows SodaFlow's build.cake target for target, and that file keeps the history and the
// measurements behind each step. What is repeated here is what a reader needs in order to act.
//
// Run it locally with:
//
//     dotnet tool restore
//     dotnet cake
//
// which restores, builds, tests with coverage, packs, and runs the inspection. Publishing is not
// part of the default target; see the Publish task.
//
// CI drives the tasks one phase at a time with --exclusive, so a failure is attributed to its
// phase. The dependencies below are what a local run follows; keep them accurate anyway.
//
// Package versions are not set here. Each packable project derives its own version from git tags
// via MinVer; see src/Directory.Build.props.

// Reading the SARIF only. The workflow hands the same file to code scanning.
#addin nuget:?package=Cake.Issues&version=6.0.0
#addin nuget:?package=Cake.Issues.Sarif&version=6.0.0

using System.Xml.Linq;

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var solution = File("./src/MorseCode.Toolkit.slnx");
var artifactsDirectory = Directory("./artifacts");
var coverageDirectory = Directory("./coverage");
var inspectionDirectory = Directory("./inspection");
// Kept between runs, and keyed by the settings in force; see InspectionCacheFor. Not
// inspectionDirectory, which is cleaned at the start of every inspection.
var inspectionCacheDirectory = Directory("./.inspectcode-cache");
// A byte-identical copy of SodaFlow's src/SodaFlow.sln.DotSettings. The two repositories are held
// to one rule set, so a rule change there is copied here.
var inspectionSettings = File("./src/MorseCode.Toolkit.sln.DotSettings");
// Overridable so a release can be rehearsed against a local folder feed. nuget.org does not allow a
// version to be deleted or reused.
var nugetSource = Argument("nuget-source", "https://api.nuget.org/v3/index.json");

//////////////////////////////////////////////////////////////////////
// SETUP
//////////////////////////////////////////////////////////////////////

Setup(context =>
{
    Information("Building MorseCode.Toolkit in {0}.", configuration);
});

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Info")
    .Description("Prints the SDK the build is running against.")
    .Does(() =>
{
    // The first thing to compare when a build reproduces locally but not on CI.
    var exitCode = StartProcess("dotnet", new ProcessSettings { Arguments = "--info" });
    if (exitCode != 0)
    {
        throw new Exception($"dotnet --info failed with exit code {exitCode}.");
    }
});

Task("Restore")
    .Description("Restores every project in the solution.")
    .IsDependentOn("Info")
    .Does(() =>
{
    DotNetRestore(solution);
});

Task("Build")
    .Description("Builds the solution.")
    .IsDependentOn("Restore")
    .Does(() =>
{
    DotNetBuild(
        solution,
        new DotNetBuildSettings
        {
            Configuration = configuration,
            NoRestore = true,
        });
});

Task("Test")
    .Description("Runs every test project, collecting coverage as it goes.")
    .IsDependentOn("Build")
    .Does(() =>
{
    CleanDirectory(coverageDirectory);

    // The test projects run on Microsoft.Testing.Platform, which global.json selects, so everything
    // after the -- belongs to them rather than to the SDK. Coverage is the Microsoft Code Coverage
    // collector, which arrives with TUnit and writes Cobertura; coverage.runsettings scopes it.
    var resultsDirectory = MakeAbsolute(coverageDirectory).FullPath;
    var coverageSettings = MakeAbsolute(File("./coverage.runsettings")).FullPath;

    DotNetTest(
        solution,
        new DotNetTestSettings
        {
            Configuration = configuration,
            NoBuild = true,
            ArgumentCustomization = args => args
                .Append("--")
                .Append("--results-directory").AppendQuoted(resultsDirectory)
                .Append("--coverage")
                .Append("--coverage-output-format").Append("cobertura")
                .Append("--coverage-settings").AppendQuoted(coverageSettings)
                .Append("--report-trx"),
        });

    // Checked here, where the reports are made. A run that submitted no coverage because none was
    // written would otherwise look like a build that got faster.
    //
    // Not a recursive glob: the collector writes every report twice, the second copy under a
    // machine-and-timestamp directory, and build.yml globs the same way for the same reason.
    var reports = GetFiles($"{coverageDirectory.Path}/*.cobertura.xml")
        .OrderBy(r => r.FullPath, StringComparer.Ordinal)
        .ToList();

    if (reports.Count == 0)
    {
        throw new Exception("No Cobertura report was produced.");
    }

    Information("Coverage reports ({0}):", reports.Count);
    foreach (var report in reports)
    {
        Information("  {0}", report.FullPath);
    }
});

Task("Pack")
    .Description("Packs every publishable project.")
    .IsDependentOn("Test")
    .Does(() =>
{
    CleanDirectory(artifactsDirectory);

    // Test projects set IsPackable false, so one pack over the solution produces only the packages.
    DotNetPack(
        solution,
        new DotNetPackSettings
        {
            Configuration = configuration,
            OutputDirectory = artifactsDirectory,
        });

    foreach (var package in GetFiles($"{artifactsDirectory.Path}/*.nupkg").OrderBy(p => p.FullPath))
    {
        Information(package.GetFilename().FullPath);
    }
});

// compiler(line): RuleId: text - the shape an editor, a log reader and a person already scan.
string Describe(IIssue issue) =>
    $"{issue.AffectedFileRelativePath?.FullPath ?? "<solution>"}"
    + $"({issue.Line?.ToString() ?? "-"}): {issue.RuleId}: {issue.MessageText}";

// One report at every severity, hints included, and any finding at all fails the build. The way to
// stop a rule failing it, other than fixing the code, is to change the rule in the .DotSettings
// file, where Rider then agrees with CI - and here that means changing it in SodaFlow first.
void RunInspection(FilePath solutionPath, FilePath reportPath, string description)
{
    var cacheDirectory = InspectionCacheFor();

    InvokeInspectCode(solutionPath, reportPath, cacheDirectory);

    var issues = ReadIssues(
            SarifIssuesFromFilePath(reportPath),
            Context.Environment.WorkingDirectory)
        .OrderBy(i => i.AffectedFileRelativePath?.FullPath ?? string.Empty, StringComparer.Ordinal)
        .ThenBy(i => i.Line ?? 0)
        .ToList();

    // Listed before the throw, because the build that fails on the inspection is the one that needs
    // to say what it found.
    Information("InspectCode found {0} issue(s) in {1}.", issues.Count, description);
    foreach (var issue in issues)
    {
        Information("  {0}", Describe(issue));
    }

    if (issues.Count > 0)
    {
        throw new Exception(
            $"InspectCode found {issues.Count} issue(s) in {description}, listed above. Fix them, or "
            + $"change the rule in {inspectionSettings.Path} and in SodaFlow's copy.");
    }
}

// A subdirectory of inspectionCacheDirectory named by a hash of every file that decides what an
// inspection reports. inspectcode revalidates a cache against source edits but not against the
// settings, so a settings change has to start cold. Caches left by other settings are removed.
//
// build.yml hashes the same files into its cache key.
DirectoryPath InspectionCacheFor()
{
    var inputs = new List<FilePath> { inspectionSettings.Path };
    inputs.AddRange(GetFiles("./.editorconfig"));
    inputs.AddRange(GetFiles("./src/**/.editorconfig"));

    var root = MakeAbsolute(Directory("."));
    var ordered = inputs
        .Select(input => MakeAbsolute(input))
        .GroupBy(input => input.FullPath, StringComparer.Ordinal)
        .Select(group => group.First())
        .OrderBy(input => input.FullPath, StringComparer.Ordinal)
        .ToList();

    string key;
    using (var sha = System.Security.Cryptography.SHA256.Create())
    {
        foreach (var input in ordered)
        {
            // The path goes in as well as the content, so moving a file is a change too.
            var name = System.Text.Encoding.UTF8.GetBytes(root.GetRelativePath(input).FullPath + "\n");
            sha.TransformBlock(name, 0, name.Length, null, 0);

            var content = System.IO.File.ReadAllBytes(input.FullPath);
            sha.TransformBlock(content, 0, content.Length, null, 0);
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        key = BitConverter.ToString(sha.Hash, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
    }

    EnsureDirectoryExists(inspectionCacheDirectory);

    foreach (var other in GetDirectories("./.inspectcode-cache/*"))
    {
        if (!string.Equals(other.GetDirectoryName(), key, StringComparison.Ordinal))
        {
            DeleteDirectory(other, new DeleteDirectorySettings { Recursive = true, Force = true });
        }
    }

    foreach (var loose in GetFiles("./.inspectcode-cache/*"))
    {
        DeleteFile(loose);
    }

    var current = inspectionCacheDirectory + Directory(key);
    EnsureDirectoryExists(current);

    Information(
        "Inspection cache {0}, keyed by {1}.",
        key,
        string.Join(", ", ordered.Select(input => root.GetRelativePath(input).FullPath)));

    return current;
}

// One inspectcode run over one solution, writing SARIF at every severity.
void InvokeInspectCode(FilePath solutionPath, FilePath outputPath, DirectoryPath cacheDirectory)
{
    var arguments = new ProcessArgumentBuilder()
        .Append("jb")
        .Append("inspectcode")
        .AppendQuoted(MakeAbsolute(solutionPath).FullPath)
        .AppendSwitchQuoted("--output", "=", MakeAbsolute(outputPath).FullPath)
        .Append("--format=Sarif")
        // Named explicitly because inspectcode pairs settings with a solution by name, and the
        // solution is .slnx while the settings are .sln.DotSettings. Absolute, because inspectcode
        // ignores a relative --settings path without saying so.
        .AppendSwitchQuoted("--settings", "=", MakeAbsolute(inspectionSettings.Path).FullPath)
        .AppendSwitchQuoted("--caches-home", "=", MakeAbsolute(cacheDirectory).FullPath)
        // So the issues are reported against paths from the repository root rather than from src.
        .Append("--absolute-paths")
        // Already built by whatever depends on this.
        .Append("--no-build")
        .Append($"--properties:Configuration={configuration}")
        // Every severity. inspectcode's default is SUGGESTION, which leaves out the hints Rider shows.
        .Append("--severity=INFO")
        .Append("--verbosity=WARN");

    var exitCode = StartProcess("dotnet", new ProcessSettings { Arguments = arguments });
    if (exitCode != 0)
    {
        throw new Exception($"inspectcode failed (exit {exitCode}).");
    }
}

Task("Inspect-Code")
    .Description("Runs JetBrains InspectCode over the solution and reports what it finds.")
    .IsDependentOn("Build")
    .Does(() =>
{
    CleanDirectory(inspectionDirectory);

    RunInspection(
        solution.Path,
        (inspectionDirectory + File("inspectcode.sarif")).Path,
        solution.Path.FullPath);
});

Task("Publish")
    .Description("Pushes the one package this build's tag names to nuget.org.")
    .Does(() =>
{
    // Gated on a tag, and a tag build publishes exactly the one package its tag names, so the order
    // packages reach nuget.org is the order their tags are pushed. Push dependencies first, and wait
    // for each run to publish before pushing the next.
    if (!BuildSystem.IsRunningOnGitHubActions ||
        GitHubActions.Environment.Workflow.RefType != GitHubActionsRefType.Tag)
    {
        Information("Not a tag build - skipping NuGet push.");
        return;
    }

    var tag = GitHubActions.Environment.Workflow.RefName;
    if (string.IsNullOrEmpty(tag))
    {
        throw new Exception(
            "This is a tag build but the tag name is empty, so there is no way to tell which " +
            "package it releases.");
    }

    // Minted by the NuGet login step in build.yml through trusted publishing; nothing here stores a
    // key. Thrown rather than skipped: a tag build that quietly released nothing is the failure
    // hardest to notice.
    var apiKey = EnvironmentVariable("NUGET_API_KEY");
    if (string.IsNullOrEmpty(apiKey))
    {
        throw new Exception(
            "NUGET_API_KEY is not set. It is minted by the NuGet login step in "
            + ".github/workflows/build.yml, which exchanges this run's OIDC token for a short-lived "
            + "key. An empty value means that exchange did not happen or was refused - check that a "
            + "trusted publishing policy for this repository and workflow exists on nuget.org, owned "
            + "by the MorseCodeSoftware organization.");
    }

    // Read from the projects, where MinVerTagPrefix and PackageId already live, rather than kept as
    // a second copy here that could drift from them.
    var packageIdByPrefix = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var project in GetFiles("./src/**/*.csproj"))
    {
        var document = XDocument.Load(project.FullPath);
        var prefix = document.Descendants("MinVerTagPrefix").FirstOrDefault();
        var id = document.Descendants("PackageId").FirstOrDefault();
        if (prefix != null && id != null)
        {
            packageIdByPrefix[prefix.Value] = id.Value;
        }
    }

    if (packageIdByPrefix.Count == 0)
    {
        throw new Exception("Found no project under src declaring both MinVerTagPrefix and PackageId.");
    }

    // The rest of the tag is the version, so it has to start with a digit; otherwise morsecode-mvvm-
    // would claim morsecode-mvvm-wpf-1.0.0. More than one match is refused rather than tie-broken,
    // because publishing the wrong package cannot be undone.
    var prefixes = packageIdByPrefix.Keys
        .Where(p =>
            tag.StartsWith(p, StringComparison.Ordinal) &&
            tag.Length > p.Length &&
            char.IsDigit(tag[p.Length]))
        .OrderBy(p => p, StringComparer.Ordinal)
        .ToList();

    if (prefixes.Count == 0)
    {
        Information("Tag '{0}' does not name a package in this repository - skipping NuGet push.", tag);
        Information(
            "Known prefixes: {0}",
            string.Join(", ", packageIdByPrefix.Keys.OrderBy(p => p, StringComparer.Ordinal)));
        return;
    }

    if (prefixes.Count > 1)
    {
        throw new Exception(
            $"Tag '{tag}' matches more than one package prefix: {string.Join(", ", prefixes)}. " +
            "Rename one of them so that neither extends the other.");
    }

    var packageId = packageIdByPrefix[prefixes[0]];
    Information("Tag '{0}' releases {1}.", tag, packageId);

    // Matched by name rather than by glob, which would also match every package whose id extends
    // this one. The legacy *.symbols.nupkg that IncludeSymbols emits is excluded, because nuget.org
    // rejects it.
    var packages = GetFiles($"{artifactsDirectory.Path}/*.nupkg")
        .Where(p => !p.GetFilename().FullPath.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
        .Where(p =>
        {
            var name = p.GetFilename().FullPath;
            return name.StartsWith(packageId + ".", StringComparison.OrdinalIgnoreCase) &&
                   name.Length > packageId.Length + 1 &&
                   char.IsDigit(name[packageId.Length + 1]);
        })
        .ToList();

    if (packages.Count != 1)
    {
        throw new Exception(
            $"Expected exactly one {packageId} package in artifacts, found {packages.Count}.");
    }

    Information("Pushing {0} to {1}", packages[0].GetFilename(), nugetSource);

    // --skip-duplicate makes re-running a tag build a no-op for a package it already pushed.
    DotNetNuGetPush(
        packages[0].FullPath,
        new DotNetNuGetPushSettings
        {
            ApiKey = apiKey,
            Source = nugetSource,
            SkipDuplicate = true,
        });
});

// Inspect-Code is listed second so a default run still packs first: leaving the packages and the
// coverage behind is worth more than failing a few seconds earlier.
Task("Default")
    .Description("Restore, build, test with coverage, pack, and inspect.")
    .IsDependentOn("Pack")
    .IsDependentOn("Inspect-Code");

RunTarget(target);
