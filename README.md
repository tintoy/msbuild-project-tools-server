# MSBuild project file tools

An [LSP](https://github.com/Microsoft/language-server-protocol)-compatible language service that provides intellisense for MSBuild project files, including auto-complete for `<PackageReference>` elements.

For more information, see [msbuild-project-tools-vscode](https://github.com/tintoy/msbuild-project-tools-vscode).

You need the .NET Core runtime v3.0.0 (or SDK v3.0.1xx) or newer installed to use the language service (but your projects can target any version you have installed).

## Building from source

See [BUILDING.md](docs/BUILDING.md).

## Design

See [architectural overview](docs/architecture/overview.md) for details (this is a work-in-progress; if you have questions, feel free to create an issue).

## Limitations

* Limited intellisense is available for dynamic `PropertyGroup` / `ItemGroup` declarations (i.e. those appearing inside a `Target` element); these are only evaluated when the project is built and so very little information about them is available to us when statically evaluating the project (see [tintoy/msbuild-project-tools-server#5](https://github.com/tintoy/msbuild-project-tools-server/issues/5#issuecomment-383352512) for details).
* Support for task completions is experimental; if you find a problem with it, please [create an issue](https://github.com/tintoy/msbuild-project-tools-server/issues/new).
* Support for MSBuild expressions is experimental; if you find a problem with it, please [create an issue](https://github.com/tintoy/msbuild-project-tools-server/issues/new).
* If you open more than one project at a time (or navigate to imported projects), subsequent projects will be loaded into the same MSBuild project collection as the first project. Once you have closed the last project file, the next project file you open will become the master project. The master project will become selectable in a later release.

## Questions / bug reports

If you have questions, feedback, feature requests, or would like to report a bug, please feel free to reach out by creating an issue. When reporting a bug, please try to include as much information as possible about what you were doing at the time, what you expected to happen, and what actually happened.

If you're interested in collaborating that'd be great, too :-)
