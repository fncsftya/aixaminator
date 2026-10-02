using System.Text;
using Aixaminator.Features;

namespace Aixaminator.Tests.Features;

public class BinaryContentDetectorTests
{
    [Fact]
    public async Task Plain_text_is_not_binary()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Just some text\twith tabs\r\nand newlines."));

        Assert.False(await BinaryContentDetector.IsBinaryAsync(stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Empty_stream_is_not_binary()
    {
        using var stream = new MemoryStream();

        Assert.False(await BinaryContentDetector.IsBinaryAsync(stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Null_bytes_indicate_binary_content()
    {
        using var stream = new MemoryStream([0x48, 0x00, 0x49, 0x50]);

        Assert.True(await BinaryContentDetector.IsBinaryAsync(stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Mostly_control_characters_indicate_binary_content()
    {
        using var stream = new MemoryStream([0x01, 0x02, 0x03, 0x41]);

        Assert.True(await BinaryContentDetector.IsBinaryAsync(stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Stream_position_is_restored()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("abcdef"));
        stream.Position = 2;

        await BinaryContentDetector.IsBinaryAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal(2, stream.Position);
    }
}
