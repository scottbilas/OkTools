# Publishing OkTools.Core

Do this:

* Edit `Core.csproj` and bump the `PackageVersion`
* `dotnet build src/Core/Core.csproj --nologo -c Release && dotnet test src/Core/Core-Tests.csproj --nologo`
* Send it to GitHub
  * `git commit/reset` and get to a clean state
  * `git push`, wait for https://github.com/scottbilas/OkTools/actions to be green (this runs the "Validate Dev Branch" action)
  * `git tag release-$version` where `$version` is what was set in the .csproj above
  * `git push --tags` (this runs the "Publish NuGet Package" action)

If there are no errors, publishing the new version to the Nuget Gallery should happen in about 5 minutes.
