using System.Runtime.InteropServices;

namespace NotasDesktop;

static class Native
{
    public const int WM_NCHITTEST = 0x0084;
    public const int WM_NCLBUTTONDOWN = 0x00A1;
    public const int WM_HOTKEY = 0x0312;

    public const int HTCLIENT = 1;
    public const int HTCAPTION = 2;
    public const int HTLEFT = 10;
    public const int HTRIGHT = 11;
    public const int HTBOTTOM = 15;
    public const int HTBOTTOMLEFT = 16;
    public const int HTBOTTOMRIGHT = 17;

    public const int WM_MOUSEACTIVATE = 0x0021;
    public const int MA_NOACTIVATE = 3;

    public const int GWLP_HWNDPARENT = -8;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);

    const int ULW_ALPHA = 2;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct BLENDFUNCTION
    {
        public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Point pptDst, ref Size psize,
        IntPtr hdcSrc, ref Point pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);

    // ---------- RichEdit: formatação e renderização ----------

    public const int EM_GETRECT = 0x00B2;
    public const int EM_GETCHARFORMAT = 0x043A;
    public const int EM_SETCHARFORMAT = 0x0444;
    public const int EM_FORMATRANGE = 0x0439;
    public const int EM_SETFONTSIZE = 0x04DF;
    public const int SCF_SELECTION = 1;

    public const uint CFM_BOLD = 0x1, CFM_ITALIC = 0x2, CFM_UNDERLINE = 0x4, CFM_STRIKEOUT = 0x8;
    public const uint CFM_BACKCOLOR = 0x04000000, CFM_FACE = 0x20000000, CFM_COLOR = 0x40000000, CFM_SIZE = 0x80000000;
    public const uint CFE_AUTOBACKCOLOR = 0x04000000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CHARFORMAT2
    {
        public int cbSize;
        public uint dwMask;
        public uint dwEffects;
        public int yHeight; // em twips (1/20 de ponto)
        public int yOffset;
        public int crTextColor;
        public byte bCharSet;
        public byte bPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szFaceName;
        public short wWeight;
        public short sSpacing;
        public int crBackColor;
        public int lcid;
        public int dwReserved;
        public short sStyle;
        public short wKerning;
        public byte bUnderlineType;
        public byte bAnimation;
        public byte bRevAuthor;
        public byte bUnderlineColor;

        public static CHARFORMAT2 Novo() => new() { cbSize = Marshal.SizeOf<CHARFORMAT2>(), szFaceName = "" };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct CHARRANGE { public int cpMin, cpMax; }

    [StructLayout(LayoutKind.Sequential)]
    public struct FORMATRANGE
    {
        public IntPtr hdc, hdcTarget;
        public RECT rc, rcPage;
        public CHARRANGE chrg;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref CHARFORMAT2 lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref FORMATRANGE lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, out RECT lParam);

    [DllImport("gdi32.dll")]
    public static extern int GetDeviceCaps(IntPtr hdc, int index);
    public const int LOGPIXELSX = 88, LOGPIXELSY = 90;

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    /// <summary>Mostra um bitmap ARGB numa janela WS_EX_LAYERED, com transparência por pixel.</summary>
    public static void AplicarBitmapEmCamada(IntPtr janela, Bitmap bitmap, Point posicao, double opacidade)
    {
        IntPtr telaDc = GetDC(IntPtr.Zero);
        IntPtr memoriaDc = CreateCompatibleDC(telaDc);
        IntPtr hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
        IntPtr anterior = SelectObject(memoriaDc, hBitmap);
        try
        {
            var tamanho = bitmap.Size;
            var origem = Point.Empty;
            var mistura = new BLENDFUNCTION
            {
                SourceConstantAlpha = (byte)Math.Round(Math.Clamp(opacidade, 0.05, 1.0) * 255),
                AlphaFormat = 1, // AC_SRC_ALPHA
            };
            UpdateLayeredWindow(janela, telaDc, ref posicao, ref tamanho, memoriaDc, ref origem, 0, ref mistura, ULW_ALPHA);
        }
        finally
        {
            SelectObject(memoriaDc, anterior);
            DeleteObject(hBitmap);
            DeleteDC(memoriaDc);
            ReleaseDC(IntPtr.Zero, telaDc);
        }
    }

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    public static extern int SetWindowTheme(IntPtr hWnd, string? subAppName, string? subIdList);

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

/// <summary>Janela invisível que recebe o atalho global Ctrl+Alt+N.</summary>
sealed class HotkeyWindow : NativeWindow, IDisposable
{
    const int Id = 1;
    readonly Action acao;

    public bool Registrado { get; }

    public HotkeyWindow(Action acao)
    {
        this.acao = acao;
        CreateHandle(new CreateParams());
        Registrado = Native.RegisterHotKey(Handle, Id,
            Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, (uint)Keys.N);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY) acao();
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (Registrado) Native.UnregisterHotKey(Handle, Id);
        DestroyHandle();
    }
}
