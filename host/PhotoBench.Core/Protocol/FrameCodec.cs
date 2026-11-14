using System.Globalization;
using System.Text;

namespace PhotoBench.Core.Protocol;

public static class FrameCodec
{
    public static ushort Crc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (byte value in data)
        {
            crc ^= (ushort)(value << 8);
            for (int i = 0; i < 8; ++i)
                crc = unchecked((ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1));
        }
        return crc;
    }

    private static bool ValidCommand(string? text) =>
        text is { Length: >= 1 and <= 16 } &&
        text.All(c => c is >= 'A' and <= 'Z' or '_');

    private static bool ValidArguments(string? text) =>
        text is { Length: >= 1 and <= 400 } &&
        text.All(c => c is >= ' ' and <= '~' && c != '|');

    public static byte[] Encode(Frame frame)
    {
        if (!ValidCommand(frame.Command) || !ValidArguments(frame.Arguments))
            throw new ArgumentException("Invalid frame command or arguments");
        string body = "1|" + frame.Sequence.ToString(CultureInfo.InvariantCulture) +
                      "|" + frame.Command + "|" + frame.Arguments;
        ushort crc = Crc16(Encoding.ASCII.GetBytes(body));
        byte[] encoded = Encoding.ASCII.GetBytes(body + "|" + crc.ToString("X4") + "\n");
        if (encoded.Length > 512) throw new ArgumentException("Frame exceeds 512 bytes");
        return encoded;
    }

    internal static FrameError Decode(ReadOnlySpan<byte> line, out Frame? frame)
    {
        frame = null;
        string raw = Encoding.ASCII.GetString(line);
        string[] fields = raw.Split('|');
        if (fields.Length != 5 || fields[0] != "1" ||
            !ushort.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out ushort seq) ||
            !ValidCommand(fields[2]) || !ValidArguments(fields[3]) ||
            fields[4].Length != 4 ||
            !fields[4].All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F'))
            return FrameError.Structure;
        ushort expected = ushort.Parse(fields[4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        if (Crc16(line[..raw.LastIndexOf('|')]) != expected) return FrameError.Crc;
        frame = new Frame(seq, fields[2], fields[3]);
        return FrameError.None;
    }
}

internal enum FrameError { None, Structure, Crc }

public sealed class FrameParser
{
    private readonly byte[] buffer = new byte[511];
    private int length;
    private bool discarding;
    public uint CrcErrors { get; private set; }
    public uint StructureErrors { get; private set; }
    public uint OverflowErrors { get; private set; }

    public List<Frame> Push(ReadOnlySpan<byte> bytes)
    {
        var frames = new List<Frame>();
        foreach (byte value in bytes)
        {
            if (value == (byte)'\n')
            {
                if (!discarding)
                {
                    FrameError error = FrameCodec.Decode(buffer.AsSpan(0, length), out Frame? frame);
                    if (error == FrameError.Crc) ++CrcErrors;
                    else if (error == FrameError.Structure) ++StructureErrors;
                    else frames.Add(frame!);
                }
                length = 0;
                discarding = false;
            }
            else if (!discarding)
            {
                if (value is < 32 or > 126)
                {
                    ++StructureErrors;
                    discarding = true;
                    length = 0;
                }
                else if (length >= buffer.Length)
                {
                    ++OverflowErrors;
                    discarding = true;
                    length = 0;
                }
                else buffer[length++] = value;
            }
        }
        return frames;
    }
}
