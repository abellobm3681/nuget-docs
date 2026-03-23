namespace NugetDocs.IntegrationTests;

[TestClass]
public class DiffCommandTests
{
    [TestMethod]
    public async Task Diff_TypeOnly()
    {
        var (exitCode, output, _) = await CliTestHelper.RunAsync(
            "diff", "Newtonsoft.Json", "--from", "13.0.1", "--to", "13.0.3", "--type-only");

        // Exit code 0 = no breaking changes, 2 = breaking changes
        exitCode.Should().BeOneOf(0, 2);
        output.Should().Contain("Newtonsoft.Json");
    }
}
