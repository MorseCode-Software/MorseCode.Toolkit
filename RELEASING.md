# Releasing

Each package in this repository releases on its own, from a tag. The tag is the project's
`MinVerTagPrefix` followed by the version, such as `morsecode-mvvm-0.3.0`. Pushing the tag starts
the Build workflow. Its Publish job pushes that one package to nuget.org, and only that package.

A version on nuget.org can't be replaced or deleted. So the order of the tags matters, and so does
the way they're pushed.

## Steps

1. **Find what has to release with the package.** Run the release-order check for the tag you mean
   to push, on the commit you mean to tag:

   ```bash
   dotnet cake --target=Verify-Release-Order --tag=morsecode-controls-ui-avalonia-0.3.0
   ```

   If a package that it compiles against changed after its own last tag, the check fails and lists
   those packages in the order to release them. Release them first, starting at the top of the list.

2. **Tag the commit**, on `main`:

   ```bash
   git tag morsecode-stagedconstruction-0.3.0 <commit>
   ```

3. **Push one tag at a time:**

   ```bash
   git push origin morsecode-stagedconstruction-0.3.0
   ```

   Don't push several tags in one push. GitHub starts no workflow at all when more than three tags
   arrive in one push, so nothing builds and nothing publishes, with no error. Up to three tags in
   one push build at the same time, in no particular order.

4. **Wait until nuget.org lists the version**, not just until the Publish job is green. nuget.org
   validates and indexes a package after it accepts it, which usually takes less than 15 minutes.
   The version is listed when it appears at
   `https://api.nuget.org/v3-flatcontainer/<package id in lowercase>/index.json`.

5. **Push the next tag**, and repeat from step 4 until the last package is out.

## When the check stops a release

The Publish job's first step, "Verify dependencies are published", reads the package it's about to
push. If the package depends on a package from this repository at a version that nuget.org doesn't
list yet, the step fails and nothing is pushed. This happens when a tag was pushed before its
dependency was listed, or when the dependency's own run failed.

Once nuget.org lists the dependency, open the failed run and choose **Re-run failed jobs**. That
runs the Publish job again, and the job pushes the package that the run already built. If the new
attempt can't download that package, **Re-run all jobs** builds it again from the same tag, at the
same version.

## After a release

The samples reference published packages at a pinned version. Moving them to the new version is a
change of its own, made in a pull request with the lock files that the restore rewrites.
