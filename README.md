# OkTools

These tools are ok.

[![validate](https://github.com/scottbilas/OkTools/actions/workflows/validate.yml/badge.svg)](https://github.com/scottbilas/OkTools/actions/workflows/validate.yml)

## Building

`dotnet build` should do it. Currently requires .NET 6. Only tested on Windows, and some tools will always be Windows-only (like okunity).

Perf testing: `dotnet run -c Release --project perftests/Core.PerfTests/Core.PerfTests.csproj -- --filter * --disasm --artifacts artifacts/perf/Core.PerfTests`

## okflog (Flog.Cli)

TUI app. A progressive filtering `tail` + `less` for incremental analysis. Very early in development.

[CLI docs](src/Flog.Cli/FlogCli.docopt.txt)

## loggo (Loggo.Cli) 

A generator for log output. Nice for testing okflog. Finished.

[CLI docs](src/Loggo.Cli/LoggoCli.docopt.txt)

## pmltool (PmlTool.Cli)

Symbolicator, query, and conversion tool for Process Monitor ([procmon](https://docs.microsoft.com/en-us/sysinternals/downloads/procmon)) files. Stable but capabilities are limited to what kind of analysis I'm doing on any given day.

[README](src/PmlTool.Cli/README.md)

[CLI docs](src/PmlTool.Cli/PmlToolCli.docopt.txt)

## showkeys (Showkeys.Cli)

Showkeys for Windows. Virtual terminal sequences. Useful when debugging TUI apps on Windows. Finished.

## okunity (Unity.Cli)

Toolkit of helpful stuff for working with Unity from the command line. Fairly stable.
