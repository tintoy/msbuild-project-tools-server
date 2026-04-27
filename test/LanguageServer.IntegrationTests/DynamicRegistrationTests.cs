using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace MSBuildProjectTools.LanguageServer.IntegrationTests
{
    public class DynamicRegistrationTests(ITestOutputHelper testOutput) : IntegrationTestBase(testOutput), IAsyncLifetime
    {
        private readonly LanguageServerFixture _fixture = new(true);
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
        ///     Test that the server registers completion handler dynamically.
        /// </summary>
        [Fact]
        public async Task ServerProvidesDynamicCompletionHandler()
        {
            Assert.NotNull(_fixture.Client);
            Assert.NotNull(_fixture.Client!.RegistrationManager);
            // Test if the server announced completion handler as static capability,
            // despite the client had dynamic registration support enabled.
            if (_fixture.Client!.ServerSettings?.Capabilities?.CompletionProvider is not null)
            {
                // If so, the client should still provide a registration for this
                // static capability in dynamic registration mode.
                Assert.Contains(_fixture.Client!.RegistrationManager!.CurrentRegistrations,
                    reg => reg.Method == TextDocumentNames.Completion);
            }
            else
            {
                // If not, wait for the server to actually register the completion
                // handler dynamically.
                int count;
                for (count = 0; count < 5; count++)
                    if (await _fixture.Client!.RegistrationManager!.Registrations
                        .Timeout(TimeSpan.FromSeconds(15))
                        .Any(regs =>
                            regs.Any(reg => reg.Method == TextDocumentNames.Completion)))
                        break;
                Assert.NotEqual(5, count);
            }
        }
    }
}
