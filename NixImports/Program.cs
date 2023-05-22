// See https://aka.ms/new-console-template for more information

using AsmResolver.DotNet;
using NixImports;

Console.WriteLine("NixImports by dr4k0nia - https://github.com/dr4k0nia");
var maker = new NameMaker(ModuleDefinition.FromFile(args[0]));
maker.Run();
