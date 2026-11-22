using System.IO;

namespace LightBoard.Tests;

public class BoardRecoveryTests
{
    private static string TempDirectory( )
    {
        return Path.Join(Path.GetTempPath( ), $"lb-recover-{Guid.NewGuid( ):N}");
    }

    [Fact]
    public void FindLatest_MissingDirectory_ReturnsNull( )
    {
        Assert.Null(BoardRecovery.FindLatest(TempDirectory( )));
    }

    [Fact]
    public void FindLatest_EmptyDirectory_ReturnsNull( )
    {
        var directory = TempDirectory( );
        Directory.CreateDirectory(directory);
        try
        {
            Assert.Null(BoardRecovery.FindLatest(directory));
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [Fact]
    public void FindLatest_ReturnsNewestBoardFile( )
    {
        var directory = TempDirectory( );
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Join(directory, "20260101-080000.lbf"), "");
            File.WriteAllText(Path.Join(directory, "20260101-120000.lbf"), "");
            File.WriteAllText(Path.Join(directory, "20260101-100000.lbf"), "");
            // 非板文件不参与挑选
            File.WriteAllText(Path.Join(directory, "20260102-000000.txt"), "");

            Assert.Equal(
                Path.Join(directory, "20260101-120000.lbf"),
                BoardRecovery.FindLatest(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
