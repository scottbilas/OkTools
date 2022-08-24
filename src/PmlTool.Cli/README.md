# PML Baker

## Problem

[Process Monitor](https://docs.microsoft.com/en-us/sysinternals/downloads/procmon) is awesome but has a few limitations regarding stack captures:

* PML (procmon-log) files are not portable, because symbolication is done at query time by the app, not capture time. Full symbols must be available to any user trying to query a PML.
* Ok, well there _is_, but it's **XML**, and this is absurdly verbose. Long captures can just forget about it.
* Mono stacks cannot be symbolicated. Unity users care about this.

## Solution

`pmlbaker` improves this by creating a sidecar `.pmlbaked` file that adds symbols (both native and mono-jit) for all frames, in a more compact format. The original PML plus this `.pmlbaked` file can be considered a portable capture log.

An alternative I considered was modifying [ProcMonXv2](https://github.com/zodiacon/ProcMonXv2) as it's open source, but it has some deal-breaking bugs and is really distant from procmon in terms of rich functionality (particularly the summary features).

The best solution would be for Microsoft to put procmon up on Github. As a bonus, we could help fix its crash bugs. :)

## Usage

The workflow becomes:

1. Run a capture as normal
2. If using Unity, be sure to a) run with env var UNITY_MIXED_CALLSTACKS=1 and b) copy `%TEMP%\pmip*` to a safe location before the Unity.exe process terminates.
3. Save the procmon log to a PML if using virtual memory backing
4. `pmlbaker` the PML and pmip to get a `.pmlbaked` file

Now, tooling that is working against the PML (or a CSV) can use the PmlQuery class to run simple queries on the `.pmlbaked` file. Or parse that file directly, it's a straightforward format.

For a head start on working with PML files, see [procmon-parser](https://github.com/eronnen/procmon-parser). I recommend looking at _both_ the [PML Format.md](https://github.com/eronnen/procmon-parser/blob/master/docs/PML%20Format.md) and the Python source when assembling your own processor.
