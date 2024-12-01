# Publishing

## OkTools.Core

* Edit `Core.csproj` and bump the `PackageVersion`
* Run `pre-publish.ps1 -Core` to build and test.
* Send it to GitHub
  * `git commit/reset` and get to a clean state
  * `git push`, wait for https://github.com/scottbilas/OkTools/actions to be green (this runs the "Validate Dev Branch" action)
  * `git tag release-core-$version` where `$version` is what was set in the .csproj above
  * `git push --tags` (this runs the "Publish NuGet Package" action)

If there are no errors, publishing the new version to the Nuget Gallery should happen in about 5 minutes.

## OkTools.Terminal

Same as OkTools.Core except replace Core with Terminal and use tag release-terminal-$version.
