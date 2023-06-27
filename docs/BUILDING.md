# Building MSBuild Project Tools language server

You'll need:

1. .NET 6.0.0 or newer

To build:

1. `powershell build.ps1`

Debugging:

There are 2 main ways of debugging this LSP:
- Debugging tests: Just debug a test via your IDE, nothing special here
- Debugging as part of VS Code extension:
  1. Clone [extension repo](https://github.com/tintoy/msbuild-project-tools-vscode) for this LSP
  2. Follow [the guide](https://github.com/tintoy/msbuild-project-tools-vscode/blob/master/docs/BUILDING.md) and start debugging that extension
  3. After new VS Code window appeared, choose `Attach to LSP process` debug configuration and manually attach to LSP process, spawned by extension
  4. Now you can trigger various LSP calls via VS Code window (the one that appeared after you started debugging extension) and hit your breakpoints in LSP code

Note that in the second setup you don't work with this repo directly, but through git submodules system in extension repo