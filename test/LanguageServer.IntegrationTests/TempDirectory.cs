using System;
using System.IO;

namespace MSBuildProjectTools.LanguageServer.IntegrationTests
{
    internal class TempDirectory : IDisposable
    {
        public string Path { get; }

        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                try
                {
                    Directory.Delete(Path, recursive: true);
                }
                catch (IOException)
                {
                    // Ignore exceptions during cleanup
                }
            }
        }

        public static implicit operator string(TempDirectory tempDirectory) => tempDirectory.Path;
    }
}
