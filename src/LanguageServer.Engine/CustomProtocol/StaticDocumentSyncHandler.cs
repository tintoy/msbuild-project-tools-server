using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace MSBuildProjectTools.LanguageServer.CustomProtocol
{
    /// <summary>
    ///     Custom handler interface which supports <see cref="LanguageServer"/>
    ///     in calculating server capabilities for <see cref="TextDocumentSyncOptions"/>.
    /// </summary>
    /// <remarks>
    ///     This will be required at least until v0.14.0 of OmniSharp libraries.
    /// </remarks>
    public interface IStaticDocumentSyncHandler : IRegistration<TextDocumentSyncOptions>, IWillSaveTextDocumentHandler,
            IWillSaveWaitUntilTextDocumentHandler
    {

    }
}
