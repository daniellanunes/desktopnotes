namespace NotasDesktop;

/// <summary>
/// Janela com transparência por pixel que desenha a anotação direto sobre o papel de parede.
/// Pixels totalmente transparentes deixam o clique passar para a área de trabalho.
/// Durante a edição ela vira uma moldura leve atrás do editor e repassa os cliques para ele.
/// </summary>
sealed class DesktopLayer : Form
{
    readonly NoteForm nota;
    Point? pressionado;
    bool selecionando;

    public DesktopLayer(NoteForm nota)
    {
        this.nota = nota;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // A área de trabalho (Progman) como dona mantém a anotação visível no Win+D.
        var areaDeTrabalho = Native.FindWindow("Progman", null);
        if (areaDeTrabalho != IntPtr.Zero)
            Native.SetWindowLongPtr(Handle, Native.GWLP_HWNDPARENT, areaDeTrabalho);
        AplicarSempreVisivel();
        Redesenhar();
    }

    /// <summary>Alfinete ligado: "sempre visível" (por cima até de janelas maximizadas); desligado: nível normal.</summary>
    public void AplicarSempreVisivel()
    {
        if (!IsHandleCreated) return;
        Native.SetWindowPos(Handle, nota.Dados.Fixada ? Native.HWND_TOPMOST : Native.HWND_NOTOPMOST, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        Redesenhar();
    }

    public void Redesenhar()
    {
        if (!IsHandleCreated || Width <= 0 || Height <= 0) return;
        if (nota.Editando)
        {
            using var moldura = DesenharMoldura();
            Native.AplicarBitmapEmCamada(Handle, moldura, Location, 1.0);
        }
        else
        {
            using var desenho = nota.DesenharTexto(Size);
            Native.AplicarBitmapEmCamada(Handle, desenho, Location, nota.Dados.Opacidade);
        }
    }

    /// <summary>Sobe a anotação acima das janelas abertas (fica "sempre visível" por um instante e volta ao normal).</summary>
    public void TrazerParaFrente()
    {
        const uint flags = Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE;
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, flags);
        if (!nota.Dados.Fixada) Native.SetWindowPos(Handle, Native.HWND_NOTOPMOST, 0, 0, 0, 0, flags);
    }

    Bitmap DesenharMoldura()
    {
        var bmp = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        // Véu escuro atrás de texto claro (ou claro atrás de texto escuro) só enquanto edita.
        var veu = nota.TextoClaro ? Color.Black : Color.White;
        var linha = nota.TextoClaro ? Color.White : Color.Black;
        g.Clear(Color.FromArgb(nota.Dados.Fixada ? 150 : 45, veu)); // fixada: mesmo fundo escuro da exibição
        using (var barra = new SolidBrush(Color.FromArgb(55, veu)))
            g.FillRectangle(barra, 0, 0, Width, nota.AlturaBarra);
        // "pegador" pontilhado no meio da faixa de cima: arrastar ali move a anotação
        using (var pontos = new SolidBrush(Color.FromArgb(150, linha)))
        {
            int tamanho = Math.Max(2, nota.Margem / 3), meio = Width / 2, y0 = nota.AlturaBarra / 2 - tamanho;
            for (int i = -3; i <= 3; i++)
                for (int j = 0; j < 2; j++)
                    g.FillRectangle(pontos, meio + i * tamanho * 2, y0 + j * tamanho * 2, tamanho, tamanho);
        }
        using var caneta = new Pen(Color.FromArgb(110, linha)) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        g.DrawRectangle(caneta, 0, 0, Width - 1, Height - 1);
        if (!nota.Dados.Travada)
        {
            caneta.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
            int x = Width - 3, y = Height - 3;
            for (int i = 4; i <= 12; i += 4) g.DrawLine(caneta, x - i, y, x, y - i);
        }
        return bmp;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_MOUSEACTIVATE)
        {
            m.Result = Native.MA_NOACTIVATE; // clicar aqui não tira o foco do editor
            return;
        }
        base.WndProc(ref m);
        if (m.Msg != Native.WM_NCHITTEST || !nota.Editando || nota.Dados.Travada || (int)m.Result != Native.HTCLIENT) return;

        long lp = (long)m.LParam;
        var p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
        int borda = nota.Margem, canto = nota.Margem * 2;
        bool direita = p.X >= Width - borda, baixo = p.Y >= Height - borda;
        if (p.X >= Width - canto && p.Y >= Height - canto) m.Result = Native.HTBOTTOMRIGHT;
        else if (baixo) m.Result = Native.HTBOTTOM;
        else if (direita) m.Result = Native.HTRIGHT;
        else if (p.X < borda) m.Result = Native.HTLEFT;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Right) { nota.MostrarMenu(Cursor.Position); return; }
        if (e.Button != MouseButtons.Left) return;

        if (!nota.Editando) { pressionado = e.Location; return; }
        if (e.Y < nota.AlturaBarra) { Mover(); return; }
        nota.PosicionarCursor(PointToScreen(e.Location), ModifierKeys == Keys.Shift);
        selecionando = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = nota.Editando && e.Y < nota.AlturaBarra && !nota.Dados.Travada ? Cursors.SizeAll : Cursors.Default;
        if (selecionando)
            nota.PosicionarCursor(PointToScreen(e.Location), estender: true);
        // Fora da edição a anotação não se move: só é possível reposicionar editando (✥ ou faixa de cima).
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        selecionando = false;
        if (pressionado != null && e.Button == MouseButtons.Left)
        {
            pressionado = null;
            // Clique no alfinete o desliga; numa bolinha de tarefa só marca/desmarca, sem entrar na edição.
            if (nota.Dados.Fixada && nota.AreaAlfinete(Size).Contains(e.Location))
                nota.AlternarFixar();
            else if (!nota.TentarMarcarTarefa(PointToScreen(e.Location)))
                nota.IniciarEdicao(PointToScreen(e.Location));
        }
    }

    /// <summary>Começa a arrastar a anotação com o mouse (o editor e a barra acompanham).</summary>
    public void Mover()
    {
        if (nota.Dados.Travada) return;
        Native.ReleaseCapture();
        Native.SendMessage(Handle, Native.WM_NCLBUTTONDOWN, Native.HTCAPTION, IntPtr.Zero);
    }
}
