using Microsoft.VisualStudio.SolutionPersistence.Model;
using MSBuildProjectTools.LanguageServer.Utilities;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     Represents a folder in a solution's semantic model.
    /// </summary>
    public class VsSolutionFolder
        : VsSolutionObject<SolutionFolderModel>
    {
        /// <summary>
        ///     Create a new <see cref="VsSolutionFolder"/>.
        /// </summary>
        /// <param name="solution">
        ///     The <see cref="VsSolution"/> that contains the underlying object.
        /// </param>
        /// <param name="model">
        ///     The underlying <see cref="SolutionFolderModel"/>.
        /// </param>
        /// <param name="declaringXml">
        ///     An <see cref="XSNode"/> representing the object's declaring XML.
        /// </param>
        public VsSolutionFolder(VsSolution solution, SolutionFolderModel model, XSNode declaringXml)
            : base(solution, model, declaringXml)
        {
        }

        /// <summary>
        ///     The underlying <see cref="SolutionFolderModel"/> represented by the <see cref="VsSolutionFolder"/>.
        /// </summary>
        public SolutionFolderModel Folder => UnderlyingObject;

        /// <summary>
        ///     The object's name.
        /// </summary>
        public override string Name => Folder.Name;

        /// <summary>
        ///     The folder's absolute path within the solution.
        /// </summary>
        public string Path => Folder.Path;

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
