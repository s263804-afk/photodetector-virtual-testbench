using System.Text;
using PhotoBench.Core.Protocol;

namespace PhotoBench.Tests;

public class ProtocolTests
{
    [Fact]
    public void CrcKnownVectorIs29B1()
    {
        Assert.Equal(0x29B1, FrameCodec.Crc16("123456789"u8));
    }

    [Theory]
    [InlineData(0, "HELLO", "-", "1|0|HELLO|-|CB9D\n")]
    [InlineData(1, "PING", "-", "1|1|PING|-|CFA9\n")]
    [InlineData(3, "FEED", "124,869,4095", "1|3|FEED|124,869,4095|643E\n")]
    [InlineData(3, "DATA", "0;124:124:0;869:496:0;4095:1696:2",
        "1|3|DATA|0;124:124:0;869:496:0;4095:1696:2|609D\n")]
    public void GoldenFramesMatchIndependentBytes(int sequence, string command, string args, string wire)
    {
        var frame = new Frame((ushort)sequence, command, args);
        Assert.Equal(Encoding.ASCII.GetBytes(wire), FrameCodec.Encode(frame));
        Assert.Equal(frame, Assert.Single(new FrameParser().Push(Encoding.ASCII.GetBytes(wire))));
    }

    [Fact]
    public void EveryFragmentBoundaryAndCoalescedFramesAreSupported()
    {
        byte[] bytes = "1|1|PING|-|CFA9\n"u8.ToArray();
        for (int split = 0; split <= bytes.Length; ++split)
        {
            var parser = new FrameParser();
            var frames = parser.Push(bytes.AsSpan(0, split));
            frames.AddRange(parser.Push(bytes.AsSpan(split)));
            Assert.Equal(new Frame(1, "PING", "-"), Assert.Single(frames));
        }
        var coalesced = new FrameParser().Push(Encoding.ASCII.GetBytes(
            "1|0|HELLO|-|CB9D\n1|1|PING|-|CFA9\n"));
        Assert.Equal(2, coalesced.Count);
    }

    [Theory]
    [InlineData("1|1|PING|-|0000\n", true)]
    [InlineData("1|1|PING|-|CFAg\n", false)]
    [InlineData("1|1|FEED||0000\n", false)]
    [InlineData("1|PING|FFFF\n", false)]
    [InlineData("1|-1|PING|-|0000\n", false)]
    [InlineData("2|1|PING|-|0000\n", false)]
    public void BadFramesAreDroppedAndParserRecovers(string bad, bool crc)
    {
        var parser = new FrameParser();
        Assert.Empty(parser.Push(Encoding.ASCII.GetBytes(bad)));
        Assert.Equal(1u, crc ? parser.CrcErrors : parser.StructureErrors);
        Assert.Single(parser.Push("1|1|PING|-|CFA9\n"u8));
    }

    [Fact]
    public void OverflowAndNonAsciiAreBoundedAndRecoverable()
    {
        var parser = new FrameParser();
        byte[] overlong = Enumerable.Repeat((byte)'X', 512).Append((byte)'\n').ToArray();
        Assert.Empty(parser.Push(overlong));
        Assert.Equal(1u, parser.OverflowErrors);
        Assert.Empty(parser.Push(new byte[] { 255, (byte)'\n' }));
        Assert.Equal(1u, parser.StructureErrors);
        Assert.Single(parser.Push("1|1|PING|-|CFA9\n"u8));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad|argument")]
    [InlineData("nonascii\u00E9")]
    [InlineData("line\nbreak")]
    public void EncoderRejectsUnframedOrNonAsciiArguments(string args)
    {
        Assert.Throws<ArgumentException>(() => FrameCodec.Encode(new Frame(1, "PING", args)));
    }
}
