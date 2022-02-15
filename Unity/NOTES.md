# Notes

## Services

* Query running Unity processes - extract command line args, working dir, version, project path, whether there are child instances (like profiler and background importer) and their PID's etc.
* Be able to ask for running Unity processes associated with a certain projectpath or unity build.

## Ideas

Sure would be nice to have a "CLI Helper" nuget that does all kinds of useful terminal stuff:

* File globbing utilities
* Config system
  * Cascading git style INI files
  * CLI override
  * `@profile` and micro scripting language (INI as command set)
  * Docopt support
  * Overlays such as for CLI reapplied on top of an INI file change (i.e. nondestructive merging)
  * Schema generation so we can have similar to vscode settings experience
  * Do everything with sourcegen support so we get type safety, autocomplete, self-documenting (easy schema gen)
* Dumping abilities including to JSON and YAML
* Efficient subprocess launching and stdin/out/err routing 
* NiceIO++
* Logging w/ rotation
* VCS-awareness

Extended library that adds support for when we're staying resident, such as when used from a service or GUI/TUI.

* Auto refresh of config by watching for changes to cascading INI file changes


## Frustrations with Libraries

### Docopt.net

Problems:

* Exception-based control flow is WTF
* No context is given when the user gives bad args (it just dumps the usage section). This is super annoying.."what did I do wrong??" "who knows..".
* Too frameworky, needs to be a toolkit with a framework on top. I want access to low level utilities, but they're all internal.
* Hard coded help and version is bad ("do it my way or I'll throw!")
* Use of old school `ArrayList` and then the argvalue stuff given is really janky.
* t4-based gen was removed before sourcegen was implemented (and currently the sourcegen version shows little sign of life)
* Doesn't know how to reflow doc content, important for variable width terminals (otherwise user has to pick a wrap column).
* Parsing of doc happens every time. Should test perf to see how bad it is with a complex grammar. Better to codegen the parser..
* No support for inline markup, such as "trim before here" or "this is a wrap point" or color/bold/etc. codes.
* Impossible to debug when there's a grammar problem. You just hack over and over trying to get the right brackets and ... and options specs, then look at the dictionary that comes back.. A dump grammar can be a stopgap until codegen types are implemented.
* No support for a flag set to false vs a default (missing) flag. (For example `--flag` -> `true`, `--flag:false` (also `0` or `n` or `f` in place of `false`) -> `false`, (missing) -> `null`) otherwise cannot override a default set in a config file

Nice:

[Try DocOpt](try.docopt.org) yay!

Future:

* `dynamic` might be a better fit than, or complementary to, the dictionary approach. Worth experimenting with.

### Microsoft.Extensions.FileSystemGlobbing

* Minimal grammar support. No `*.{jpg,png}` type stuff for example, or env vars.
* Does not support absolute paths. Forced to split the pattern and do a separate query.
* Cannot find dirs, only files.

Alternative: Meziantou.Framework.Globbing

...but couldn't get it to work, at least in LINQPad. Didn't pursue it further.

### DotNetConfig

Basic issue is that the tooling is (as it says on the tin) geared towards people writing tools for the dotnet tooling ecosystem. I'm not doing that. I want something that feels like gitconfig but with my own name and following more XDG-ish convention.

Problems:

* Hard coded name `.netconfig`, whereas I want something like `.oktools`. I can hand it the specific config path I want to use, but it still does its parent/global aggregation with `.netconfig`.
* Can't reuse any types to do (for example) my own aggregation because they're internal.
* Aggregation always happens, no control over it
* Cannot override the global/system locations, which are Windowsy but not XDG friendly
* Doesn't seem to have any way to feed back about errors, like using unrecognized escape sequences (it just ignores them) which is easy to do accidentally when using windows style paths.  
