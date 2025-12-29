using Microsoft.Language.Xml;
using MSBuildProjectTools.LanguageServer.SemanticModel;
using MSBuildProjectTools.LanguageServer.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace MSBuildProjectTools.LanguageServer.Tests
{
    public class VsSolutionLocatorTests(ITestOutputHelper testOutput)
        : TestBase(testOutput)
    {
        [Fact]
        public async Task SimpleTest()
        {
            VsSolution solution;

            using (var buffer = new MemoryStream(Encoding.UTF8.GetBytes(SolutionText)))
            {
                solution = await
                    VsSolution.CreateInvalid(
                        new FileInfo("MSBuildProjectTools.slnx")
                    )
                    .LoadFrom(buffer);
            }

            XmlDocumentSyntax solutionXml = Parser.ParseText(SolutionText);

            var xmlPositions = new TextPositions(SolutionText);
            var solutionXmlLocator = new XmlLocator(solutionXml, xmlPositions);
            Assert.NotEmpty(solutionXmlLocator.AllNodes);

            var solutionObjectLocator = new VsSolutionObjectLocator(solution, solutionXmlLocator, xmlPositions);

            Assert.Equal(4, solutionObjectLocator.AllObjects.Count());
        }

        const string SolutionText = """
            <Solution>
                <Configurations>
                    <Platform Name="Any CPU" />
                    <Platform Name="x64" />
                    <Platform Name="x86" />
                </Configurations>
                <Folder Name="/Solution Items/">
                    <File Path=".editorconfig" />
                    <File Path=".gitignore" />
                    <File Path="Directory.Build.props" />
                    <File Path="Directory.Build.targets" />
                    <File Path="Directory.Packages.props" />
                    <File Path="LICENSE" />
                    <File Path="MSBuildProjectTools.ruleset" />
                    <File Path="OSSREADME.json" />
                    <File Path="README.md" />
                </Folder>
                <Folder Name="/src/">
                    <Project Path="src/LanguageServer.Common/LanguageServer.Common.csproj" />
                    <Project Path="src/LanguageServer.Engine/LanguageServer.Engine.csproj" />
                    <Project Path="src/LanguageServer.SemanticModel.MSBuild/LanguageServer.SemanticModel.MSBuild.csproj" />
                    <Project Path="src/LanguageServer.SemanticModel.Xml/LanguageServer.SemanticModel.Xml.csproj" />
                    <Project Path="src/LanguageServer/LanguageServer.csproj" />
                </Folder>
                <Folder Name="/test/">
                    <Project Path="test/LanguageServer.Engine.Tests/LanguageServer.Engine.Tests.csproj">
                        <Platform Solution="Debug|Any CPU" Project="x64" />
                    </Project>
                    <Project Path="test/LanguageServer.IntegrationTests/LanguageServer.IntegrationTests.csproj">
                        <Platform Project="x64" />
                    </Project>
                </Folder>
            </Solution>    
        """;

    }
}
