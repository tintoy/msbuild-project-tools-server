using Microsoft.VisualStudio.SolutionPersistence.Model;
using MSBuildProjectTools.LanguageServer.Utilities;
using System;
using System.IO;
using System.Linq;
using static NuGet.Packaging.PackagingConstants;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     Represents a file item in a solution's semantic model.
    /// </summary>
    public class VsSolutionFile
        : VsSolutionObject<SolutionFolderModel>
    {
        public VsSolutionFile(VsSolution solution, SolutionFolderModel folder, string relativePath, XSNode declaringXml)
            : base(solution, folder, declaringXml)
        {
            if (solution == null)
                throw new ArgumentNullException(nameof(solution));

            if (folder == null)
                throw new ArgumentNullException(nameof(folder));

            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(relativePath)}.", nameof(relativePath));

            if (declaringXml == null)
                throw new ArgumentNullException(nameof(declaringXml));

            RelativePath = relativePath;
        }

        /// <summary>
        ///     The underlying <see cref="SolutionFolderModel"/> containing the file represented by the <see cref="VsSolutionFile"/>.
        /// </summary>
        public SolutionFolderModel Folder => UnderlyingObject;

        /// <summary>
        ///     The object's name.
        /// </summary>
        public override string Name => RelativePath;

        /// <summary>
        ///     The path of the file, relative to the parent folder.
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        ///     The path of the file, relative to the solution root.
        /// </summary>
        public string FullPath => Path.Combine(Folder.Path, RelativePath);

        /// <summary>
        ///     The kind of solution object represented by the <see cref="VsSolutionFile"/>.
        /// </summary>
        public override VsSolutionObjectKind Kind => VsSolutionObjectKind.File;

        /// <summary>
        ///     The full path of the file where the <see cref="VsSolutionFile"/> is declared.
        /// </summary>
        public override string SourceFile => Solution.File.FullName;
    }
}
