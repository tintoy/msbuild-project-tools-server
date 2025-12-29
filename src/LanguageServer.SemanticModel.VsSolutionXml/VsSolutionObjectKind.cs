using Microsoft.VisualStudio.SolutionPersistence.Model;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     A type of VS Solution object.
    /// </summary>
    public enum VsSolutionObjectKind
    {
        /// <summary>
        ///     An object in an invalid solution.
        /// </summary>
        Invalid = 0,

        /// <summary>
        ///     The solution (<see cref="SolutionModel"/>).
        /// </summary>
        Solution = 1,

        /// <summary>
        ///     A solution folder (<see cref="SolutionFolderModel"/>) in a solution.
        /// </summary>
        Folder = 2,

        /// <summary>
        ///     A project (<see cref="SolutionProjectModel"/>) in a solution.
        /// </summary>
        Project = 3,

        /// <summary>
        ///     A solution item (<see cref="SolutionItemModel"/>) in a solution.
        /// </summary>
        Item = 4,
    }
}
