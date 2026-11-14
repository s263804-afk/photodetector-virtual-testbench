using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace PhotoBench.Core.Reporting;

public static class ReleaseMetadata
{
    public static RunMetadata Create(string root, string elfSha256, ushort low = 16, ushort high = 4079)
    {
        using var tools = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reports", "tool-versions.json")));
        string fixtures = Path.Combine(root, "fixtures");
        var hashes = Directory.EnumerateFiles(fixtures, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).ToDictionary(x => Path.GetRelativePath(fixtures, x).Replace('\\', '/'),
                x => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(x))).ToLowerInvariant());
        return new("1.0.0", elfSha256, "PhotoBench1.0.0 / " + RuntimeInformation.FrameworkDescription,
            tools.RootElement.Clone(), hashes, new() {
                ["responsivityAperW"] = .6, ["transimpedanceOhm"] = 100000, ["offsetV"] = .100,
                ["referenceV"] = 3.3, ["maximumCode"] = 4095, ["filterWindow"] = 16,
                ["modelSpacingMs"] = 1, ["noiseSigmaV"] = .002, ["seed"] = 20261112,
                ["lowLimit"] = low, ["highLimit"] = high, ["responseTimeoutMs"] = 2000,
                ["maximumTotalAttempts"] = 3, ["mode"] = "VIRTUAL_FEED"
            });
    }
}
