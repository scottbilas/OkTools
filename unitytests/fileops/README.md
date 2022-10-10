# fileops (test Unity project)

This will generate the test data for ProcMonUtils.Tests.

To run:

```pwsh
cd $reporoot
. ./setenv.ps1 # run dotnet build and set aliases

rm -rf unitytests/fileops/Library
ucapture unitytests/fileops # run the tests
```

Then...

* Open `events.pml` in procmon, filter for paths ending in `fileops.cs`.
* Save-as a new PML to `events.pml`, selecting "events displayed using current filter". (This will make the file a lot smaller and just what we want to test.)
* `del tests/ProcMonUtils.Tests/testdata/*`
* `copy unitytests/fileops/Temp/procmon/* tests/ProcMonUtils.Tests/testdata/`
