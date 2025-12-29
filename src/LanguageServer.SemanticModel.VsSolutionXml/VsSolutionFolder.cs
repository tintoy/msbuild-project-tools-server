using Microsoft.VisualStudio.SolutionPersistence.Model;
using MSBuildProjectTools.LanguageServer.Utilities;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     Represents a folder in a solution's semantic model.
    /// </summary>
    public class VsSolutionFolder(VsSolution solution, SolutionFolderModel solutionFolder, XSNode declaringXml)
        : VsSolutionObject<SolutionFolderModel>(solution, solutionFolder, declaringXml)
    {
        /// <summary>
        ///     The underlying <see cref="SolutionFolderModel"/> represented by the <see cref="VsSolutionFolder"/>.
        /// </summary>
        public SolutionFolderModel Folder => UnderlyingObject;

        /// <summary>
        ///     The object's name.
        /// </summary>
        public override string Name => Folder.Name;

        /// <summary>
        ///     The kind of solution object represented by the <see cref="VsSolutionFolder"/>.
        /// </summary>
        public override VsSolutionObjectKind Kind => VsSolutionObjectKind.Folder;

        /// <summary>
        ///     The full path of the file where the <see cref="VsSolutionFolder"/> is declared.
        /// </summary>
        public override string SourceFile => Solution.File.FullName;
    }
}
