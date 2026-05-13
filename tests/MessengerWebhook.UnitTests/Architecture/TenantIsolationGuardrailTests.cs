namespace MessengerWebhook.UnitTests.Architecture;

/// <summary>
/// CI guardrail: fail build if IgnoreQueryFilters() is used without // ALLOW: justification.
/// Every bypass of the EF tenant filter MUST have a documented reason.
/// </summary>
public class TenantIsolationGuardrailTests
{
    private static readonly string SrcDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../src/MessengerWebhook"));

    [Fact]
    public void NoUnjustifiedIgnoreQueryFilters()
    {
        if (!Directory.Exists(SrcDir))
        {
            // Source dir not available in this build environment — skip
            return;
        }

        var files = Directory.GetFiles(SrcDir, "*.cs", SearchOption.AllDirectories);
        var violations = new List<string>();

        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!line.Contains("IgnoreQueryFilters()")) continue;

                // Justified if current line has // ALLOW:
                if (line.Contains("// ALLOW:")) continue;

                // Justified if the previous non-empty line is a // ALLOW: comment
                var prevIdx = i - 1;
                while (prevIdx >= 0 && string.IsNullOrWhiteSpace(lines[prevIdx]))
                    prevIdx--;

                if (prevIdx >= 0 && lines[prevIdx].TrimStart().StartsWith("// ALLOW:")) continue;

                var relPath = Path.GetRelativePath(SrcDir, file).Replace('\\', '/');
                violations.Add($"{relPath}:{i + 1} — {line.Trim()}");
            }
        }

        Assert.True(violations.Count == 0,
            $"Found {violations.Count} IgnoreQueryFilters() call(s) without '// ALLOW: <reason>' justification:\n" +
            string.Join("\n", violations));
    }
}
