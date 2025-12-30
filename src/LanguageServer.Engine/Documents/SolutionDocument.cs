using Microsoft.Build.Exceptions;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using MSBuildProjectTools.LanguageServer.SemanticModel;
using MSBuildProjectTools.LanguageServer.Utilities;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Serilog;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

#nullable enable

namespace MSBuildProjectTools.LanguageServer.Documents
{
    /// <summary>
    ///     Represents the document state for a Visual Studio solution file.
    /// </summary>
    public class SolutionDocument
        : XmlDocument
    {
        /// <summary>
        ///     Create a new <see cref="SolutionDocument"/>.
        /// </summary>
        /// <param name="workspace">
        ///     The document workspace.
        /// </param>
        /// <param name="documentUri">
        ///     The document URI.
        /// </param>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        public SolutionDocument(Workspace workspace, DocumentUri documentUri, ILogger logger)
            : base(workspace, documentUri, logger)
        {
            Solution = VsSolution.CreateInvalid(
                solutionFile: documentUri.GetFileSystemPath()
            );
            if (Solution.Format != VsSolutionFormat.Xml)
                throw new NotSupportedException($"Cannot load solution file '{Solution.File.FullName}' (this file is in {Solution.Format} format, but only the new {VsSolutionFormat.Xml} format is supported).");
        }

        /// <summary>
        ///     The document kind.
        /// </summary>
        public override DocumentKind DocumentKind => DocumentKind.Solution;

        /// <summary>
        ///     The parsed solution.
        /// </summary>
        public VsSolution Solution { get; private set; }

        /// <summary>
        ///     Is the underlying solution currently loaded?
        /// </summary>
        public bool HasSolution => HasXml && Solution != null;

        /// <summary>
        ///     The solution object-lookup facility.
        /// </summary>
        protected VsSolutionObjectLocator? SolutionLocator { get; private set; }

        /// <summary>
        ///     Is the underlying solution cached (i.e. out-of-date with respect to the source text)?
        /// </summary>
        /// <remarks>
        ///     If the current solution XML is invalid, the original solution model is retained, but <see cref="SolutionLocator"/> functionality will be unavailable (since source positions may no longer match up).
        /// </remarks>
        public bool IsSolutionCached { get; private set; }

        /// <summary>
        ///     Load and parse the solution.
        /// </summary>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A task representing the load operation.
        /// </returns>
        public override async ValueTask Load(CancellationToken cancellationToken = default)
        {
            await base.Load(cancellationToken);

            if (!HasXml)
                return;

            bool loaded = await TryLoadVsSolution(cancellationToken);
            if (loaded)
                SolutionLocator = new VsSolutionObjectLocator(Solution, XmlLocator, XmlPositions);
            else
                SolutionLocator = null;

            IsSolutionCached = !loaded;
        }

        /// <summary>
        ///     Update the solution's in-memory state.
        /// </summary>
        /// <param name="xml">
        ///     The solution XML.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A task representing the update operation.
        /// </returns>
        public override async ValueTask Update(string xml, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(xml);

            await base.Update(xml, cancellationToken);

            if (!HasXml)
                return;

            bool loaded = await TryLoadVsSolution(cancellationToken);
            if (loaded)
                SolutionLocator = new VsSolutionObjectLocator(Solution, XmlLocator, XmlPositions);
            else
                SolutionLocator = null;

            IsSolutionCached = !loaded;
        }

        /// <summary>
        ///     Unload the project.
        /// </summary>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        public override async ValueTask Unload(CancellationToken cancellationToken = default)
        {
            await TryUnloadVsSolution(cancellationToken);
            SolutionLocator = null;
            IsSolutionCached = false;

            Xml = null;
            XmlPositions = null;
            IsDirty = false;

            await base.Unload(cancellationToken);
        }

        /// <summary>
        ///     Attempt to load the underlying solution.
        /// </summary>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the solution was successfully loaded; otherwise, <c>false</c>.
        /// </returns>
        async ValueTask<bool> TryLoadVsSolution(CancellationToken cancellationToken = default)
        {
            try
            {
                if (HasSolution && !IsDirty)
                    return true;

                if (HasSolution && IsDirty)
                {
                    // AF: Should probably enforce some sort of size constraint, here.
                    byte[] xml = Encoding.UTF8.GetBytes(Xml.ToFullString());
                    using (var xmlStream = new MemoryStream(xml))
                    {
                        Solution = await Solution.LoadFrom(xmlStream, cancellationToken);
                    }

                    Log.Verbose("Successfully updated solution '{SolutionFileName}' from in-memory changes.");
                }
                else
                    Solution = await VsSolution.Load(Solution.File, cancellationToken);

                return true;
            }
            catch (SolutionException invalidSolution)
            {
                if (Workspace.Configuration.Logging.IsDebugLoggingEnabled)
                {
                    Log.Error(invalidSolution, "Failed to load solution '{SolutionFileName}'.", Solution.File.FullName);
                }

                AddErrorDiagnostic(invalidSolution.Message,
                    range: invalidSolution.GetRange(XmlLocator),
                    diagnosticCode: $"VSSolution.{invalidSolution.ErrorType}"
                );
            }
            catch (XmlException invalidProjectXml)
            {
                if (Workspace.Configuration.Logging.IsDebugLoggingEnabled)
                {
                    Log.Error(invalidProjectXml, "Failed to parse XML for solution '{SolutionFileName}'.", Solution.File.FullName);
                }

                // TODO: Match SourceUri (need overloads of AddXXXDiagnostic for reporting diagnostics for other files).
                AddErrorDiagnostic(invalidProjectXml.Message,
                    range: invalidProjectXml.GetRange(XmlLocator),
                    diagnosticCode: "VSSolution.InvalidXML"
                );
            }
            catch (Exception loadError)
            {
                Log.Error(loadError, "Error loading solution '{SolutionFileName}'.", Solution.File.FullName);
            }

            Solution = Solution.ToInvalid();

            return false;
        }

        /// <summary>
        ///     Attempt to unload the underlying solution.
        /// </summary>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if the solution was successfully unloaded; otherwise, <c>false</c>.
        /// </returns>
        ValueTask<bool> TryUnloadVsSolution(CancellationToken cancellationToken = default)
        {
            if (!HasSolution)
                return ValueTask.FromResult(true);

            Solution = Solution.ToInvalid();

            return ValueTask.FromResult(true);
        }
    }
}
