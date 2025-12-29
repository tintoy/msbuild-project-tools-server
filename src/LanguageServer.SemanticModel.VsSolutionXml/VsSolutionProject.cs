using Microsoft.VisualStudio.SolutionPersistence.Model;
using MSBuildProjectTools.LanguageServer.Utilities;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     Represents a project in a solution's semantic model.
    /// </summary>
    public class VsSolutionProject(VsSolution solution, SolutionProjectModel solutionProject, XSNode declaringXml)
        : VsSolutionObject<SolutionProjectModel>(solution, solutionProject, declaringXml)
    {
        /// <summary>
        ///     The underlying <see cref="SolutionProjectModel"/> represented by the <see cref="VsSolutionProject"/>.
        /// </summary>
        public SolutionProjectModel Project => UnderlyingObject;

        /// <summary>
        ///     The object's name.
        /// </summary>
        public override string Name => Project.ActualDisplayName;

        /// <summary>
        ///     The kind of solution object represented by the <see cref="VsSolutionProject"/>.
        /// </summary>
        public override VsSolutionObjectKind Kind => VsSolutionObjectKind.Project;

        /// <summary>
        ///     The full path of the file where the <see cref="VsSolutionProject"/> is declared.
        /// </summary>
        public override string SourceFile => Solution.File.FullName;
    }
}
