using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace GameLauncher.Services;

public enum PadButton { Up, Down, Left, Right, A, B, X, Y, Start, Back, LB, RB, Guide }

public enum PadKind { Xbox, PlayStation }

/// <summary>
/// Lê comandos ~60 vezes por segundo e transforma os botões em eventos:
/// comandos Xbox (XInput) e PlayStation (HID, ver <see cref="PlayStationInput"/>).
/// O estado dos dois é juntado antes de detetar toques, por isso um comando que apareça
/// das duas formas (ex.: PlayStation com DS4Windows) não gera ações a dobrar.
/// Direções repetem quando se mantém carregado.
/// </summary>
public sealed class GamepadService : IDisposable
{
    public event Action<PadButton>? Pressed;

    /// <summary>Tipo do último comando em que se carregou num botão (para mostrar os ícones certos).</summary>
    public PadKind LastKind { get; private set; } = PadKind.Xbox;
    private ushort _lastPs, _lastXbox;

    private readonly DispatcherTimer _timer;
    private readonly PlayStationInput _playStation = new();
    private ushort _lastButtons;
    private PadButton? _heldDir;
    private DateTime _nextRepeat;
    private bool _xinputAvailable = true;
    private readonly bool[] _slotConnected = new bool[4];
    private readonly DateTime[] _slotNextCheck = new DateTime[4];

    public IReadOnlyCollection<string> PlayStationControllers => _playStation.ConnectedNames;
    public int XboxControllers { get; private set; }

    public GamepadService()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => Poll();
    }

    public void Start()
    {
        _playStation.Start();
        _timer.Start();
    }

    public void Dispose()
    {
        _timer.Stop();
        _playStation.Dispose();
    }

    private void Poll()
    {
        ushort ps = _playStation.Buttons;
        ushort xbox = 0;

        if (_xinputAvailable)
        {
            int connected = 0;
            var nowCheck = DateTime.UtcNow;
            for (int i = 0; i < 4; i++)
            {
                // Perguntar por portas vazias é lento: só volta a tentar passado 1 segundo.
                if (!_slotConnected[i] && nowCheck < _slotNextCheck[i]) continue;
                try
                {
                    if (XInputGetState(i, out var state) != 0)
                    {
                        _slotConnected[i] = false;
                        _slotNextCheck[i] = nowCheck.AddSeconds(1);
                        continue;
                    }
                    _slotConnected[i] = true;
                    connected++;
                    var pad = state.Gamepad;
                    xbox |= pad.Buttons;
                    const short dead = 16000; // o stick esquerdo conta como setas
                    if (pad.ThumbLY > dead) xbox |= PadBits.Up;
                    if (pad.ThumbLY < -dead) xbox |= PadBits.Down;
                    if (pad.ThumbLX < -dead) xbox |= PadBits.Left;
                    if (pad.ThumbLX > dead) xbox |= PadBits.Right;
                }
                catch (DllNotFoundException) { _xinputAvailable = false; break; }
                catch (EntryPointNotFoundException) { _xinputAvailable = false; break; }
            }
            XboxControllers = connected;
        }

        // Qual o comando usado? (Um PlayStation com DS4Windows aparece nos dois: conta como PlayStation.)
        if ((ps & ~_lastPs) != 0) LastKind = PadKind.PlayStation;
        else if ((xbox & ~_lastXbox) != 0 && ps == 0) LastKind = PadKind.Xbox;
        _lastPs = ps;
        _lastXbox = xbox;

        ushort buttons = (ushort)(ps | xbox);
        ushort pressedNow = (ushort)(buttons & ~_lastButtons);
        _lastButtons = buttons;

        Fire(pressedNow, PadBits.A, PadButton.A);
        Fire(pressedNow, PadBits.B, PadButton.B);
        Fire(pressedNow, PadBits.X, PadButton.X);
        Fire(pressedNow, PadBits.Y, PadButton.Y);
        Fire(pressedNow, PadBits.Start, PadButton.Start);
        Fire(pressedNow, PadBits.Back, PadButton.Back);
        Fire(pressedNow, PadBits.LB, PadButton.LB);
        Fire(pressedNow, PadBits.RB, PadButton.RB);
        Fire(pressedNow, PadBits.Guide, PadButton.Guide);

        // Direções: primeiro toque imediato, depois repete enquanto estiver carregado.
        PadButton? dir =
            (buttons & PadBits.Up) != 0 ? PadButton.Up :
            (buttons & PadBits.Down) != 0 ? PadButton.Down :
            (buttons & PadBits.Left) != 0 ? PadButton.Left :
            (buttons & PadBits.Right) != 0 ? PadButton.Right : null;

        var now = DateTime.UtcNow;
        if (dir is null) { _heldDir = null; return; }
        if (dir != _heldDir)
        {
            _heldDir = dir;
            _nextRepeat = now.AddMilliseconds(380);
            Pressed?.Invoke(dir.Value);
        }
        else if (now >= _nextRepeat)
        {
            _nextRepeat = now.AddMilliseconds(120);
            Pressed?.Invoke(dir.Value);
        }
    }

    private void Fire(ushort pressedNow, ushort mask, PadButton b)
    {
        if ((pressedNow & mask) != 0) Pressed?.Invoke(b);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [DllImport("xinput1_4.dll")]
    private static extern int XInputGetState(int userIndex, out XInputState state);
}
