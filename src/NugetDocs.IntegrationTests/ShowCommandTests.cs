namespace NugetDocs.IntegrationTests;

[TestClass]
public class ShowCommandTests
{
    [TestMethod]
    public async Task Show_DecompilesType()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert");

        exitCode.Should().Be(0);
        output.Should().Contain("class JsonConvert");
        output.Should().Contain("SerializeObject");
    }

    [TestMethod]
    public async Task Show_ShortNameResolution()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "show", "Microsoft.Extensions.AI.Abstractions", "IChatClient");

        exitCode.Should().Be(0);
        output.Should().Contain("interface IChatClient");
    }

    [TestMethod]
    public async Task Show_MemberFilter()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "JsonConvert", "--member", "SerializeObject");

        exitCode.Should().Be(0);
        output.Should().Contain("SerializeObject");
    }

    [TestMethod]
    public async Task Show_AssemblyAttributes()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "show", "Newtonsoft.Json", "--assembly");

        exitCode.Should().Be(0);
        output.Should().Contain("assembly:");
    }
}
