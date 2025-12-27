using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

#nullable enable

namespace MSBuildProjectTools.LanguageServer.Utilities
{
    /// <summary>
    ///     Information about a Visual Studio solution file (either '.slnx' format, or legacy '.sln' format).
    /// </summary>
    /// <param name="File">
    ///     The solution file that the underlying <see cref="SolutionModel"/> was loaded from.
    /// </param>
    /// <param name="Model">
    ///     A <see cref="SolutionModel"/> representing the solution contents.
    /// </param>
    public record class VsSolution(FileInfo File, SolutionModel Model)
    {
        /// <summary>
        ///     Save the solution's contents to the solution <see cref="File"/>.
        /// </summary>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the asynchronous operation.
        /// </param>
        /// <exception cref="InvalidOperationException">
        ///     The solution format could not be termined from the name of the target <see cref="File"/>.
        /// </exception>
        public async Task Save(CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(File.Extension))
                throw new InvalidOperationException($"Cannot determine the solution file format (file name '{File.FullName}' has no extension).");

            ISolutionSerializer? solutionSerializer = Model.SerializerExtension?.Serializer ?? SolutionSerializers.GetSerializerByMoniker(File.FullName);
            if (solutionSerializer == null)
                throw new InvalidOperationException($"Cannot determine the solution file format for '{File.FullName}'.");

            await solutionSerializer.SaveAsync(File.FullName, Model, cancellationToken);
        }

        /// <summary>
        ///     Save the solution's contents to a specific file.
        /// </summary>
        /// <param name="solutionFile">
        ///     The name of the target solution file.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        ///     A <see cref="VsSolution"/> representing the persisted solution.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///     The solution format could not be termined from the name of the target <paramref name="solutionFile"/>.
        /// </exception>
        public Task<VsSolution> SaveAs(string solutionFile, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(solutionFile))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: '{nameof(solutionFile)}'.", nameof(solutionFile));

            return SaveAs(new FileInfo(solutionFile), cancellationToken);
        }

        /// <summary>
        ///     Save the solution's contents to a specific file.
        /// </summary>
        /// <param name="solutionFile">
        ///     A <see cref="FileInfo"/> representing the target solution file.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        ///     A <see cref="VsSolution"/> representing the persisted solution.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///     The solution format could not be termined from the name of the target <paramref name="solutionFile"/>.
        /// </exception>
        public async Task<VsSolution> SaveAs(FileInfo solutionFile, CancellationToken cancellationToken = default)
        {
            if (solutionFile == null)
                throw new ArgumentNullException(nameof(solutionFile));

            if (String.IsNullOrWhiteSpace(solutionFile.Extension))
                throw new ArgumentException($"Cannot determine the solution file format (file name '{solutionFile.FullName}' has no extension).", nameof(solutionFile));

            ISolutionSerializer? solutionSerializer = SolutionSerializers.GetSerializerByMoniker(solutionFile.FullName);
            if (solutionSerializer == null)
                throw new ArgumentException($"Cannot determine the solution file format for '{solutionFile.FullName}'.", nameof(solutionFile));

            await solutionSerializer.SaveAsync(solutionFile.FullName, Model, cancellationToken);

            if (solutionFile == File)
                return this;

            return this with
            {
                File = solutionFile,
            };
        }

        /// <summary>
        ///     Load the solution's contents from a file.
        /// </summary>
        /// <param name="solutionFile">
        ///     The name of the target solution file.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        ///     A <see cref="VsSolution"/> representing the persisted solution.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///     The solution format could not be termined from the name of the target <paramref name="solutionFile"/>.
        /// </exception>
        public static Task<VsSolution> Load(string solutionFile, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(solutionFile))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: '{nameof(solutionFile)}'.", nameof(solutionFile));

            return Load(new FileInfo(solutionFile), cancellationToken);
        }

        /// <summary>
        ///     Load the solution's contents from a file.
        /// </summary>
        /// <param name="solutionFile">
        ///     A <see cref="FileInfo"/> representing the target solution file.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        ///     A <see cref="VsSolution"/> representing the persisted solution.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///     The solution format could not be termined from the name of the target <paramref name="solutionFile"/>.
        /// </exception>
        public static async Task<VsSolution> Load(FileInfo solutionFile, CancellationToken cancellationToken = default)
        {
            if (solutionFile == null)
                throw new ArgumentNullException(nameof(solutionFile));

            if (String.IsNullOrWhiteSpace(solutionFile.Extension))
                throw new ArgumentException($"Cannot determine the solution file format (file name '{solutionFile.FullName}' has no extension).", nameof(solutionFile));

            ISolutionSerializer? solutionSerializer = SolutionSerializers.GetSerializerByMoniker(solutionFile.FullName);
            if (solutionSerializer == null)
                throw new ArgumentException($"Cannot determine the solution file format for '{solutionFile.FullName}'.", nameof(solutionFile));

            SolutionModel solutionModel = await solutionSerializer.OpenAsync(solutionFile.FullName, cancellationToken);

            return new VsSolution(
                File: solutionFile,
                Model: solutionModel
            );
        }

        /// <summary>
        ///     Implicitly convert a <see cref="VsSolution"/> to a <see cref="SolutionModel"/>.
        /// </summary>
        /// <param name="solution">
        ///     The <see cref="VsSolution"/> to convert.
        /// </param>
        /// <seealso cref="Model"/>
        public static implicit operator SolutionModel?(VsSolution? solution)
        {
            if (solution == null)
                return null;

            return solution.Model;
        }
    }
}
