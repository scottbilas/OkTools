# OkTools

Various small CLI tools and libraries bundled together for convenience.

[![validate](https://github.com/scottbilas/OkTools/actions/workflows/validate.yml/badge.svg)](https://github.com/scottbilas/OkTools/actions/workflows/validate.yml)

## Building

`dotnet build` should do it. Currently requires .NET 8 (7 in a couple cases). Only tested on Windows. There is some platform-specific

Perf testing: `dotnet run -c Release --project perftests/Core.PerfTests/Core.PerfTests.csproj -- --filter * --disasm --artifacts artifacts/perf/Core.PerfTests`

## OkTools.Core

A pile of useful C# utilities published as NuGet [package](https://www.nuget.org/packages/OkTools.Core/). I update this fairly regularly with new toys.

## loggo (Loggo.Cli) 

A generator for log output. Nice for testing tools that sublaunch cli apps and redirect their stdin/out like a command shell. Finished, with occasional tweaks added.

[CLI docs](src/Loggo.Cli/LoggoCli.docopt.txt)

## pmltool (PmlTool.Cli)

Symbolicator, query, and conversion tool for Process Monitor ([procmon](https://docs.microsoft.com/en-us/sysinternals/downloads/procmon)) files. Stable but capabilities are limited to what kind of analysis I'm doing on any given day.

[README](src/PmlTool.Cli/README.md)

[CLI docs](src/PmlTool.Cli/PmlToolCli.docopt.txt)

## showkeys (Showkeys.Cli)

Showkeys-ish for Windows. Prints virtual terminal sequences for keypresses. Useful when debugging TUI apps on Windows. Finished, but I drop in to tweak it sometimes.

[CLI docs](src/Showkeys.Cli/Program.cs#9)

## stale (Stale.Cli)

A utility that will run cli apps, capture output, monitor, filter, show live status. I want to be able to have "sticky" error messages while a given build log or whatever continues to scroll.

[CLI docs](src/Stale.Cli/StaleCli.docopt.txt)

## okunity (Unity.Cli)

Toolkit of helpful stuff for working with Unity from the command line. For example killing the Hub and prevent it from coming up automatically, or running Unity with a project-local timestamp-rotated log file. Fairly stable.

## On Pause

### lyfe (Lyfe.Cli)

I'm having trouble finding time to get this thing off the ground, but the idea is to have a basic machine process manager that looks at desired target state of hardware (like "long plane flight" or "docked at desk") and will start/stop system processes and services accordingly.

This is an attempt to deal with the general inability of Windows programmers to write background-run software that actually idles (0% CPU). I like my laptop battery going as far as possible and there are endless annoying but useful vampires I want to shift in and out.

### okflog (Flog.Cli)

TUI app. A progressive filtering `tail` + `less` for incremental analysis. On hold while I work on Stale.

[CLI docs](src/Flog.Cli/FlogCli.docopt.txt)

