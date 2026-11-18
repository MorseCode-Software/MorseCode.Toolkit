// Build script for the solution under src, and for the inspection of the samples under samples.
//
// The samples are not built here. Each has its own solution, and the samples workflow builds it. This
// file only inspects a sample (Inspect-Sample) and checks that the settings beside it match.
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

using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Xml.Linq;

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

// Which sample Inspect-Sample looks at. Empty for every other target.
var sampleName = Argument("sample", string.Empty);

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
// The editor-only copies beside the sample solutions, which must match it byte for byte. Rider pairs
// a .DotSettings file with the solution beside it, so each solution needs a copy of its own. CI reads
// only the canonical file. Thus, the copies exist for the editor alone, and a difference between
// them is a difference between what CI enforces and what Rider shows.
var mirroredInspectionSettings = new[]
{
    File("./samples/Workbench/MorseCode.Toolkit.Samples.Workbench.sln.DotSettings"),
};
// Overridable so a release can be rehearsed against a local folder feed: --tag names the package, and
// this names the folder. RELEASING.md gives the command. nuget.org does not allow a version to be
// deleted or reused.
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
    .IsDependentOn("Verify-Inspection-Settings")
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

// A dependency of Build rather than a step of its own in the workflow. As a dependency it runs
// everywhere without being listed anywhere: a local `dotnet cake` and CI both reach it through Build.
// It needs nothing compiled, so it costs milliseconds.
Task("Verify-Inspection-Settings")
    .Description("Checks that the inspection settings beside each sample match the canonical ones.")
    .Does(() =>
{
    var expected = System.IO.File.ReadAllBytes(MakeAbsolute(inspectionSettings.Path).FullPath);
    var problems = new List<string>();

    foreach (var mirrored in mirroredInspectionSettings)
    {
        var path = MakeAbsolute(mirrored.Path).FullPath;

        if (!System.IO.File.Exists(path))
        {
            problems.Add($"{mirrored.Path} is missing.");
        }
        else if (!System.IO.File.ReadAllBytes(path).SequenceEqual(expected))
        {
            problems.Add($"{mirrored.Path} differs from {inspectionSettings.Path}.");
        }
    }

    foreach (var problem in problems)
    {
        Error("  {0}", problem);
    }

    if (problems.Count > 0)
    {
        throw new Exception(
            $"{problems.Count} inspection settings file(s) out of step with {inspectionSettings.Path}, "
            + "listed above. Copy that file over them. They are meant to be byte-identical.");
    }

    Information(
        "All {0} mirrored inspection settings match {1}.",
        mirroredInspectionSettings.Length,
        inspectionSettings.Path);
});

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
    inputs.AddRange(GetFiles("./samples/**/.editorconfig"));

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

// One sample per run, named by --sample, because the samples workflow runs a job for each sample, so a
// failure says which sample it is.
//
// Not dependent on Build, which compiles the solution under src. A sample does not use it: the samples
// reference the published packages, and the workflow has already built the sample itself by the time
// this runs.
Task("Inspect-Sample")
    .Description("Runs JetBrains InspectCode over one sample solution. Pass --sample=Workbench.")
    .Does(() =>
{
    if (string.IsNullOrWhiteSpace(sampleName))
    {
        throw new Exception("Pass which sample to inspect, for example --sample=Workbench.");
    }

    var sampleSolution = File($"./samples/{sampleName}/MorseCode.Toolkit.Samples.{sampleName}.slnx");

    if (!FileExists(sampleSolution))
    {
        throw new Exception($"No sample solution at {sampleSolution.Path}.");
    }

    // Not cleaned, unlike the inspection of the libraries: each sample writes its own report, and
    // running all of them locally should end with all of them.
    EnsureDirectoryExists(inspectionDirectory);

    RunInspection(
        sampleSolution.Path,
        (inspectionDirectory + File($"inspectcode-{sampleName}.sarif")).Path,
        $"the {sampleName} sample");
});

// The package of each MinVerTagPrefix under src, with its project. Read from the projects, where
// MinVerTagPrefix and PackageId already live, rather than kept as a second copy here that could
// drift from them.
Dictionary<string, (string Id, FilePath Project)> PackagesByPrefix()
{
    var packages = new Dictionary<string, (string Id, FilePath Project)>(StringComparer.Ordinal);
    foreach (var project in GetFiles("./src/**/*.csproj"))
    {
        var document = XDocument.Load(project.FullPath);
        var prefix = document.Descendants("MinVerTagPrefix").FirstOrDefault();
        var id = document.Descendants("PackageId").FirstOrDefault();
        if (prefix != null && id != null)
        {
            packages[prefix.Value] = (id.Value, project);
        }
    }

    if (packages.Count == 0)
    {
        throw new Exception("Found no project under src declaring both MinVerTagPrefix and PackageId.");
    }

    return packages;
}

// The prefixes that a tag starts with. The rest of the tag is the version, so it has to start with
// a digit; otherwise morsecode-mvvm- would claim morsecode-mvvm-wpf-1.0.0.
List<string> PrefixesOf(string tag, IEnumerable<string> prefixes) =>
    prefixes
        .Where(p => IsTagOf(tag, p))
        .OrderBy(p => p, StringComparer.Ordinal)
        .ToList();

bool IsTagOf(string tag, string prefix) =>
    tag.StartsWith(prefix, StringComparison.Ordinal) && tag.Length > prefix.Length && char.IsDigit(tag[prefix.Length]);

// The one package that a tag releases. Thrown rather than skipped, for a tag that names no package as
// for one that names more than one: a tag build that quietly released nothing is the failure hardest to
// notice, and publishing the wrong package cannot be undone. The likely way to name none is a tag with
// the prefix that a package had before a rename, such as morsecode-avalonia-controls- for what is now
// MorseCode.Controls.UI.Avalonia. The message lists the prefixes that the projects declare now.
(string Id, FilePath Project) PackageOfTag(string tag, Dictionary<string, (string Id, FilePath Project)> packages)
{
    var prefixes = PrefixesOf(tag, packages.Keys);

    if (prefixes.Count == 0)
    {
        throw new Exception(
            $"Tag '{tag}' does not name a package in this repository, so this build would release " +
            "nothing. A release tag is the MinVerTagPrefix of a project under src followed by its version. " +
            "The prefixes are: " +
            string.Join(", ", packages.Keys.OrderBy(p => p, StringComparer.Ordinal)) + ". " +
            "Delete the tag, and push one with the prefix of the package.");
    }

    if (prefixes.Count > 1)
    {
        throw new Exception(
            $"Tag '{tag}' matches more than one package prefix: {string.Join(", ", prefixes)}. " +
            "Rename one of them so that neither extends the other.");
    }

    return packages[prefixes[0]];
}

// The projects that a project file names in a ProjectReference. The references that
// src/Directory.Build.props adds are analyzers, which a package does not depend on, and they are not
// in the project file.
List<FilePath> ProjectReferencesOf(FilePath project) =>
    XDocument.Load(project.FullPath)
        .Descendants("ProjectReference")
        .Select(reference => reference.Attribute("Include")?.Value)
        .Where(include => !string.IsNullOrEmpty(include))
        .Select(include => new FilePath(System.IO.Path.GetFullPath(
            System.IO.Path.Combine(project.GetDirectory().FullPath, include.Replace('\\', '/')))))
        .ToList();

// The last tag of a package: the highest version with its prefix, of the tags in the history of this
// commit. With MinVerIgnoreHeight, that is the version that MinVer gives the package here.
string LastTag(string prefix)
{
    var exitCode = StartProcess(
        "git",
        new ProcessSettings
        {
            Arguments = new ProcessArgumentBuilder()
                .Append("tag").Append("--merged").Append("HEAD")
                .Append("--list").AppendQuoted(prefix + "*")
                .Append("--sort=-v:refname"),
            RedirectStandardOutput = true,
        },
        out var tags);

    if (exitCode != 0)
    {
        throw new Exception($"git tag failed with exit code {exitCode}.");
    }

    return tags.FirstOrDefault(tag => IsTagOf(tag, prefix));
}

// True when a folder is different at this commit and at a tag.
bool ChangedSince(string tag, DirectoryPath folder)
{
    // git diff --quiet gives 1 for a difference and 0 for none. A larger code is a failure of git.
    var exitCode = StartProcess(
        "git",
        new ProcessSettings
        {
            Arguments = new ProcessArgumentBuilder()
                .Append("diff").Append("--quiet").Append(tag).Append("HEAD")
                .Append("--").AppendQuoted(folder.FullPath),
        });

    if (exitCode > 1)
    {
        throw new Exception($"git diff failed with exit code {exitCode}.");
    }

    return exitCode == 1;
}

// Each package of this repository that a project compiles against, directly or through a different
// project, and that changed after its last tag. The list puts a package after the packages that it
// compiles against, which is the order to release them in.
List<string> UnreleasedReferences(FilePath project, Dictionary<string, (string Id, FilePath Project)> packages)
{
    var prefixByProject = packages.ToDictionary(
        package => package.Value.Project.FullPath,
        package => package.Key,
        StringComparer.OrdinalIgnoreCase);
    var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var unreleased = new List<string>();

    void Visit(FilePath reference)
    {
        if (!visited.Add(reference.FullPath))
        {
            return;
        }

        foreach (var next in ProjectReferencesOf(reference))
        {
            Visit(next);
        }

        if (!prefixByProject.TryGetValue(reference.FullPath, out var prefix))
        {
            return;
        }

        var id = packages[prefix].Id;
        var folder = Context.Environment.WorkingDirectory.GetRelativePath(reference.GetDirectory());
        var lastTag = LastTag(prefix);
        if (lastTag == null)
        {
            unreleased.Add($"{id}: there is no tag {prefix}<version> in the history of this commit.");
        }
        else if (ChangedSince(lastTag, folder))
        {
            unreleased.Add($"{id}: {folder.FullPath} changed after {lastTag}.");
        }
    }

    foreach (var reference in ProjectReferencesOf(project))
    {
        Visit(reference);
    }

    return unreleased;
}

// A tag build packs all packages from the source at its commit. A package that the tagged package
// references declares the version of its last tag. But the build compiles the tagged package against
// the source of that package at this commit. If that source changed after its last tag, the release
// names a version of it that does not have what the build used. Nobody can replace a version after
// it is on nuget.org. Thus, a tag build runs this check before it builds, and the check names each
// package to release first.
//
// The check compares the folder of each project that the tagged project references, directly or
// through a different project, with the last tag of that project. It does not see a change that is
// not in those folders, for example in src/Directory.Build.props.
Task("Verify-Release-Order")
    .Description(
        "Fails when the tag names no package, or when the tagged package compiles against a package here that " +
        "changed after its last tag.")
    .Does(() =>
{
    // With --tag, the check runs for a tag before the push.
    var tag = ReleaseTag();
    if (string.IsNullOrEmpty(tag))
    {
        Information("Not a tag build, and no --tag was given - nothing to check.");
        return;
    }

    // Fails here, before the build and the tests, for a tag that names no package or more than one. This
    // failure skips the publish job, which would otherwise release nothing or the wrong package.
    var packages = PackagesByPrefix();
    var (packageId, project) = PackageOfTag(tag, packages);
    var unreleased = UnreleasedReferences(project, packages);

    if (unreleased.Count > 0)
    {
        throw new Exception(
            $"Tag '{tag}' releases {packageId}, which compiles against packages of this repository that " +
            "changed after their last tag. Release them first, in this order:" + Environment.NewLine +
            string.Join(Environment.NewLine, unreleased.Select(entry => "  " + entry)) + Environment.NewLine +
            "Tag each one on this commit, and wait until nuget.org lists its version. Then run this build " +
            "again. RELEASING.md gives the steps.");
    }

    Information("Tag '{0}' releases {1}, and each package that it compiles against is released.", tag, packageId);
});

// True for the build of a tag in GitHub Actions, which is the one build that publishes.
bool IsTagBuild() =>
    BuildSystem.IsRunningOnGitHubActions &&
    GitHubActions.Environment.Workflow.RefType == GitHubActionsRefType.Tag;

// The tag that this run releases: --tag, or else the tag of a tag build. Empty for each other run. Thus,
// --tag runs the release tasks on a machine, for a rehearsal, with the tag that a push gives.
string ReleaseTag()
{
    var tag = Argument("tag", IsTagBuild() ? GitHubActions.Environment.Workflow.RefName : string.Empty);
    if (IsTagBuild() && string.IsNullOrEmpty(tag))
    {
        throw new Exception(
            "This is a tag build but the tag name is empty, so there is no way to tell which " +
            "package it releases.");
    }

    return tag;
}

// The feed as a URL, or null when it is a folder.
Uri FeedUrl() =>
    Uri.TryCreate(nugetSource, UriKind.Absolute, out var source) &&
    (source.Scheme == Uri.UriSchemeHttp || source.Scheme == Uri.UriSchemeHttps)
        ? source
        : null;

// The one package file of a package in artifacts.
FilePath PackageFileOf(string packageId)
{
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

    return packages[0];
}

// The dependencies that the nuspec of a package declares, each with the lowest version of its
// range. Each target framework has its own group, thus a dependency can occur more than once.
List<(string Id, string Version)> DependenciesOf(FilePath package)
{
    using var archive = ZipFile.OpenRead(package.FullPath);
    var nuspec = archive.Entries.Single(entry =>
        !entry.FullName.Contains('/') && entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));

    using var stream = nuspec.Open();
    return XDocument.Load(stream)
        .Descendants()
        .Where(element => element.Name.LocalName == "dependency")
        .Select(element => (
            Id: element.Attribute("id")?.Value ?? throw new Exception($"A dependency in {nuspec.FullName} has no id."),
            Version: LowestVersionOf(element.Attribute("version")?.Value ?? string.Empty)))
        .Distinct()
        .ToList();
}

// The lowest version that a NuGet range accepts. A version with no brackets is the lowest version. A
// range with no lowest version, or one that does not include it, has no one version to look for.
string LowestVersionOf(string range)
{
    var text = range.Trim();
    if (text.StartsWith("[", StringComparison.Ordinal))
    {
        text = text.Substring(1).Split(',', ']')[0].Trim();
    }
    else if (text.StartsWith("(", StringComparison.Ordinal))
    {
        text = string.Empty;
    }

    if (text.Length == 0)
    {
        throw new Exception(
            $"The range '{range}' has no lowest version that it includes, so there is no version to look for.");
    }

    return text;
}

// True when the feed has the version of a package. For nuget.org, that is true only after it
// validates and indexes the package, and not when it accepts the push. A folder feed is for a
// rehearsal.
async Task<bool> IsOnFeed(HttpClient client, string id, string version)
{
    // The feed keeps IDs and versions in lowercase, and a version with no build metadata.
    var lowerId = id.ToLowerInvariant();
    var lowerVersion = version.Split('+')[0].ToLowerInvariant();

    var source = FeedUrl();
    if (source == null)
    {
        return FolderFeedHas(lowerId, lowerVersion);
    }

    // The service index gives the address of the flat container, and the flat container gives the
    // versions of a package. A package with no versions gives 404.
    using var index = JsonDocument.Parse(await client.GetStringAsync(source));
    var baseAddress = index.RootElement.GetProperty("resources")
        .EnumerateArray()
        .Where(resource => resource.GetProperty("@type").GetString() == "PackageBaseAddress/3.0.0")
        .Select(resource => resource.GetProperty("@id").GetString())
        .FirstOrDefault()
        ?? throw new Exception($"The service index at {nugetSource} has no PackageBaseAddress/3.0.0 resource.");

    var versionsAddress = new Uri(new Uri(baseAddress.TrimEnd('/') + "/"), $"{lowerId}/index.json");
    using var response = await client.GetAsync(versionsAddress);
    if (response.StatusCode == HttpStatusCode.NotFound)
    {
        return false;
    }

    response.EnsureSuccessStatusCode();

    using var versions = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    return versions.RootElement.GetProperty("versions")
        .EnumerateArray()
        .Any(listed => string.Equals(listed.GetString(), lowerVersion, StringComparison.OrdinalIgnoreCase));
}

// dotnet nuget push puts a package at the top of a folder feed, with the case of its ID. Thus, this
// compares the name with no case, which also holds on a file system that has case.
bool FolderFeedHas(string lowerId, string lowerVersion)
{
    var folder = MakeAbsolute(Directory(nugetSource)).FullPath;
    if (!System.IO.Directory.Exists(folder))
    {
        throw new Exception($"The feed {nugetSource} is not a URL, and no folder of that name exists.");
    }

    var name = $"{lowerId}.{lowerVersion}.nupkg";
    return System.IO.Directory.EnumerateFiles(folder, "*.nupkg")
        .Any(file => string.Equals(System.IO.Path.GetFileName(file), name, StringComparison.OrdinalIgnoreCase));
}

// nuget.org accepts a package whose dependency it does not have, and nobody can replace a version
// on it. The tag build of a package can push before the tag build of its dependency, or the build
// of the dependency can fail. Thus, before the push, this check reads the dependencies from the
// package itself, and it does not use the tags. Each dependency that is a package of this repository
// must be on the feed at the lowest version of its range. The publish job has no history, and this
// check needs none.
Task("Verify-Published-Dependencies")
    .Description("Fails when the package to publish depends on a package here that is not on the feed yet.")
    .Does(async () =>
{
    var tag = ReleaseTag();
    if (string.IsNullOrEmpty(tag))
    {
        Information("Not a tag build, and no --tag was given - nothing to check.");
        return;
    }

    var (packageId, _) = PackageOfTag(tag, PackagesByPrefix());
    Information("Tag '{0}' releases {1}.", tag, packageId);

    var package = PackageFileOf(packageId);
    var ours = new HashSet<string>(PackagesByPrefix().Values.Select(p => p.Id), StringComparer.OrdinalIgnoreCase);
    var dependencies = DependenciesOf(package).Where(dependency => ours.Contains(dependency.Id)).ToList();

    using var client = new HttpClient();
    var missing = new List<string>();
    foreach (var (id, version) in dependencies)
    {
        if (await IsOnFeed(client, id, version))
        {
            Information("{0} {1} is on {2}.", id, version, nugetSource);
        }
        else
        {
            missing.Add($"{id} {version}");
        }
    }

    if (missing.Count > 0)
    {
        throw new Exception(
            $"{package.GetFilename()} depends on packages of this repository that are not on {nugetSource} yet:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, missing.Select(entry => "  " + entry)) + Environment.NewLine +
            "Nothing was pushed. Wait until the feed lists each one, then run the failed jobs of this run " +
            "again. RELEASING.md gives the steps.");
    }

    if (dependencies.Count == 0)
    {
        Information("{0} depends on no package of this repository.", package.GetFilename());
    }
    else
    {
        Information(
            "{0} depends on {1} package(s) of this repository, and the feed has each one.",
            package.GetFilename(),
            dependencies.Count);
    }
});

Task("Publish")
    .Description("Pushes the one package this build's tag names to nuget.org.")
    .IsDependentOn("Verify-Published-Dependencies")
    .Does(() =>
{
    // Gated on a tag, and a tag build publishes exactly the one package its tag names, so the order
    // packages reach nuget.org is the order their tags are pushed. Verify-Published-Dependencies
    // refuses a package that is early. RELEASING.md gives the order.
    var tag = ReleaseTag();
    if (string.IsNullOrEmpty(tag))
    {
        Information("Not a tag build, and no --tag was given - skipping NuGet push.");
        return;
    }

    // A rehearsal pushes to a folder only. From a machine, a push to a URL is a real release, and
    // nobody can replace a version on nuget.org.
    if (!IsTagBuild() && FeedUrl() != null)
    {
        throw new Exception(
            $"--tag rehearses a release, and a rehearsal pushes only to a folder, not to {nugetSource}. " +
            "Give --nuget-source=<folder>. A release pushes its tag, and GitHub Actions publishes it.");
    }

    // Minted by the NuGet login step in build.yml through trusted publishing; nothing here stores a
    // key. Thrown rather than skipped: a tag build that quietly released nothing is the failure
    // hardest to notice. A push to a folder needs no key.
    var apiKey = EnvironmentVariable("NUGET_API_KEY");
    if (IsTagBuild() && string.IsNullOrEmpty(apiKey))
    {
        throw new Exception(
            "NUGET_API_KEY is not set. It is minted by the NuGet login step in "
            + ".github/workflows/build.yml, which exchanges this run's OIDC token for a short-lived "
            + "key. An empty value means that exchange did not happen or was refused - check that a "
            + "trusted publishing policy for this repository and workflow exists on nuget.org, owned "
            + "by the MorseCodeSoftware organization.");
    }

    // Verify-Release-Order already failed for a tag that names no package or more than one, and its
    // failure skips this job. Checked again, so that this target alone never pushes the wrong package or
    // quietly pushes none.
    var (packageId, _) = PackageOfTag(tag, PackagesByPrefix());
    Information("Tag '{0}' releases {1}.", tag, packageId);

    var package = PackageFileOf(packageId);
    Information("Pushing {0} to {1}", package.GetFilename(), nugetSource);

    // --skip-duplicate makes re-running a tag build a no-op for a package it already pushed.
    DotNetNuGetPush(
        package.FullPath,
        new DotNetNuGetPushSettings
        {
            ApiKey = string.IsNullOrEmpty(apiKey) ? null : apiKey,
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
