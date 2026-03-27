using System.Collections.Generic;
using System.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace MSBuildProjectTools.LanguageServer
{
    /// <summary>
    ///     Well-known document filters.
    /// </summary>
    public static class DocumentFilters
    {
        /// <summary>
        ///     The identifier for the "file" scheme.
        /// </summary>
        static readonly string FileSchemeIdentifier = "file";

        /// <summary>
        ///     Well-known MSBuild document filters.
        /// </summary>
        public static class MSBuild
        {
            /// <summary>
            ///     The document filter for any file using the MSBuild language.
            /// </summary>
            public static DocumentFilter ByLanguage => new DocumentFilter
            {
                Pattern = "**/*.*",
                Language = LanguageIdentifiers.MSBuild,
                Scheme = FileSchemeIdentifier,
            };

            /// <summary>
            ///     The document filter for all files using the MSBuild language.
            /// </summary>
            public static IEnumerable<DocumentFilter> All
            {
                get
                {
                    yield return ByLanguage;
                }
            }
        }

        /// <summary>
        ///     Well-known XML document filters.
        /// </summary>
        public static class Xml
        {
            /// <summary>
            ///     The document filter for MSBuild project files using the XML language.
            /// </summary>
            public static DocumentFilter MSBuildProjectFiles => new DocumentFilter
            {
                Pattern = "**/*.*proj",
                Language = LanguageIdentifiers.Xml,
                Scheme = FileSchemeIdentifier,
            };

            /// <summary>
            ///     The document filter for MSBuild properties files using the XML language.
            /// </summary>
            public static DocumentFilter MSBuildPropertiesFiles => new DocumentFilter
            {
                Pattern = "**/*.props",
                Language = LanguageIdentifiers.Xml,
                Scheme = FileSchemeIdentifier,
            };

            /// <summary>
            ///     The document filter for MSBuild target files using the XML language.
            /// </summary>
            public static DocumentFilter MSBuildTargetsFiles => new DocumentFilter
            {
                Pattern = "**/*.targets",
                Language = LanguageIdentifiers.Xml,
                Scheme = FileSchemeIdentifier,
            };

            /// <summary>
            ///     The document filter for any file using the VS Solution XML language.
            /// </summary>
            public static readonly DocumentFilter SlnxFiles = new DocumentFilter
            {
                Pattern = "**/*.slnx",
                Language = LanguageIdentifiers.Xml,
                Scheme = FileSchemeIdentifier
            };

            /// <summary>
            ///     The document filter for all MSBuild and VS Solution XML files using the XML language.
            /// </summary>
            public static IEnumerable<DocumentFilter> All
            {
                get
                {
                    yield return MSBuildProjectFiles;
                    yield return MSBuildPropertiesFiles;
                    yield return MSBuildTargetsFiles;
                    yield return SlnxFiles;
                }
            }
        }

        /// <summary>
        ///     Well-known VS Solution XML document filters.
        /// </summary>
        public static class VsSolutionXml
        {
            /// <summary>
            ///     The document filter for any file using the VS Solution XML language.
            /// </summary>
            public static readonly DocumentFilter ByLanguage = new DocumentFilter
            {
                Pattern = "**/*.*",
                Language = LanguageIdentifiers.VsSolutionXml,
                Scheme = FileSchemeIdentifier
            };

            /// <summary>
            ///     The document filter for any file using the VS Solution XML language.
            /// </summary>
            public static readonly DocumentFilter SlnxFiles = new DocumentFilter
            {
                Pattern = "**/*.slnx",
                Language = LanguageIdentifiers.VsSolutionXml,
                Scheme = FileSchemeIdentifier
            };

            /// <summary>
            ///     The document filter for all VS Solution XML files using the XML language.
            /// </summary>
            public static IEnumerable<DocumentFilter> All
            {
                get
                {
                    yield return ByLanguage;

                    yield return SlnxFiles;
                }
            }
        }

        /// <summary>
        ///     The document filter for all supported file types and languages.
        /// </summary>
        public static IEnumerable<DocumentFilter> All
        {
            get
            {
                foreach (DocumentFilter msbuildDocumentFilter in MSBuild.All)
                    yield return msbuildDocumentFilter;

                foreach (DocumentFilter vsSolutionXmlDocumentFilter in VsSolutionXml.All)
                    yield return vsSolutionXmlDocumentFilter;

                foreach (DocumentFilter xmlDocumentFilter in Xml.All)
                    yield return xmlDocumentFilter;
            }
        }
    }
}
