using System.Collections.Concurrent;
using HidSharp;

namespace GameLauncher.Services;

/// <summary>
/// Lê comandos PlayStation (DualShock 4 e DualSense) diretamente por HID, por cabo ou Bluetooth,
/// sem precisar da Steam nem do DS4Windows. Cada comando tem uma thread que lê os relatórios;
/// <see cref="Buttons"/> junta o estado de todos.
/// </summary>
public sealed class PlayStationInput : IDisposable
{
    private readonly ConcurrentDictionary<string, ushort> _state = new();
    private readonly ConcurrentDictionary<string, string> _names = new();
    private Timer? _scanTimer;
    private volatile bool _disposed;

    /// <summary>Botões carregados em todos os comandos PlayStation ligados.</summary>
    public ushort Buttons
    {
        get
        {
            ushort m = 0;
            foreach (var v in _state.Values) m |= v;
            return m;
        }
    }

    public IReadOnlyCollection<string> ConnectedNames => _names.Values.ToList();

    public void Start()
    {
        // Procura comandos novos de 3 em 3 segundos (ligados depois de a app abrir).
        _scanTimer = new Timer(_ => Scan(), null, TimeSpan.Zero, TimeSpan.FromSeconds(3));
    }

    private void Scan()
    {
        if (_disposed) return;
        try
        {
            foreach (var dev in DeviceList.Local.GetHidDevices(PlayStationReport.SonyVendorId))
            {
                if (!PlayStationReport.IsSupported(dev.ProductID)) continue;
                var key = dev.DevicePath;
                if (_state.ContainsKey(key)) continue;

                int len;
                try { len = dev.GetMaxInputReportLength(); } catch { continue; }
                if (len < 8) continue;

                if (!dev.TryOpen(out HidStream stream)) continue;
                _state[key] = 0;
                bool dualSense = PlayStationReport.DualSenseIds.Contains(dev.ProductID);
                bool bluetooth = len != PlayStationReport.UsbReportLength;
                _names[key] = (dualSense ? "DualSense" : "DualShock 4") + (bluetooth ? " (Bluetooth)" : " (USB)");
                var thread = new Thread(() => ReadLoop(key, stream, len, dualSense, bluetooth))
                {
                    IsBackground = true,
                    Name = "PlayStation pad",
                };
                thread.Start();
            }
        }
        catch { /* sem permissões ou HID indisponível: ignora */ }
    }

    private void ReadLoop(string key, HidStream stream, int length, bool dualSense, bool bluetooth)
    {
        var buf = new byte[length];
        try
        {
            stream.ReadTimeout = 1000;
            while (!_disposed)
            {
                int n;
                try { n = stream.Read(buf, 0, buf.Length); }
                catch (TimeoutException) { continue; }

                if (n <= 0) continue;
                if (PlayStationReport.Parse(buf, n, dualSense, bluetooth) is { } mask) _state[key] = mask;
            }
        }
        catch { /* comando desligado */ }
        finally
        {
            try { stream.Dispose(); } catch { }
            _state.TryRemove(key, out _);
            _names.TryRemove(key, out _);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _scanTimer?.Dispose();
    }
}
