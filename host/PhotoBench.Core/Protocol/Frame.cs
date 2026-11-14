namespace PhotoBench.Core.Protocol;

public sealed record Frame(ushort Sequence, string Command, string Arguments);
