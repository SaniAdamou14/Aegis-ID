using Aegis.Cli.Suppressions;

namespace Aegis.Cli.Tests;

public class SuppressionFileLoaderTests
{
    private static string WriteTempFile(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"aegis-suppressions-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        return path;
    }

    [Fact]
    public void ValidFile_LoadsSuppressionsWithAndWithoutExpiry()
    {
        var path = WriteTempFile(
            """
            - controlId: IAM-005
              objectId: app-002
              reason: "Rotation scheduled next quarter"
              expires: 2099-12-31
            - controlId: IAM-004
              objectId: u-004
              reason: "Documented break-glass account"
            """);

        try
        {
            var suppressions = SuppressionFileLoader.Load(path);

            Assert.Equal(2, suppressions.Count);
            Assert.Contains(suppressions, s => s is { ControlId: "IAM-005", ObjectId: "app-002" } && s.Expires is not null);
            Assert.Contains(suppressions, s => s is { ControlId: "IAM-004", ObjectId: "u-004" } && s.Expires is null);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MissingReason_ThrowsInvalidDataException()
    {
        var path = WriteTempFile(
            """
            - controlId: IAM-005
              objectId: app-002
            """);

        try
        {
            Assert.Throws<InvalidDataException>(() => SuppressionFileLoader.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MissingObjectId_ThrowsInvalidDataException()
    {
        var path = WriteTempFile(
            """
            - controlId: IAM-005
              reason: "some reason"
            """);

        try
        {
            Assert.Throws<InvalidDataException>(() => SuppressionFileLoader.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
