global using OkTools.Terminal.Extensions;

global using SimpleTextMacro = (string macroName, string replacement);
global using SimpleMacroWriter = (string macroName, System.Action<OkTools.Terminal.CliContext, System.IO.TextWriter> writeAction);
