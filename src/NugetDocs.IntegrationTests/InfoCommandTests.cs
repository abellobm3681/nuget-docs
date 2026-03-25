namespace NugetDocs.IntegrationTests;

[TestClass]
public class InfoCommandTests
{
    [TestMethod]
    public async Task Info_ShowsPackageMetadata()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "info", "Newtonsoft.Json");

        exitCode.Should().Be(0);
        output.Should().Contain("Newtonsoft.Json");
        output.Should().Contain("James Newton-King");
    }

    [TestMethod]
    public async Task Info_JsonOutput()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "info", "Newtonsoft.Json", "--json");

        exitCode.Should().Be(0);
        output.Should().Contain("\"id\"");
        output.Should().Contain("\"authors\"");
    }

    [TestMethod]
    public async Task Info_MetaPackage_ShowsMetadata()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "info", "Humanizer");

        exitCode.Should().Be(0);
        output.Should().Contain("Humanizer");
        output.Should().Contain("Dependencies:");
    }
}
