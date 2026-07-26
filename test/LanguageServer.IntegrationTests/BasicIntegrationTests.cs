using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Serilog.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace MSBuildProjectTools.LanguageServer.IntegrationTests
{
    using CustomProtocol;
    using Utilities;

    public class BasicIntegrationTests(ITestOutputHelper testOutput) : IntegrationTestBase(testOutput), IAsyncLifetime
    {
        static readonly JsonSerializerSettings DumpSerializerSettings = new JsonSerializerSettings
        {
            Converters =
            {
                new StringEnumConverter(),
            },
            Formatting = Formatting.Indented,
        };

        private readonly LanguageServerFixture _fixture = new(false);
        private readonly TempDirectory _workspaceRoot = new();

        public async Task InitializeAsync()
        {
            var loggerProvider = new SerilogLoggerProvider(
                Log.ForContext(GetType())
            );
            await _fixture.StartAsync(_workspaceRoot, loggerProvider);
        }

        public async Task DisposeAsync()
        {
            await _fixture.StopAsync();
            _workspaceRoot.Dispose();
        }

        /// <summary>
        ///     Test that the language server can respond to server capabilities request.
        /// </summary>
        [Fact]
        public void ServerProvidesCapabilities()
        {
            Assert.NotNull(_fixture.Client);
            Assert.NotNull(_fixture.Client.ServerSettings);
            Assert.NotNull(_fixture.Client.ServerSettings.Capabilities);
        }

        /// <summary>
        ///     Test that a specific server capability (CompletionProvider) is available.
        /// </summary>
        [Fact]
        public void ServerProvidesStaticCompletionHandler()
        {
            Assert.NotNull(_fixture.Client);
            Assert.NotNull(_fixture.Client!.ServerSettings?.Capabilities?.CompletionProvider);
            Assert.DoesNotContain(_fixture.Client!.RegistrationManager?.CurrentRegistrations,
                reg => reg.Method == TextDocumentNames.Completion);
        }

        [Fact]
        public async Task DocumentSyncCsproj()
        {
            var testFilePath = Path.Combine(_workspaceRoot, "Test.csproj");
            await File.WriteAllTextAsync(testFilePath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net6.0</TargetFramework>
                </PropertyGroup>  
            </Project>
            """);

            var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var busyNotificationBuffer = new ConcurrentQueue<BusyNotificationParams>();
            using var receivedNotification = new ManualResetEventSlim(initialState: false);

            using IDisposable notificationHandlerRegistration = _fixture.Client.Register(registry =>
            {
                registry.OnNotification<BusyNotificationParams>("msbuild/busy", notification =>
                {
                    busyNotificationBuffer.Enqueue(notification);
                    receivedNotification.Set();
                });
            });

            await _fixture.Client.SendRequest(new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    Uri = DocumentUri.FromFileSystemPath(testFilePath),
                },
            }, timeout.Token);


            receivedNotification.Wait(timeout.Token);

            BusyNotificationParams[] actualBusyNotifications = busyNotificationBuffer.ToArray();
            Log.Information("Actual busy-notifications: {NotificationJson:l}",
                JsonConvert.SerializeObject(actualBusyNotifications, DumpSerializerSettings)
            );

            Assert.Collection(actualBusyNotifications,
                notification1 =>
                {
                    Assert.True(notification1.IsBusy);
                    Assert.Equal("Loading...", notification1.Message);
                },
                notification2 =>
                {
                    Assert.False(notification2.IsBusy);
                    Assert.Equal("Project loaded.", notification2.Message);
                }
            );
        }

        [Fact]
        public async Task AutoCompleteCsproj()
        {
            var testFilePath = Path.Combine(_workspaceRoot, "Test.csproj");
            await File.WriteAllTextAsync(testFilePath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net6.0</TargetFramework>
                </PropertyGroup>  
            </Project>
            """);

            var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            CompletionList completionList = await _fixture.Client.SendRequest(new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = DocumentUri.FromFileSystemPath(testFilePath)
                },
                Position = new(4, 6)
            }, timeout.Token);

            Assert.NotNull(completionList);
            Assert.NotNull(completionList.Items);

            CompletionItem[] completionItems = completionList.Items.OrderBy(item => item.SortText ?? item.Label).ToArray();

            Log.Information("Received {CompletionCount} completions from the language server.", completionItems.Length);
            for (int itemIndex = 0; itemIndex < completionItems.Length; itemIndex++)
            {
                Log.Information("\tCompletionItems[{ItemIndex}] = {@CompletionItem}",
                    itemIndex,
                    completionItems[itemIndex]
                );
            }

            Assert.NotEmpty(completionItems);
            Assert.Equal(
                expected: [
                    "<!-- -->",
                    "<Import>",
                    "<ItemGroup>",
                    "<PropertyGroup>",
                    "<Target>",
                ],
                actual: completionItems.Select(item => item.Label)
            );
        }

        [Fact]
        public async Task HoverCsproj()
        {
            var testFilePath = Path.Combine(_workspaceRoot, "Test.csproj");
            await File.WriteAllTextAsync(testFilePath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net6.0</TargetFramework>
                </PropertyGroup>  
            </Project>
            """);

            var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Hover hoverResult = await _fixture.Client.SendRequest(new HoverParams
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = DocumentUri.FromFileSystemPath(testFilePath)
                },
                Position = new Position(3, 9).ToLsp()
            }, timeout.Token);

            Assert.NotNull(hoverResult);
            Assert.NotNull(hoverResult.Contents);
            Assert.Equal(
                "Property: `OutputType` Type of output to generate (WinExe, Exe, or Library) Value: `Exe`",
                hoverResult.Contents.ToString()
            );
        }

        [Fact]
        public async Task DefinitionCsproj()
        {
            var testFilePath = Path.Combine(_workspaceRoot, "Test.csproj");
            await File.WriteAllTextAsync(testFilePath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net6.0</TargetFramework>
                </PropertyGroup>  
            </Project>
            """);

            MSBuildEngineInstance compatibleMSBuild = MSBuildHelper.FindEngineForTargetFrameworkVersion(_fixture.TargetFrameworkVersion, logger: Log);
            Assert.NotNull(compatibleMSBuild);

            var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            LocationOrLocationLinks definitionResult = await _fixture.Client.SendRequest(new DefinitionParams
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = DocumentUri.FromFileSystemPath(testFilePath)
                },
                Position = new Position(1, 15).ToLsp()
            }, timeout.Token);

            Assert.NotNull(definitionResult);

            LocationOrLocationLink[] expectedDefinitions = [
                new LocationOrLocationLink(
                    new Location
                    {
                        Uri = DocumentUri.FromFileSystemPath(
                            Path.Combine(
                                compatibleMSBuild.GetSdkImportDirectory("Microsoft.NET.Sdk"),
                                "Sdk.props"
                            )
                        ),
                        Range = new Range(
                            start: new Position(1, 1),
                            end: new Position(1, 1)
                        ).ToLsp()
                    }
                ),
                new LocationOrLocationLink(
                    new Location
                    {
                        Uri = DocumentUri.FromFileSystemPath(
                            Path.Combine(
                                compatibleMSBuild.GetSdkImportDirectory("Microsoft.NET.Sdk"),
                                "Sdk.targets"
                            )
                        ),
                        Range = new Range(
                            start: new Position(1, 1),
                            end: new Position(1, 1)
                        ).ToLsp()
                    }
                ),
            ];
            Log.Information("Expected definitions: {DefinitionJson:l}",
                JsonConvert.SerializeObject(expectedDefinitions, DumpSerializerSettings)
            );

            LocationOrLocationLink[] actualDefinitions = definitionResult.ToArray();
            Log.Information("Actual definitions: {DefinitionJson:l}",
                JsonConvert.SerializeObject(actualDefinitions, DumpSerializerSettings)
            );

            Assert.Equal(expectedDefinitions, actualDefinitions);
        }

        [Fact]
        public async Task SymbolsCsproj()
        {
            var testFilePath = Path.Combine(
                Path.GetFullPath(_workspaceRoot),
                "Test.csproj"
            );
            await File.WriteAllTextAsync(testFilePath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net6.0</TargetFramework>
                </PropertyGroup>  
            </Project>
            """);

            MSBuildEngineInstance compatibleMSBuild = MSBuildHelper.FindEngineForTargetFrameworkVersion(_fixture.TargetFrameworkVersion, logger: Log);
            Assert.NotNull(compatibleMSBuild);

            var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            SymbolInformationOrDocumentSymbolContainer documentSymbolResult = await _fixture.Client.SendRequest(new DocumentSymbolParams
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = DocumentUri.FromFileSystemPath(testFilePath),
                },
            }, timeout.Token);

            Assert.NotNull(documentSymbolResult);

            SymbolInformation[] expectedSymbols = [
                new SymbolInformation
                {
                    Name = "Microsoft.NET.Sdk",
                    ContainerName = "Import (SDK)",
                    Kind = SymbolKind.Package,
                    Location = new Location
                    {
                        Uri = DocumentUri.FromFileSystemPath(testFilePath),
                        Range = new Range(
                            start: new Position(1, 10),
                            end: new Position(1, 33)
                        ).ToLsp(),
                    },
                },
                new SymbolInformation
                {
                    Name = "OutputType",
                    ContainerName = "Property",
                    Kind = SymbolKind.Property,
                    Location = new Location
                    {
                        Uri = DocumentUri.FromFileSystemPath(testFilePath),
                        Range = new Range(
                            start: new Position(3, 9),
                            end: new Position(3, 37)
                        ).ToLsp(),
                    },
                },
                new SymbolInformation
                {
                    Name = "TargetFramework",
                    ContainerName = "Property",
                    Kind = SymbolKind.Property,
                    Location = new Location
                    {
                        Uri = DocumentUri.FromFileSystemPath(testFilePath),
                        Range = new Range(
                            start: new Position(4, 9),
                            end: new Position(4, 50)
                        ).ToLsp(),
                    },
                },
            ];
            Log.Information("Expected symbols: {SymbolJson:l}",
                JsonConvert.SerializeObject(expectedSymbols, DumpSerializerSettings)
            );

            SymbolInformation[] actualSymbols = documentSymbolResult.Select(symbol => symbol.SymbolInformation).ToArray();
            Log.Information("Actual symbols: {SymbolJson:l}",
                JsonConvert.SerializeObject(actualSymbols, DumpSerializerSettings)
            );

            Assert.Collection(actualSymbols,
                symbol01 =>
                {
                    Assert.Equal("Microsoft.NET.Sdk", symbol01.Name);
                    Assert.Equal(SymbolKind.Package, symbol01.Kind);
                    Assert.Equal("Import (SDK)", symbol01.ContainerName);

                    Assert.Equal(testFilePath, symbol01.Location.Uri.GetFileSystemPath(), ignoreCase: true);
                    Assert.Equal(
                        new Range(
                            start: new Position(1, 10),
                            end: new Position(1, 33)
                        ),
                        symbol01.Location.Range.ToNative()
                    );
                },
                symbol02 =>
                {
                    Assert.Equal("OutputType", symbol02.Name);
                    Assert.Equal(SymbolKind.Property, symbol02.Kind);
                    Assert.Equal("Property", symbol02.ContainerName);

                    Assert.Equal(testFilePath, symbol02.Location.Uri.GetFileSystemPath(), ignoreCase: true);
                    Assert.Equal(
                        new Range(
                            start: new Position(3, 9),
                            end: new Position(3, 37)
                        ),
                        symbol02.Location.Range.ToNative()
                    );
                },
                symbol03 =>
                {
                    Assert.Equal("TargetFramework", symbol03.Name);
                    Assert.Equal(SymbolKind.Property, symbol03.Kind);
                    Assert.Equal("Property", symbol03.ContainerName);

                    Assert.Equal(testFilePath, symbol03.Location.Uri.GetFileSystemPath(), ignoreCase: true);
                    Assert.Equal(
                        new Range(
                            start: new Position(4, 9),
                            end: new Position(4, 50)
                        ),
                        symbol03.Location.Range.ToNative()
                    );
                }
            );
        }

        [Fact]
        public async Task DocumentSyncSlnx()
        {
            var testFilePath = Path.Combine(_workspaceRoot, "Test.slnx");
            await File.WriteAllTextAsync(testFilePath,
            """
            <Solution>

            </Solution>
            """);

            var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var busyNotificationBuffer = new ConcurrentQueue<BusyNotificationParams>();
            using var receivedNotification = new ManualResetEventSlim(false);

            using IDisposable notificationHandlerRegistration = _fixture.Client.Register(registry =>
            {
                registry.OnNotification<BusyNotificationParams>("msbuild/busy", notification =>
                {
                    busyNotificationBuffer.Enqueue(notification);
                    receivedNotification.Set();
                });
            });

            await _fixture.Client.SendRequest(new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    Uri = DocumentUri.FromFileSystemPath(testFilePath),
                },
            }, timeout.Token);


            receivedNotification.Wait();

            BusyNotificationParams[] actualBusyNotifications = busyNotificationBuffer.ToArray();
            Log.Information("Actual busy-notifications: {NotificationJson:l}",
                JsonConvert.SerializeObject(actualBusyNotifications, DumpSerializerSettings)
            );

            Assert.Collection(actualBusyNotifications,
                notification1 =>
                {
                    Assert.True(notification1.IsBusy);
                    Assert.Equal("Loading...", notification1.Message);
                },
                notification2 =>
                {
                    Assert.False(notification2.IsBusy);
                    Assert.Equal("Solution loaded.", notification2.Message);
                }
            );
        }

        [Fact]
        public async Task AutoCompleteSlnx()
        {
            var testFilePath = Path.Combine(_workspaceRoot, "Test.slnx");
            await File.WriteAllTextAsync(testFilePath,
            """
            <Solution>

            </Solution>
            """);

            var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            CompletionList completionList = await _fixture.Client.SendRequest(new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = DocumentUri.FromFileSystemPath(testFilePath)
                },
                Position = new(2, 1)
            }, timeout.Token);

            Assert.NotNull(completionList);
            Assert.NotNull(completionList.Items);

            CompletionItem[] completionItems = completionList.Items.OrderBy(item => item.SortText ?? item.Label).ToArray();

            Log.Information("Received {CompletionCount} completions from the language server.", completionItems.Length);
            for (int itemIndex = 0; itemIndex < completionItems.Length; itemIndex++)
            {
                Log.Information("\tCompletionItems[{ItemIndex}] = {@CompletionItem}",
                    itemIndex,
                    completionItems[itemIndex]
                );
            }

            Assert.NotEmpty(completionItems);

            Assert.Equal(
                expected: [
                    "<!-- -->",
                ],
                actual: completionItems.Select(item => item.Label)
            );
        }

        [Fact]
        public async Task HoverSlnx()
        {
            var testFilePath = Path.Combine(_workspaceRoot, "Test.slnx");
            await File.WriteAllTextAsync(testFilePath,
            """
            <Solution>
                <Configurations>
                    <Platform Name="Any CPU" />
                    <Platform Name="x64" />
                    <Platform Name="x86" />
                </Configurations>
                <Folder Name="/Solution Items/">
                    <File Path=".editorconfig" />
                    <File Path=".gitignore" />
                    <File Path="Directory.Build.props" />
                    <File Path="Directory.Build.targets" />
                    <File Path="Directory.Packages.props" />
                    <File Path="LICENSE" />
                    <File Path="MSBuildProjectTools.ruleset" />
                    <File Path="OSSREADME.json" />
                    <File Path="README.md" />
                </Folder>
                <Folder Name="/src/">
                    <Project Path="src/LanguageServer.Common/LanguageServer.Common.csproj" />
                    <Project Path="src/LanguageServer.Engine/LanguageServer.Engine.csproj" />
                    <Project Path="src/LanguageServer.SemanticModel.MSBuild/LanguageServer.SemanticModel.MSBuild.csproj" />
                    <Project Path="src/LanguageServer.SemanticModel.Xml/LanguageServer.SemanticModel.Xml.csproj" />
                    <Project Path="src/LanguageServer/LanguageServer.csproj" />
                </Folder>
                <Folder Name="/test/">
                    <Project Path="test/LanguageServer.Engine.Tests/LanguageServer.Engine.Tests.csproj">
                        <Platform Solution="Debug|Any CPU" Project="x64" />
                    </Project>
                    <Project Path="test/LanguageServer.IntegrationTests/LanguageServer.IntegrationTests.csproj">
                        <Platform Project="x64" />
                    </Project>
                </Folder>
            </Solution>
            """);

            var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Hover hoverResult = await _fixture.Client.SendRequest(new HoverParams
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = DocumentUri.FromFileSystemPath(testFilePath)
                },
                Position = new Position(7, 7).ToLsp()
            }, timeout.Token);

            Assert.NotNull(hoverResult);
            Assert.NotNull(hoverResult.Contents);
            Assert.Equal(
                "Folder: `Solution Items`",
                hoverResult.Contents.ToString()
            );
        }

        /// <summary>
        ///     Test that the language server does process textDocument/didOpen
        ///     notification by testing that the busy state notification (msbuild/busy)
        ///     reaches the client.
        /// </summary>
        [Fact]
        public async Task OpenCsproj()
        {
            var testFilePath = Path.Combine(_workspaceRoot, "Test.csproj");
            await File.WriteAllTextAsync(testFilePath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net6.0</TargetFramework>
                </PropertyGroup>  
            </Project>
            """);

            IDisposable handlerRegistration = null;
            var tcsBusy = new TaskCompletionSource();

            Action<Action<BusyNotificationParams>> attach =
                handler =>
                {
                    var assertHandler = handler;
                    handler = @params =>
                    {
                        assertHandler(@params);
                        if (!@params.IsBusy)
                            tcsBusy.TrySetResult();
                    };
                    var cancelReg = tcsBusy.CancelAfter(TimeSpan.FromSeconds(5));
                    var handlerReg = _fixture.Client.Register(
                        registry => registry.AddHandler("msbuild/busy",
                            NotificationHandler.For(handler)));
                    handlerRegistration = new CompositeDisposable(handlerReg, cancelReg);
                };

            Action<Action<BusyNotificationParams>> detach =
                handler => handlerRegistration?.Dispose();

            var raisedBusy = await Assert.RaisesAsync(
                attach, detach,
                () =>
                {
                    _fixture.Client.SendNotification(new DidOpenTextDocumentParams
                    {
                        TextDocument = new TextDocumentItem
                        {
                            Uri = DocumentUri.FromFileSystemPath(testFilePath),
                            LanguageId = "msbuild"
                        }
                    });
                    return tcsBusy.Task;
                }
            );

            Assert.False(raisedBusy.Arguments.IsBusy);
            Assert.Equal("Project loaded.", raisedBusy.Arguments.Message);
        }
    }
}
