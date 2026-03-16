using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace AWOMS.NOC.Agent.Services;

/// <summary>
/// Serilog enricher that appends a "Caller" property containing the source file name and
/// line number of the AWOMS code that initiated the log call (e.g. "Worker.cs:87").
/// Requires symbols embedded in the binary (<DebugType>embedded</DebugType> in the .csproj).
/// </summary>
public class CallerEnricher : ILogEventEnricher
{
    private static readonly string[] SkippedNamespacePrefixes =
    [
        "Serilog.",
        "Microsoft.Extensions.Logging.",
        "Microsoft.Extensions.Hosting.",
        "System.",
        "Castle.",
    ];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var stack = new StackTrace(fNeedFileInfo: true);

        for (int i = 0; i < stack.FrameCount; i++)
        {
            var frame = stack.GetFrame(i);
            var method = frame?.GetMethod();
            if (method == null) continue;

            var declaringType = method.DeclaringType?.FullName ?? string.Empty;

            if (SkippedNamespacePrefixes.Any(prefix => declaringType.StartsWith(prefix, StringComparison.Ordinal)))
                continue;

            // Skip the enricher itself
            if (declaringType.Contains(nameof(CallerEnricher), StringComparison.Ordinal))
                continue;

            var fileName = frame!.GetFileName();
            if (string.IsNullOrEmpty(fileName)) continue;

            var shortName = Path.GetFileName(fileName);
            var lineNumber = frame.GetFileLineNumber();

            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("Caller", $"{shortName}:{lineNumber}"));
            return;
        }

        // Fallback when no suitable frame is found (e.g. fully trimmed/inlined)
        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("Caller", "unknown"));
    }
}
