using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MSBuildProjectTools.LanguageServer.Handlers
{
    using CompletionProviders;
    using Documents;
    using LanguageServer.CustomProtocol;
    using SemanticModel;
    using Utilities;

    using Position = Position;

    /// <summary>
    ///     Handler for completion requests.
    /// </summary>
    public sealed class CompletionHandler
        : Handler, ICustomCompletionHandler
    {
        /// <summary>
        ///     Create a new <see cref="CompletionHandler"/>.
        /// </summary>
        /// <param name="server">
        ///     The language server.
        /// </param>
        /// <param name="workspace">
        ///     The document workspace.
        /// </param>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        public CompletionHandler(ILanguageServer server, Workspace workspace, ILogger logger)
            : base(server, logger)
        {
            if (workspace == null)
                throw new ArgumentNullException(nameof(workspace));

            Workspace = workspace;
        }

        /// <summary>
        ///     Registered completion providers.
        /// </summary>
        public List<ICompletionProvider> Providers { get; } = new List<ICompletionProvider>();

        /// <summary>
        ///     The document workspace.
        /// </summary>
        Workspace Workspace { get; }

        /// <summary>
        ///     The LSP document selector that describes documents the handler is interested in.
        /// </summary>
        DocumentSelector DocumentSelector { get; } = new DocumentSelector(
            new DocumentFilter
            {
                Pattern = "**/*.*",
                Language = "msbuild",
                Scheme = "file"
            },
            new DocumentFilter
            {
                Pattern = "**/*.*proj",
                Language = "xml",
                Scheme = "file"
            },
            new DocumentFilter
            {
                Pattern = "**/*.props",
                Language = "xml",
                Scheme = "file"
            },
            new DocumentFilter
            {
                Pattern = "**/*.targets",
                Language = "xml",
                Scheme = "file"
            }
        );

        /// <summary>
        ///     Registration options for handling completion-request events.
        /// </summary>
        CompletionRegistrationOptions CompletionRegistrationOptions
        {
            get => new CompletionRegistrationOptions
            {
                DocumentSelector = DocumentSelector,
                TriggerCharacters = new string[] {
                    "<", // Element
                },
                ResolveProvider = false
            };
        }

        /// <summary>
        ///     Should the handler return an empty <see cref="CompletionList"/>s instead of <c>null</c>?
        /// </summary>
        bool ReturnEmptyCompletionLists => Workspace.Configuration.EnableExperimentalFeatures.Contains("empty-completion-lists");

        /// <summary>
        ///     A <see cref="CompletionList"/> (or <c>null</c>) representing no completions.
        /// </summary>
        CompletionList NoCompletions => ReturnEmptyCompletionLists ? new CompletionList(Enumerable.Empty<CompletionItem>(), isIncomplete: false) : null;

        /// <summary>
        ///     Called when completions are requested.
        /// </summary>
        /// <param name="parameters">
        ///     The request parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation whose result is the completion list or <c>null</c> if no completions are provided.
        /// </returns>
        async Task<CompletionList> OnCompletion(CompletionParams parameters, CancellationToken cancellationToken)
        {
            ProjectDocument projectDocument = await Workspace.GetProjectDocument(parameters.TextDocument.Uri);

            XmlLocation location;

            bool isIncomplete = false;
            List<CompletionItem> completionItems = new List<CompletionItem>();
            using (await projectDocument.Lock.ReaderLockAsync(cancellationToken))
            {
                Position position = parameters.Position.ToNative();
                Log.Verbose("Completion requested for {Position:l}", position);

                if (!projectDocument.HasXml)
                {
                    Log.Verbose("Completion short-circuited; project document does not have valid XML.");

                    return NoCompletions;
                }

                location = projectDocument.XmlLocator.Inspect(position);
                if (location == null)
                {
                    Log.Verbose("Completion short-circuited; nothing interesting at {Position:l}", position);

                    return NoCompletions;
                }

                Log.Verbose("Completion will target {XmlLocation:l}", location);

                string triggerCharacters = null;
                if (parameters.Context != null && parameters.Context.TriggerKind == CompletionTriggerKind.TriggerCharacter)
                    triggerCharacters = parameters.Context.TriggerCharacter;

                List<Task<CompletionList>> allProviderCompletions =
                    Providers.Select(
                        provider => provider.ProvideCompletionsAsync(location, projectDocument, triggerCharacters, cancellationToken)
                    )
                    .ToList();

                while (allProviderCompletions.Count > 0)
                {
                    Task<CompletionList> providerCompletionTask = await Task.WhenAny(allProviderCompletions);
                    allProviderCompletions.Remove(providerCompletionTask);

                    try
                    {
                        CompletionList providerCompletions = await providerCompletionTask;
                        if (providerCompletions != null)
                        {
                            completionItems.AddRange(providerCompletions.Items);

                            isIncomplete |= providerCompletions.IsIncomplete; // If any provider returns incomplete results, VSCode will need to ask again as the user continues to type.
                        }
                    }
                    catch (AggregateException aggregateSuggestionError)
                    {
                        foreach (Exception suggestionError in aggregateSuggestionError.Flatten().InnerExceptions)
                            Log.Error(suggestionError, "Failed to provide completions.");

                        return NoCompletions;
                    }
                    catch (Exception suggestionError)
                    {
                        Log.Error(suggestionError, "Failed to provide completions.");

                        return NoCompletions;
                    }
                }
            }

            Log.Verbose("Offering a total of {CompletionCount} completions for {Location:l} (Exhaustive: {Exhaustive}).", completionItems.Count, location, !isIncomplete);

            if (completionItems.Count == 0 && !isIncomplete)
                return NoCompletions;

            CompletionList completionList = new CompletionList(completionItems, isIncomplete);

            return completionList;
        }

        /// <summary>
        ///     Handle a request for completion.
        /// </summary>
        /// <param name="parameters">
        ///     The request parameters.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the request.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the operation whose result is the completion list or <c>null</c> if no completions are provided.
        /// </returns>
        async Task<CompletionList> IRequestHandler<CompletionParams, CompletionList>.Handle(CompletionParams parameters, CancellationToken cancellationToken)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            using (BeginOperation("OnCompletion"))
            {
                try
                {
                    return await OnCompletion(parameters, cancellationToken);
                }
                catch (Exception unexpectedError)
                {
                    Log.Error(unexpectedError, "Unhandled exception in {Method:l}.", "OnCompletion");

                    return null;
                }
            }
        }

        /// <summary>
        ///     Get registration options for handling completion requests.
        /// </summary>
        /// <returns>
        ///     The registration options.
        /// </returns>
        CompletionRegistrationOptions IRegistration<CompletionRegistrationOptions>.GetRegistrationOptions() => CompletionRegistrationOptions;

        /// <summary>
        ///     Called to inform the handler of the language server's completion capabilities.
        /// </summary>
        /// <param name="capabilities">
        ///     A <see cref="CompletionCapability"/> data structure representing the capabilities.
        /// </param>
        void ICapability<CompletionCapability>.SetCapability(CompletionCapability capabilities)
        {
        }
    }
}
