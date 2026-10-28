namespace NotasDesktop;

/// <summary>Camada escura sobre a tela inteira onde o usuário arrasta a área da nova nota.</summary>
sealed class PickerOverlay : Form
{
    const int LarguraPadrao = 380;
    const int AlturaPadrao = 200;

    public event Action<Rectangle>? AreaEscolhida;

    Point? inicio;
    Rectangle area;

    public PickerOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        BackColor = Color.Black;
        Opacity = 0.4;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        KeyPreview = true;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right) { Close(); return; }
        if (e.Button != MouseButtons.Left) return;
        inicio = e.Location;
        area = new Rectangle(e.Location, Size.Empty);
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (inicio is not { } p) return;
        area = Rectangle.FromLTRB(Math.Min(p.X, e.X), Math.Min(p.Y, e.Y), Math.Max(p.X, e.X), Math.Max(p.Y, e.Y));
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || inicio is not { } p) return;
        var escolhida = area;
        // Um clique simples (ou arrasto muito pequeno) cria uma nota de tamanho padrão.
        if (escolhida.Width < LogicalToDeviceUnits(60) || escolhida.Height < LogicalToDeviceUnits(40))
            escolhida = new Rectangle(p, new Size(LogicalToDeviceUnits(LarguraPadrao), LogicalToDeviceUnits(AlturaPadrao)));
        escolhida.Offset(Bounds.Location); // coordenadas do formulário -> coordenadas da tela
        Close();
        AreaEscolhida?.Invoke(escolhida);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var tela = Screen.PrimaryScreen!.Bounds;
        tela.Offset(-Bounds.X, -Bounds.Y);

        using var fonte = new Font("Segoe UI", 16f);
        const string dica = "Clique onde quer escrever  •  ou arraste para definir a largura  •  Esc cancela";
        var tamanho = g.MeasureString(dica, fonte);
        g.DrawString(dica, fonte, Brushes.White, tela.Left + (tela.Width - tamanho.Width) / 2, tela.Top + tela.Height * 0.08f);

        if (inicio != null && area.Width > 0 && area.Height > 0)
        {
            using var borda = new Pen(Color.White, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            g.DrawRectangle(borda, area);
        }
    }
}
