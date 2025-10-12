# MSBuild Project Tools Language Server - AI Coding Instructions

This is a **Language Server Protocol (LSP)** implementation for MSBuild project files, providing IntelliSense, completions, and diagnostics for `.csproj`, `.props`, and `.targets` files.

## Architecture Overview

### Layered Design
The server is organized in distinct layers (see `docs/architecture/overview.md`):

1. **Protocol Layer** (`LanguageServer`)
   - Entry point: `src/LanguageServer/Program.cs`
   - Uses OmniSharp's LSP implementation via STDIN/STDOUT
   - Autofac for DI, Serilog for logging
   - Modules: `LoggingModule`, `LanguageServerModule`

2. **Document Management** (`LanguageServer.Engine/Documents`)
   - `Workspace`: Manages all projects for a workspace
   - `ProjectDocument` (abstract): Base for project state
     - `MasterProjectDocument`: Primary project
     - `SubProjectDocument`: Imported/referenced projects
   - **Critical**: All document state protected by `AsyncReaderWriterLock` - callers must acquire locks before access

3. **Handlers** (`LanguageServer.Engine/Handlers`)
   - `DocumentSyncHandler`: Triggers on `textDocument/didOpen` to load projects
   - `CompletionHandler`: Orchestrates multiple completion providers in parallel
   - Completion merging: Returns `null` only if ALL providers return `null`

4. **Completion Providers** (`LanguageServer.Engine/CompletionProviders`)
   - Auto-registered via Autofac: all `CompletionProvider` subclasses
   - Examples: `PackageReferenceCompletionProvider`, `PropertyElementCompletionProvider`, `TaskElementCompletionProvider`
   - Base class: `CompletionProvider` / interface: `ICompletionProvider`

5. **Semantic Models**
   - `LanguageServer.SemanticModel.Xml`: XML syntax navigation (`XmlLocator`, `XSElement`)
   - `LanguageServer.SemanticModel.MSBuild`: MSBuild-specific models (`MSBuildObjectLocator`, `MSBuildProperty`, `MSBuildTarget`)
   - Built on: `Microsoft.Language.Xml` + `Microsoft.Build.Construction/Evaluation`

### Key Dependencies
- **MSBuild SDK**: Uses `Microsoft.Build` and `Microsoft.Build.Locator`
- **NuGet**: `NuGet.Protocol` for package source querying
- **LSP**: OmniSharp.Extensions.LanguageServer
- **Parsing**: `Microsoft.Language.Xml` for syntax, `Sprache` for MSBuild expression parsing

## Development Workflows

### Building
```powershell
# Standard build
.\build.ps1

# Using task runner
dotnet build MSBuildProjectTools.sln
```

Output directory: `out/language-server/`

### Testing
- Framework: **xUnit**
- Test project: `test/LanguageServer.Engine.Tests/`
- Attributes: `[Fact]`, `[Theory]`
- Pattern: Tests use `MSBuildEngineFixture` collection for MSBuild initialization
- Example: `MSBuildObjectLocatorTests.cs` demonstrates position-based lookup testing

### Debugging
Two approaches (see `docs/BUILDING.md`):
1. **Test debugging**: Standard IDE debugging
2. **Extension debugging**: 
   - Clone companion [extension repo](https://github.com/tintoy/msbuild-project-tools-vscode)
   - Start extension debugger
   - Attach to LSP process via "Attach to LSP process" configuration

## Project Conventions

### .NET Configuration
- **Target Framework**: `net8.0` (set in root `Directory.Build.props`)
- **SDK Version**: 8.0.100 (`global.json`)
- **Central Package Management**: Enabled via `Directory.Packages.props`
- All projects in `src/` import parent `Directory.Build.props` for doc generation

### Code Style
- **EditorConfig**: 4-space indents (2 for XML/YAML), `crlf` line endings
- **Var usage**: Use `var` for built-in types and apparent types (warning-level enforcement)
- **Case blocks**: Indent case contents, not when block present

### Naming Patterns
- Handlers: `*Handler.cs` in `Handlers/` (e.g., `CompletionHandler`)
- Providers: `*Provider.cs` or `*CompletionProvider.cs` in `CompletionProviders/`
- Semantic models: `MSBuild*` prefix for MSBuild objects, `XS*` for XML syntax
- Test files: `*Tests.cs` (e.g., `MSBuildObjectLocatorTests`)

## Important Patterns

### Document State Access
Always use the lock pattern when accessing `ProjectDocument`:
```csharp
using (await projectDocument.Lock.ReaderLockAsync())
{
    // Read document state
}

using (await projectDocument.Lock.WriterLockAsync())
{
    // Modify document state
}
```

### Completion Provider Registration
Providers auto-register via reflection in `LanguageServerModule`:
```csharp
builder.RegisterAssemblyTypes(ThisAssembly)
    .Where(type => type.IsSubclassOf(completionProviderType) && !type.IsAbstract)
    .As<CompletionProvider>()
    .As<ICompletionProvider>();
```
Just inherit from `CompletionProvider` - no manual registration needed.

### MSBuild Project Loading
Projects can be "cached" (`IsMSBuildProjectCached = true`) when XML is invalid - original MSBuild project retained but positions may mismatch. Check this flag before using locators.

### Help/Schema Data
Static JSON files in `help/`: `properties.json`, `elements.json`, `tasks.json`, `items.json`
- Provide IntelliSense descriptions for MSBuild primitives
- Loaded by completion providers for contextual help

## Common Pitfalls

1. **MSBuild Environment**: `Program.cs` ensures `DOTNET_ROOT` is set if `DOTNET_HOST_PATH` exists (critical for MSBuild discovery)
2. **Dynamic Properties**: Limited intellisense for properties/items inside `<Target>` elements (only evaluated at build time)
3. **Master Project**: First opened project becomes master; subsequent projects share the same `ProjectCollection`
4. **NuGet Auth**: Non-interactive credential provider setup in `ConfigureNuGetCredentialProviders()`

## Extension Points

To add new completion scenarios:
1. Create class inheriting `CompletionProvider` in `CompletionProviders/`
2. Override `ProvideCompletionsAsync()` method
3. Return `CompletionList` or `null` (auto-registered via DI)

To add new LSP handlers:
1. Create handler in `Handlers/` inheriting OmniSharp handler interfaces
2. Register in `LanguageServerModule.Load()` as `Handler`
3. Add to language server in `OnActivated` hook
