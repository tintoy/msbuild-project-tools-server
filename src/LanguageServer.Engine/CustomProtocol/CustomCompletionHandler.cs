using OmniSharp.Extensions.Embedded.MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace MSBuildProjectTools.LanguageServer.CustomProtocol
{
    /// <summary>
    ///     Custom handler for "textDocument/completion" that accepts <see cref="CompletionParams"/> (the built-in version simply uses <see cref="TextDocumentPositionParams"/>, which doesn't include information about how completion was triggered).
    /// </summary>
    [Parallel, Method("textDocument/completion")]
    public interface ICustomCompletionHandler
        : IRequestHandler<CompletionParams, CompletionList>, IJsonRpcHandler, IJsonRpcRequestHandler<CompletionParams, CompletionList>, IRegistration<CompletionRegistrationOptions>, ICapability<CompletionCapability>
    {
    }
}
