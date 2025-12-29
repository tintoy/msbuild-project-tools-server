using Microsoft.Build.Construction;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MSBuildProjectTools.LanguageServer.Utilities
{
    /// <summary>
    ///     Information about a Visual Studio solution file (either '.slnx' format, or legacy '.sln' format).
    /// </summary>
    /// <param name="File">
    ///     The solution file that the underlying <see cref="SolutionModel"/> was loaded from.
    /// </param>
    /// <param name="Format">
    ///     The solution file format.
    /// </param>
    /// <param name="Model">
    ///     A <see cref="SolutionModel"/> representing the solution contents.
    /// </param>
    /// <param name="IsValid">
    ///     Is the solution model valid?
    /// </param>
    public record class VsSolution(FileInfo File, VsSolutionFormat Format, SolutionModel Model, bool IsValid = true)
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

            if (!IsValid)
                throw new InvalidOperationException($"Cannot save the solution because its model is missing or invalid.");

            switch (Format)
            {
                case VsSolutionFormat.Legacy:
                {
                    await SolutionSerializers.SlnFileV12.SaveAsync(File.FullName, Model, cancellationToken);

                    break;
                }
                case VsSolutionFormat.Xml:
                {
                    await SolutionSerializers.SlnXml.SaveAsync(File.FullName, Model, cancellationToken);

                    break;
                }
                default:
                {
                    throw new NotSupportedException($"Unsupported format '{Format}' for solution file '{File.Name}'.");
                }
            }
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

            if (!IsValid)
                throw new InvalidOperationException($"Cannot save the solution because its model is missing or invalid.");

            if (solutionFile == File)
            {
                await Save(cancellationToken);

                return this;
            }

            VsSolutionFormat format = VsSolutionHelper.GetSolutionFormat(solutionFile.Name);

            switch (format)
            {
                case VsSolutionFormat.Legacy:
                {
                    await SolutionSerializers.SlnFileV12.SaveAsync(solutionFile.FullName, Model, cancellationToken);

                    break;
                }
                case VsSolutionFormat.Xml:
                {
                    await SolutionSerializers.SlnXml.SaveAsync(solutionFile.FullName, Model, cancellationToken);

                    break;
                }
                default:
                {
                    throw new NotSupportedException($"Unsupported format '{format}' for solution file '{solutionFile}'.");
                }
            }

            return this with
            {
                File = solutionFile,
                Format = format,
            };
        }

        /// <summary>
        ///     Load the solution's contents from a <see cref="Stream"/>.
        /// </summary>
        /// <param name="solutionContent">
        ///     A <see cref="Stream"/> containing the solution contents.
        /// </param>
        /// <param name="cancellationToken">
        ///     An optional <see cref="CancellationToken"/> that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        ///     A <see cref="VsSolution"/> representing the persisted solution.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///     The solution format could not be termined from the name of the target <paramref name="solutionContent"/>.
        /// </exception>
        public async Task<VsSolution> LoadFrom(Stream solutionContent, CancellationToken cancellationToken = default)
        {
            if (solutionContent == null)
                throw new ArgumentNullException(nameof(solutionContent));

            SolutionModel solutionModel;

            switch (Format)
            {
                case VsSolutionFormat.Legacy:
                {
                    solutionModel = await SolutionSerializers.SlnFileV12.OpenAsync(solutionContent, cancellationToken);

                    break;
                }
                case VsSolutionFormat.Xml:
                {
                    solutionModel = await SolutionSerializers.SlnXml.OpenAsync(solutionContent, cancellationToken);

                    break;
                }
                default:
                {
                    throw new ArgumentException($"Unsupported format for solution file '{File.Name}'.", nameof(solutionContent));
                }
            }

            return this with
            {
                Model = solutionModel,
                IsValid = true,
            };
        }

        /// <summary>
        ///     Create an invalid <see cref="VsSolution"/> to represent the solution, but with invalid state.
        /// </summary>
        /// <returns>
        ///     The new <see cref="VsSolution"/>.
        /// </returns>
        public VsSolution ToInvalid() => this with { Model = new SolutionModel(), IsValid = false };

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

            VsSolutionFormat format = VsSolutionHelper.GetSolutionFormat(solutionFile.Name);

            SolutionModel solutionModel;

            switch (format)
            {
                case VsSolutionFormat.Legacy:
                {
                    solutionModel = await SolutionSerializers.SlnFileV12.OpenAsync(solutionFile.FullName, cancellationToken);

                    break;
                }
                case VsSolutionFormat.Xml:
                {
                    solutionModel = await SolutionSerializers.SlnXml.OpenAsync(solutionFile.FullName, cancellationToken);

                    break;
                }
                default:
                {
                    throw new NotSupportedException($"Unsupported format for solution file '{solutionFile.FullName}'.");
                }
            }

            return new VsSolution(
                File: solutionFile,
                Format: format,
                Model: solutionModel,
                IsValid: true
            );
        }

        /// <summary>
        ///     Create a <see cref="VsSolution"/> to represent a solution with invalid state.
        /// </summary>
        /// <param name="solutionFile">
        ///     The solution file.
        /// </param>
        /// <returns>
        ///     The new <see cref="VsSolution"/>.
        /// </returns>
        public static VsSolution CreateInvalid(string solutionFile)
        {
            if (solutionFile == null)
                throw new ArgumentNullException(nameof(solutionFile));

            return CreateInvalid(
                solutionFile: new FileInfo(solutionFile)
            );
        }

        /// <summary>
        ///     Create a <see cref="VsSolution"/> to represent a solution with invalid state.
        /// </summary>
        /// <param name="solutionFile">
        ///     The solution file.
        /// </param>
        /// <returns>
        ///     The new <see cref="VsSolution"/>.
        /// </returns>
        public static VsSolution CreateInvalid(FileInfo solutionFile)
        {
            if (solutionFile == null)
                throw new ArgumentNullException(nameof(solutionFile));

            return new VsSolution(
                File: solutionFile,
                Format: VsSolutionHelper.GetSolutionFormat(solutionFile.Name),
                Model: new SolutionModel(),
                IsValid: false
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

    /// <summary>
    ///     Well-known Visual Studio solution formats.
    /// </summary>
    public enum VsSolutionFormat
    {
        /// <summary>
        ///     An unknown format.
        /// </summary>
        Unknown = 0,

        /// <summary>
        ///     The legacy (v12) solution format (.sln).
        /// </summary>
        Legacy = 1,

        /// <summary>
        ///     The XML solution format (.slnx).
        /// </summary>
        Xml = 2,
    }
}
