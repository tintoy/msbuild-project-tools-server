using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Serilog.Extensions.Logging;
using System;
using System.IO;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace MSBuildProjectTools.LanguageServer.IntegrationTests
{
    using CustomProtocol;
    using System.Linq;
    using Utilities;

    public class BasicIntegrationTests(ITestOutputHelper testOutput) : IntegrationTestBase(testOutput), IAsyncLifetime
    {
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
            Assert.Collection(completionItems,
                item => item.Label = "<PropertyGroup>",
                item => item.Label = "<ItemGroup>",
                item => item.Label = "<Target>",
                item => item.Label = "<Import>",
                item => item.Label = "<!-- -->"
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
            Assert.Collection(completionItems,
                item => item.Label = "<!-- -->"
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
