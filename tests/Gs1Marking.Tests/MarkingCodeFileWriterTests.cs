using Gs1Marking.Client;
using Gs1Marking.Client.Application;
using Gs1Marking.Client.Infrastructure;

namespace Gs1Marking.Tests;

public class MarkingCodeFileWriterTests
{
    [Fact]
    public async Task AppendAsync_WritesEachCodeOnItsOwnLine()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gs1-marking-{Guid.NewGuid():N}.txt");

        try
        {
            await using (var writer = new MarkingCodeFileWriter(path))
            {
                await writer.AppendAsync("(01)00012345678905(21)ABC0000001(17)260101(10)LOT001");
                await writer.AppendAsync("(01)00012345678905(21)ABC0000002(17)260102(10)LOT002");
            }

            var lines = await File.ReadAllLinesAsync(path);

            Assert.Equal(2, lines.Length);
            Assert.EndsWith("(10)LOT001", lines[0]);
            Assert.EndsWith("(10)LOT002", lines[1]);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
