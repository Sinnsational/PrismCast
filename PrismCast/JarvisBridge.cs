using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using System.Text.Json;

namespace PrismCast;

/// <summary>
/// Small, dependency-free IPC surface used by JARVIS. PrismCast remains fully
/// standalone; this bridge only exposes launch and read-only status capabilities.
/// </summary>
internal sealed class JarvisBridge : IDisposable
{
    internal const string OpenChannel = "PrismCast.Jarvis.Open";
    internal const string StatusChannel = "PrismCast.Jarvis.GetStatus";

    private readonly SessionController _session;
    private readonly Action _openWindow;
    private readonly ICallGateProvider<string, object> _open;
    private readonly ICallGateProvider<string> _status;

    internal JarvisBridge(IDalamudPluginInterface pi, SessionController session, Action openWindow)
    {
        _session = session;
        _openWindow = openWindow;

        _open = pi.GetIpcProvider<string, object>(OpenChannel);
        _status = pi.GetIpcProvider<string>(StatusChannel);

        _open.RegisterAction(_ => _openWindow());
        _status.RegisterFunc(GetStatusJson);
    }

    private string GetStatusJson()
    {
        var state = _session.Mode switch
        {
            PrismMode.Hosting => "Hosting",
            PrismMode.Viewing => "Viewing",
            _ => "Ready",
        };

        var detail = !string.IsNullOrWhiteSpace(_session.CurrentTitle)
            ? $"{_session.Status}: {_session.CurrentTitle}"
            : _session.Status;

        return JsonSerializer.Serialize(new
        {
            State = state,
            Detail = detail,
            Badge = string.Empty,
            Severity = "normal",
            Online = true,
            UpdatedUtc = DateTime.UtcNow,
        });
    }

    public void Dispose()
    {
        _open.UnregisterAction();
        _status.UnregisterFunc();
    }
}
