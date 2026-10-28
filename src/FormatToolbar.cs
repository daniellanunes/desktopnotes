using System.Globalization;

namespace NotasDesktop;

/// <summary>Barra de formatação que flutua acima da anotação enquanto ela é editada.</summary>
sealed class FormatToolbar : Form
{
    static readonly (string Nome, Color Cor)[] Paleta =
    {
        ("Branco", Color.White),
        ("Preto", Color.FromArgb(17, 17, 17)),
        ("Cinza", Color.FromArgb(160, 160, 160)),
        ("Vermelho", Color.FromArgb(255, 90, 90)),
        ("Laranja", Color.FromArgb(255, 159, 67)),
        ("Amarelo", Color.FromArgb(255, 225, 77)),
        ("Verde", Color.FromArgb(107, 227, 107)),
        ("Azul", Color.FromArgb(90, 180, 255)),
        ("Roxo", Color.FromArgb(180, 130, 255)),
        ("Rosa", Color.FromArgb(255, 138, 216)),
    };

    static readonly (string Nome, Color Cor)[] Realces =
    {
        ("Amarelo", Color.FromArgb(255, 235, 59)),
        ("Verde", Color.FromArgb(118, 255, 3)),
        ("Azul", Color.FromArgb(64, 196, 255)),
        ("Rosa", Color.FromArgb(255, 64, 129)),
        ("Laranja", Color.FromArgb(255, 152, 0)),
        ("Preto", Color.Black),
        ("Branco", Color.White),
    };

    readonly NoteForm nota;
    readonly ToolStrip faixa;
    readonly ToolStripComboBox cbFonte, cbTamanho;
    readonly ToolStripButton btnNegrito, btnItalico, btnSublinhado, btnTachado, btnEsquerda, btnCentro, btnDireita, btnLista, btnTarefas;
    readonly ToolStripSplitButton btnCor, btnRealce;
    readonly Font icones = new("Segoe MDL2 Assets", 10f);
    Color ultimaCor = Color.FromArgb(255, 90, 90);
    Color ultimoRealce = Color.FromArgb(255, 235, 59);
    bool atualizando;

    public FormatToolbar(NoteForm nota)
    {
        this.nota = nota;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(170, 170, 170);
        Padding = new Padding(1);
        Font = new Font("Segoe UI", 9f);

        faixa = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            Dock = DockStyle.Fill,
            RenderMode = ToolStripRenderMode.System,
            BackColor = Color.White,
            CanOverflow = false,
            Padding = new Padding(4, 2, 4, 2),
            ImageScalingSize = new Size(LogicalToDeviceUnits(16), LogicalToDeviceUnits(16)),
        };

        cbFonte = new ToolStripComboBox
        {
            AutoSize = false,
            Width = LogicalToDeviceUnits(150),
            DropDownStyle = ComboBoxStyle.DropDown,
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems,
            ToolTipText = "Fonte",
            FlatStyle = FlatStyle.System,
        };
        cbFonte.Items.AddRange(FontFamily.Families.Select(f => (object)f.Name).ToArray());
        cbFonte.SelectedIndexChanged += (_, _) => { if (!atualizando) AplicarFonte(); };
        cbFonte.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AplicarFonte(); } };

        cbTamanho = new ToolStripComboBox
        {
            AutoSize = false,
            Width = LogicalToDeviceUnits(52),
            DropDownStyle = ComboBoxStyle.DropDown,
            ToolTipText = "Tamanho da letra",
            FlatStyle = FlatStyle.System,
        };
        cbTamanho.Items.AddRange(new object[] { "8", "9", "10", "11", "12", "14", "16", "18", "20", "24", "28", "32", "36", "48", "60", "72", "96" });
        cbTamanho.SelectedIndexChanged += (_, _) => { if (!atualizando) AplicarTamanho(); };
        cbTamanho.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AplicarTamanho(); } };

        btnNegrito = Botao("N", "Negrito (Ctrl+B)", () => nota.AlternarEfeito(Native.CFM_BOLD), new Font("Segoe UI", 9.5f, FontStyle.Bold));
        btnItalico = Botao("I", "Itálico (Ctrl+I)", () => nota.AlternarEfeito(Native.CFM_ITALIC), new Font("Georgia", 9.5f, FontStyle.Italic));
        btnSublinhado = Botao("S", "Sublinhado (Ctrl+U)", () => nota.AlternarEfeito(Native.CFM_UNDERLINE), new Font("Segoe UI", 9.5f, FontStyle.Underline));
        btnTachado = Botao("ab", "Tachado", () => nota.AlternarEfeito(Native.CFM_STRIKEOUT), new Font("Segoe UI", 9.5f, FontStyle.Strikeout));

        btnCor = new ToolStripSplitButton { DisplayStyle = ToolStripItemDisplayStyle.Image, ToolTipText = "Cor do texto" };
        btnCor.ButtonClick += (_, _) => { nota.DefinirCor(ultimaCor); nota.VoltarFoco(); };
        foreach (var (nome, cor) in Paleta)
            btnCor.DropDownItems.Add(ItemCor(nome, cor, () => EscolherCor(cor)));
        btnCor.DropDownItems.Add(new ToolStripSeparator());
        btnCor.DropDownItems.Add("Mais cores...", null, (_, _) =>
        {
            using var dialogo = new ColorDialog { Color = ultimaCor, FullOpen = true };
            if (nota.Dialogo(() => dialogo.ShowDialog(this)) == DialogResult.OK) EscolherCor(dialogo.Color);
            else nota.VoltarFoco();
        });

        btnRealce = new ToolStripSplitButton { DisplayStyle = ToolStripItemDisplayStyle.Image, ToolTipText = "Cor de realce (marca-texto)" };
        btnRealce.ButtonClick += (_, _) => { nota.DefinirRealce(ultimoRealce); nota.VoltarFoco(); };
        btnRealce.DropDownItems.Add("Sem realce", null, (_, _) => { nota.DefinirRealce(null); nota.VoltarFoco(); });
        btnRealce.DropDownItems.Add(new ToolStripSeparator());
        foreach (var (nome, cor) in Realces)
            btnRealce.DropDownItems.Add(ItemCor(nome, cor, () => EscolherRealce(cor)));

        btnEsquerda = Botao("", "Alinhar à esquerda (Ctrl+L)", () => nota.Alinhar(HorizontalAlignment.Left), icones);
        btnCentro = Botao("", "Centralizar (Ctrl+E)", () => nota.Alinhar(HorizontalAlignment.Center), icones);
        btnDireita = Botao("", "Alinhar à direita (Ctrl+R)", () => nota.Alinhar(HorizontalAlignment.Right), icones);
        btnLista = Botao("", "Lista com marcadores (Ctrl+Shift+L)", nota.AlternarLista, icones);
        btnTarefas = Botao("○ ✓", "Lista de tarefas: clique na bolinha para riscar (Ctrl+Shift+T)", nota.AlternarTarefas,
            new Font("Segoe UI Symbol", 9.5f));

        faixa.Items.AddRange(new ToolStripItem[]
        {
            cbFonte, cbTamanho,
            Botao("", "Aumentar letra (Ctrl+Shift+>)", () => nota.MudarTamanho(+2), icones),
            Botao("", "Diminuir letra (Ctrl+Shift+<)", () => nota.MudarTamanho(-2), icones),
            new ToolStripSeparator(),
            btnNegrito, btnItalico, btnSublinhado, btnTachado,
            new ToolStripSeparator(),
            btnCor, btnRealce,
            new ToolStripSeparator(),
            btnEsquerda, btnCentro, btnDireita, btnLista, btnTarefas,
            new ToolStripSeparator(),
            Botao("Limpar", "Limpar formatação (Ctrl+Espaço)", nota.LimparFormatacao),
        });
        Controls.Add(faixa);
        AtualizarImagensDeCor();

        var tamanho = faixa.PreferredSize;
        ClientSize = new Size(tamanho.Width + Padding.Horizontal, tamanho.Height + Padding.Vertical);

        Deactivate += (_, _) => nota.VerificarFocoDepois();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    /// <summary>Marca os botões conforme a formatação do texto selecionado.</summary>
    public void Atualizar()
    {
        atualizando = true;
        try
        {
            var cf = nota.FormatoSelecao();
            bool Ativo(uint efeito) => (cf.dwMask & efeito) != 0 && (cf.dwEffects & efeito) != 0;
            btnNegrito.Checked = Ativo(Native.CFM_BOLD);
            btnItalico.Checked = Ativo(Native.CFM_ITALIC);
            btnSublinhado.Checked = Ativo(Native.CFM_UNDERLINE);
            btnTachado.Checked = Ativo(Native.CFM_STRIKEOUT);
            if (!cbFonte.Focused) cbFonte.Text = (cf.dwMask & Native.CFM_FACE) != 0 ? cf.szFaceName : "";
            if (!cbTamanho.Focused) cbTamanho.Text = (cf.dwMask & Native.CFM_SIZE) != 0 ? (cf.yHeight / 20f).ToString("0.#") : "";

            var alinhamento = nota.AlinhamentoSelecao;
            btnEsquerda.Checked = alinhamento == HorizontalAlignment.Left;
            btnCentro.Checked = alinhamento == HorizontalAlignment.Center;
            btnDireita.Checked = alinhamento == HorizontalAlignment.Right;
            btnLista.Checked = nota.ListaSelecao;
            btnTarefas.Checked = nota.TarefaSelecao;
        }
        finally
        {
            atualizando = false;
        }
    }

    void AplicarFonte()
    {
        var nome = cbFonte.Text.Trim();
        if (FontFamily.Families.Any(f => f.Name.Equals(nome, StringComparison.OrdinalIgnoreCase)))
            nota.DefinirFonte(nome);
        nota.VoltarFoco();
    }

    void AplicarTamanho()
    {
        var texto = cbTamanho.Text.Trim().Replace(',', '.');
        if (float.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out var tamanho) && tamanho is >= 4 and <= 400)
            nota.DefinirTamanho(tamanho);
        nota.VoltarFoco();
    }

    void EscolherCor(Color cor)
    {
        ultimaCor = cor;
        AtualizarImagensDeCor();
        nota.DefinirCor(cor);
        nota.VoltarFoco();
    }

    void EscolherRealce(Color cor)
    {
        ultimoRealce = cor;
        AtualizarImagensDeCor();
        nota.DefinirRealce(cor);
        nota.VoltarFoco();
    }

    void AtualizarImagensDeCor()
    {
        btnCor.Image = ImagemComFaixa("A", new Font("Segoe UI", 9.5f, FontStyle.Bold), ultimaCor);
        btnRealce.Image = ImagemComFaixa("", icones, ultimoRealce);
    }

    /// <summary>Ícone com um símbolo e a faixa de cor embaixo, como no Word.</summary>
    Bitmap ImagemComFaixa(string simbolo, Font fonte, Color cor)
    {
        int lado = LogicalToDeviceUnits(16);
        var bmp = new Bitmap(lado, lado);
        using var g = Graphics.FromImage(bmp);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        int faixaAltura = Math.Max(3, lado / 5);
        TextRenderer.DrawText(g, simbolo, fonte, new Rectangle(0, -1, lado, lado - faixaAltura + 1), Color.FromArgb(40, 40, 40),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        using var pincel = new SolidBrush(cor);
        g.FillRectangle(pincel, 0, lado - faixaAltura, lado, faixaAltura);
        g.DrawRectangle(Pens.Gray, 0, lado - faixaAltura, lado - 1, faixaAltura - 1);
        return bmp;
    }

    ToolStripMenuItem ItemCor(string nome, Color cor, Action acao)
    {
        int lado = LogicalToDeviceUnits(14);
        var amostra = new Bitmap(lado, lado);
        using (var g = Graphics.FromImage(amostra))
        {
            using var pincel = new SolidBrush(cor);
            g.FillRectangle(pincel, 0, 0, lado, lado);
            g.DrawRectangle(Pens.Gray, 0, 0, lado - 1, lado - 1);
        }
        return new ToolStripMenuItem(nome, amostra, (_, _) => acao());
    }

    ToolStripButton Botao(string texto, string dica, Action acao, Font? fonte = null)
    {
        var botao = new ToolStripButton(texto)
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ToolTipText = dica,
            Margin = new Padding(1, 1, 1, 2),
        };
        if (fonte != null) botao.Font = fonte;
        botao.Click += (_, _) => { acao(); nota.VoltarFoco(); };
        return botao;
    }
}
