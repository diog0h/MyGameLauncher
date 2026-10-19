namespace GameLauncher.Services;

/// <summary>
/// Botões num formato comum a todos os comandos (os valores seguem os do XInput).
/// Os comandos PlayStation são convertidos para isto: ✕=A, ○=B, □=X, △=Y, Options=Start, Share/Create=Back, PS=Guide.
/// </summary>
public static class PadBits
{
    public const ushort Up = 0x0001, Down = 0x0002, Left = 0x0004, Right = 0x0008,
        Start = 0x0010, Back = 0x0020, LB = 0x0100, RB = 0x0200, Guide = 0x0400,
        A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;
}

/// <summary>Converte relatórios HID do DualShock 4 e do DualSense em <see cref="PadBits"/>.</summary>
public static class PlayStationReport
{
    public const int SonyVendorId = 0x054C;

    public static readonly HashSet<int> DualShock4Ids = new() { 0x05C4, 0x09CC, 0x0BA0 };
    public static readonly HashSet<int> DualSenseIds = new() { 0x0CE6, 0x0DF2 };

    public static bool IsSupported(int productId) =>
        DualShock4Ids.Contains(productId) || DualSenseIds.Contains(productId);

    /// <summary>
    /// Lê um relatório (byte 0 = ID do relatório). Devolve null se o formato não for conhecido.
    /// Formatos:
    ///  DS4 USB / Bluetooth simples (0x01): sticks em 1-4, botões em 5-7.
    ///  DS4 Bluetooth completo (0x11): igual ao USB mas 2 bytes mais à frente.
    ///  DualSense USB (0x01, 64 bytes): sticks em 1-4, botões em 8-10.
    ///  DualSense Bluetooth completo (0x31): igual ao USB mas 1 byte mais à frente.
    ///  DualSense Bluetooth simples (0x01, curto): sticks em 1-4, botões em 5-7.
    /// </summary>
    public static ushort? Parse(byte[] r, int length, bool dualSense)
    {
        if (length < 8) return null;
        int sticks, buttons;
        switch (r[0])
        {
            case 0x01 when !dualSense: sticks = 1; buttons = 5; break;
            case 0x11 when !dualSense: sticks = 3; buttons = 7; break;
            case 0x01 when dualSense && length >= 32: sticks = 1; buttons = 8; break;
            case 0x01 when dualSense: sticks = 1; buttons = 5; break;
            case 0x31 when dualSense: sticks = 2; buttons = 9; break;
            default: return null;
        }
        if (length < buttons + 3) return null;

        byte b0 = r[buttons], b1 = r[buttons + 1], b2 = r[buttons + 2];
        ushort mask = 0;

        // Cruz direcional ("hat"): 0=cima, 1=cima-dir, 2=dir, … 7=cima-esq, 8=solta
        switch (b0 & 0x0F)
        {
            case 0: mask |= PadBits.Up; break;
            case 1: mask |= PadBits.Up | PadBits.Right; break;
            case 2: mask |= PadBits.Right; break;
            case 3: mask |= PadBits.Down | PadBits.Right; break;
            case 4: mask |= PadBits.Down; break;
            case 5: mask |= PadBits.Down | PadBits.Left; break;
            case 6: mask |= PadBits.Left; break;
            case 7: mask |= PadBits.Up | PadBits.Left; break;
        }

        if ((b0 & 0x10) != 0) mask |= PadBits.X; // □
        if ((b0 & 0x20) != 0) mask |= PadBits.A; // ✕
        if ((b0 & 0x40) != 0) mask |= PadBits.B; // ○
        if ((b0 & 0x80) != 0) mask |= PadBits.Y; // △
        if ((b1 & 0x01) != 0) mask |= PadBits.LB;    // L1
        if ((b1 & 0x02) != 0) mask |= PadBits.RB;    // R1
        if ((b1 & 0x10) != 0) mask |= PadBits.Back;  // Share / Create
        if ((b1 & 0x20) != 0) mask |= PadBits.Start; // Options
        if ((b2 & 0x01) != 0) mask |= PadBits.Guide; // PS

        // Stick esquerdo (0–255, 128 = centro; Y cresce para baixo)
        const int dead = 64;
        int lx = r[sticks] - 128, ly = r[sticks + 1] - 128;
        if (lx < -dead) mask |= PadBits.Left;
        if (lx > dead) mask |= PadBits.Right;
        if (ly < -dead) mask |= PadBits.Up;
        if (ly > dead) mask |= PadBits.Down;

        return mask;
    }
}
