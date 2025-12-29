using Microsoft.VisualStudio.SolutionPersistence.Model;
using MSBuildProjectTools.LanguageServer.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     Represents the root of a solution's semantic model.
    /// </summary>
    public class VsSolutionRoot(VsSolution solution, SolutionModel solutionRoot, XSNode declaringXml)
        : VsSolutionObject<SolutionModel>(solution, solutionRoot, declaringXml)
    {
        public override string Name => Solution.File.Name;

        public override VsSolutionObjectKind Kind => VsSolutionObjectKind.Solution;

        public override string SourceFile => Solution.File.FullName;
    }

    /// <summary>
    ///     Represents a folder in a solution's semantic model.
    /// </summary>
    public class VsSolutionFolder(VsSolution solution, SolutionFolderModel solutionFolder, XSNode declaringXml)
        : VsSolutionObject<SolutionFolderModel>(solution, solutionFolder, declaringXml)
    {
        public SolutionFolderModel Folder => UnderlyingObject;

        public override string Name => Folder.Name;

        public override VsSolutionObjectKind Kind => VsSolutionObjectKind.Folder;

        public override string SourceFile => Solution.File.FullName;
    }

    /// <summary>
    ///     Represents a project in a solution's semantic model.
    /// </summary>
    public class VsSolutionProject(VsSolution solution, SolutionProjectModel solutionProject, XSNode declaringXml)
        : VsSolutionObject<SolutionProjectModel>(solution, solutionProject, declaringXml)
    {
        public SolutionProjectModel Project => UnderlyingObject;

        public override string Name => Project.ActualDisplayName;

        public override VsSolutionObjectKind Kind => VsSolutionObjectKind.Project;

        public override string SourceFile => Solution.File.FullName;
    }

    /// <summary>
    ///     Represents an item in a solution's semantic model.
    /// </summary>
    public class VsSolutionItem(VsSolution solution, SolutionItemModel solutionProject, XSNode declaringXml)
        : VsSolutionObject<SolutionItemModel>(solution, solutionProject, declaringXml)
    {
        public SolutionItemModel Item => UnderlyingObject;

        public override string Name => Item.ActualDisplayName;

        public override VsSolutionObjectKind Kind => VsSolutionObjectKind.Item;

        public override string SourceFile => Solution.File.FullName;
    }
}
