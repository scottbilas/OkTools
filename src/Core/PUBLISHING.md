# Publishing NiceIO

Do this:

* Edit `Core.csproj` and bump the `PackageVersion`
* `dotnet test src/Core/Core-Tests.csproj --nologo`
* Send it to GitHub
  * `git commit/reset` and get to a clean state
  * `git tag release-$version` where `$version` is what was set in the .csproj above
  * `git push --tags`

If there are no errors, publishing the new version to the Nuget Gallery should happen in about 5 minutes.
