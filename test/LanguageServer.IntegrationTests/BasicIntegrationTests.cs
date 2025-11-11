using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Serilog.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace MSBuildProjectTools.LanguageServer.IntegrationTests
{
    public class BasicIntegrationTests(ITestOutputHelper testOutput) : IntegrationTestBase(testOutput), IAsyncLifetime
    {
        private readonly LanguageServerFixture _fixture = new(false);
        private readonly TempDirectory _workspaceRoot = new();

        public async Task InitializeAsync()
        {
            var loggerProvider = new SerilogLoggerProvider(Log);
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
            var response = await _fixture.Client.SendRequest(new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = DocumentUri.FromFileSystemPath(testFilePath)
                },
                Position = new(4, 6)
            }, timeout.Token);

            Assert.NotNull(response);
        }
    }
}
