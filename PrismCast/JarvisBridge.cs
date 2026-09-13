using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using PrismCast.Hosting;
using System.Text.Json;

namespace PrismCast;

/// <summary>
/// Small, dependency-free IPC surface used by JARVIS. PrismCast remains fully
/// standalone; this bridge only exposes launch and read-only operational telemetry.
/// </summary>
internal sealed class JarvisBridge : IDisposable
{
    internal const string OpenChannel = "PrismCast.Jarvis.Open";
    internal const string StatusChannel = "PrismCast.Jarvis.GetStatus";
    internal const string TelemetryChannel = "PrismCast.Jarvis.GetTelemetry";

    private readonly SessionController _session;
    private readonly Action _openWindow;
    private readonly ICallGateProvider<string, object> _open;
    private readonly ICallGateProvider<string> _status;
    private readonly ICallGateProvider<string> _telemetry;

    internal JarvisBridge(IDalamudPluginInterface pi, SessionController session, Action openWindow)
    {
        _session = session;
        _openWindow = openWindow;

        _open = pi.GetIpcProvider<string, object>(OpenChannel);
        _status = pi.GetIpcProvider<string>(StatusChannel);
        _telemetry = pi.GetIpcProvider<string>(TelemetryChannel);

        _open.RegisterAction(_ => _openWindow());
        _status.RegisterFunc(GetStatusJson);
        _telemetry.RegisterFunc(GetTelemetryJson);
    }

    private string GetStatusJson()
    {
        var state = _session.Mode switch
        {
            PrismMode.Hosting => "HOSTING",
            PrismMode.Viewing => "VIEWING",
            _ => "READY",
        };

        var title = CurrentTitle();
        var viewers = _session.Mode == PrismMode.Hosting ? ViewerPresenceRegistry.GetViewerNames().Count : 0;
        var detail = !string.IsNullOrWhiteSpace(title)
            ? $"{_session.Status}: {title}"
            : _session.Status;

        return JsonSerializer.Serialize(new
        {
            State = state,
            Detail = detail,
            Badge = viewers > 0 ? viewers.ToString() : string.Empty,
            Severity = _session.Mode == PrismMode.Idle ? "normal" : "active",
            Online = true,
            UpdatedUtc = DateTime.UtcNow,
        });
    }

    private string GetTelemetryJson()
    {
        var title = CurrentTitle();
        var viewers = _session.Mode == PrismMode.Hosting ? ViewerPresenceRegistry.GetViewerNames().Count : 0;
        var mode = _session.Mode switch
        {
            PrismMode.Hosting => "HOSTING",
            PrismMode.Viewing => "VIEWING",
            _ => "IDLE",
        };
        var kind = _session.IsScreenShareSession ? "Screen share" : "Media";

        string headline;
        string recommendation;
        double priority;
        if (_session.Mode == PrismMode.Hosting)
        {
            headline = string.IsNullOrWhiteSpace(title) ? "Theater is live" : $"Now hosting: {title}";
            recommendation = $"PrismCast is hosting {kind.ToLowerInvariant()} for {viewers} viewer{(viewers == 1 ? string.Empty : "s")}. Open theater controls to manage playback.";
            priority = 35;
        }
        else if (_session.Mode == PrismMode.Viewing)
        {
            headline = string.IsNullOrWhiteSpace(title) ? "Connected to a theater" : $"Now viewing: {title}";
            recommendation = "PrismCast is connected to a live theater session. Open PrismCast to manage your viewer controls.";
            priority = 25;
        }
        else
        {
            headline = "Theater system ready";
            recommendation = "PrismCast is idle and ready to host or join a theater session.";
            priority = 10;
        }

        return JsonSerializer.Serialize(new
        {
            Headline = headline,
            Recommendation = recommendation,
            Metrics = new Dictionary<string, string>
            {
                ["Mode"] = mode,
                ["Viewers"] = _session.Mode == PrismMode.Hosting ? viewers.ToString() : "--",
                ["Source"] = _session.Mode == PrismMode.Idle ? "--" : kind,
            },
            Values = new Dictionary<string, double>
            {
                ["ViewerCount"] = viewers,
                ["Hosting"] = _session.Mode == PrismMode.Hosting ? 1 : 0,
                ["Viewing"] = _session.Mode == PrismMode.Viewing ? 1 : 0,
            },
            PrimaryDeepLink = "manage",
            SecondaryDeepLink = string.Empty,
            PriorityScore = priority,
            UpdatedUtc = DateTime.UtcNow,
        });
    }

    private string CurrentTitle()
        => _session.CurrentTitle ?? _session.ViewerState?.Title ?? string.Empty;

    public void Dispose()
    {
        _open.UnregisterAction();
        _status.UnregisterFunc();
        _telemetry.UnregisterFunc();
    }
}
