using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace NotasDesktop;

/// <summary>
/// Uma anotação escrita direto na área de trabalho, com formatação (negrito, cores, tamanhos...).
/// Fora da edição o texto é desenhado pela <see cref="DesktopLayer"/> sobre o papel de parede;
/// ao clicar nele, este editor transparente (só o texto aparece) fica por cima para digitar,
/// com a <see cref="FormatToolbar"/> flutuando acima.
/// </summary>
sealed class NoteForm : Form
{
    static readonly (string Nome, string Cor)[] CoresTexto =
    {
        ("Branco", "#FFFFFF"),
        ("Preto", "#111111"),
        ("Amarelo", "#FFE14D"),
        ("Laranja", "#FF9F43"),
        ("Vermelho", "#FF5A5A"),
        ("Verde", "#6BE36B"),
        ("Azul", "#5AB4FF"),
        ("Rosa", "#FF8AD8"),
    };

    // Compartilhados entre as anotações, como o "Localizar próxima" do Bloco de Notas.
    static string ultimoTermo = "";
    static bool ultimoCaso;

    public NoteData Dados { get; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool PermitirFechar { get; set; }

    public int AlturaBarra { get; }
    public int Margem { get; }
    public bool TextoClaro { get; private set; }
    public bool Editando { get; private set; }

    public event Action? Alterada;
    public event Action? ExcluirSolicitado;
    public event Action? NovaNotaSolicitada;

    readonly DesktopLayer camada;
    readonly ToolTip dicas = new();
    readonly Panel barra;
    readonly Label btnMenu, btnMover, btnFixar, btnNova, btnFechar;
    static readonly Color CorAlfinete = Color.FromArgb(255, 159, 67);
    readonly RichTextBox texto;
    readonly ContextMenuStrip menu;
    ToolStripMenuItem itemQuebra = null!, itemTravar = null!, itemSombra = null!, itemFixar = null!;
    FindReplaceForm? busca;
    FormatToolbar? barraFormatacao;
    bool dialogoAberto;
    bool? temaEscuro;
    int ancora;

    public NoteForm(NoteData dados, Icon icone)
    {
        Dados = dados;
        Text = "Anotação";
        Icon = icone;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AlturaBarra = LogicalToDeviceUnits(26);
        Margem = LogicalToDeviceUnits(8);
        Padding = new Padding(Margem, 0, Margem, Margem);

        texto = new RichTextBox
        {
            AcceptsTab = true,
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            HideSelection = false,
            DetectUrls = true,
        };
        texto.LinkClicked += (_, e) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.LinkText!) { UseShellExecute = true }); }
            catch { }
        };
        texto.HandleCreated += (_, _) => { temaEscuro = null; AplicarTemaRolagem(); };

        btnMenu = CriarBotao("☰", "Menu");
        btnMover = CriarBotao("✥", "Segure e arraste para mover a anotação");
        btnMover.Cursor = Cursors.SizeAll;
        btnFixar = CriarBotao("", "Alfinete: fixar por cima de todas as janelas");
        btnFixar.Font = new Font("Segoe MDL2 Assets", 10f);
        btnNova = CriarBotao("+", "Nova anotação (Ctrl+N)");
        btnFechar = CriarBotao("✕", "Excluir anotação");
        btnMenu.Dock = btnMover.Dock = DockStyle.Left;
        btnFixar.Dock = btnNova.Dock = btnFechar.Dock = DockStyle.Right;

        // O fundo da barra é transparente: arrastar por ele (na camada de baixo) também move a anotação.
        // Ordem de inclusão: o último de cada lado fica na ponta (☰ ✥ ... 📌 + ✕).
        barra = new Panel { Dock = DockStyle.Top, Height = AlturaBarra };
        barra.Controls.Add(btnMover);
        barra.Controls.Add(btnMenu);
        barra.Controls.Add(btnFixar);
        barra.Controls.Add(btnNova);
        barra.Controls.Add(btnFechar);

        Controls.Add(texto);
        Controls.Add(barra);

        menu = CriarMenu();
        texto.ContextMenuStrip = menu;
        barra.ContextMenuStrip = menu;

        btnMenu.Click += (_, _) => menu.Show(btnMenu, new Point(0, btnMenu.Height));
        btnNova.Click += (_, _) => NovaNotaSolicitada?.Invoke();
        btnFixar.Click += (_, _) => { AlternarFixar(); VoltarFoco(); };
        btnMover.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) camada?.Mover(); };
        btnFechar.Click += (_, _) => ConfirmarExcluir();
        texto.TextChanged += (_, _) => Alterada?.Invoke();
        texto.SelectionChanged += (_, _) => { if (barraFormatacao?.Visible == true) barraFormatacao.Atualizar(); };
        texto.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && texto.SelectionLength == 0)
                TentarMarcarTarefa(texto.PointToScreen(e.Location));
        };

        camada = new DesktopLayer(this)
        {
            Bounds = PosicaoValida(new Rectangle(dados.X, dados.Y, dados.Largura, dados.Altura)),
            MinimumSize = new Size(LogicalToDeviceUnits(100), LogicalToDeviceUnits(60)),
        };
        camada.LocationChanged += (_, _) => { Location = camada.Location; PosicionarBarraFormatacao(); };
        camada.SizeChanged += (_, _) => { Size = camada.Size; PosicionarBarraFormatacao(); };
        camada.ResizeEnd += (_, _) =>
        {
            Alterada?.Invoke();
            VoltarFoco(); // depois de mover/redimensionar, continua digitando
        };
        camada.Deactivate += (_, _) => { if (Editando) BeginInvoke(VerificarFoco); };
        Bounds = camada.Bounds;

        // Formatação padrão (vale para anotações novas ou antigas, sem formatação salva).
        var estilo = (dados.Negrito ? FontStyle.Bold : 0) | (dados.Italico ? FontStyle.Italic : 0);
        texto.Font = new Font(dados.Fonte, dados.TamanhoFonte, estilo);
        texto.ForeColor = Cores.De(dados.CorTexto, Color.White);
        CarregarConteudo();
        AplicarAparencia();
        texto.ClearUndo();
    }

    void CarregarConteudo()
    {
        if (!string.IsNullOrEmpty(Dados.Rtf))
        {
            try { texto.Rtf = Dados.Rtf; return; }
            catch { /* RTF inválido: cai para o texto puro */ }
        }
        texto.Text = Dados.Texto.ReplaceLineEndings("\n");
    }

    // ---------- modos: exibição (texto no papel de parede) e edição ----------

    public void Mostrar()
    {
        camada.Show();
        camada.TrazerParaFrente();
    }

    // ---------- alfinete ----------

    public void AlternarFixar()
    {
        Dados.Fixada = !Dados.Fixada;
        AplicarFixacao();
        camada.Redesenhar();
        Alterada?.Invoke();
    }

    /// <summary>Com o alfinete, a camada, o editor e a barra ficam "sempre visíveis" (acima de janelas maximizadas).</summary>
    void AplicarFixacao()
    {
        camada.AplicarSempreVisivel();
        TopMost = Dados.Fixada;
        if (barraFormatacao != null) barraFormatacao.TopMost = Dados.Fixada;
        btnFixar.ForeColor = Dados.Fixada ? CorAlfinete : btnMenu.ForeColor;
        dicas.SetToolTip(btnFixar, Dados.Fixada ? "Tirar o alfinete" : "Alfinete: fixar por cima de todas as janelas");
        if (itemFixar != null) itemFixar.Checked = Dados.Fixada;
    }

    /// <summary>Onde o alfinete aparece (canto superior direito) quando a anotação está fixada.</summary>
    public Rectangle AreaAlfinete(Size tamanho)
    {
        int lado = LogicalToDeviceUnits(20);
        return new Rectangle(tamanho.Width - Margem - lado, LogicalToDeviceUnits(3), lado, lado);
    }

    public void IniciarEdicao(Point? pontoNaTela)
    {
        Editando = true;
        camada.Redesenhar();
        Bounds = camada.Bounds;
        Show();

        barraFormatacao ??= new FormatToolbar(this);
        barraFormatacao.TopMost = Dados.Fixada;
        if (!barraFormatacao.Visible) barraFormatacao.Show(this);
        PosicionarBarraFormatacao();

        Activate();
        texto.Focus();
        if (pontoNaTela is { } p) PosicionarCursor(p, false);
        else texto.Select(texto.TextLength, 0);
        barraFormatacao.Atualizar();
    }

    public void EncerrarEdicao()
    {
        if (!Editando) return;
        Editando = false;
        busca?.Close();
        barraFormatacao?.Hide();
        if (texto.Text.Trim().Length == 0)
        {
            ExcluirSolicitado?.Invoke(); // anotação vazia some sozinha
            return;
        }
        Hide();
        camada.Redesenhar();
        Alterada?.Invoke();
    }

    /// <summary>Coloca o cursor no ponto clicado (cliques no "vazio" chegam pela camada).</summary>
    public void PosicionarCursor(Point pontoNaTela, bool estender)
    {
        if (!Editando) return;
        if (!ContainsFocus) { Activate(); texto.Focus(); }

        var p = PontoNoTexto(pontoNaTela);
        int indice = texto.TextLength == 0 ? 0 : texto.GetCharIndexFromPosition(p);
        if (texto.TextLength > 0)
        {
            var ultimo = texto.GetPositionFromCharIndex(texto.TextLength - 1);
            if (p.Y > ultimo.Y + texto.Font.Height) indice = texto.TextLength; // abaixo da última linha
        }

        if (!estender) ancora = indice;
        texto.Select(Math.Min(ancora, indice), Math.Abs(indice - ancora));
    }

    /// <summary>
    /// Converte um ponto da tela para o editor usando a posição da camada: com o editor escondido,
    /// a posição que o Windows guarda para ele não é confiável.
    /// </summary>
    Point PontoNoTexto(Point pontoNaTela) =>
        new(pontoNaTela.X - camada.Left - texto.Left, pontoNaTela.Y - camada.Top - texto.Top);

    public void MostrarMenu(Point pontoNaTela) => menu.Show(pontoNaTela);

    public void VoltarFoco()
    {
        if (!Editando) return;
        Activate();
        texto.Focus();
    }

    void PosicionarBarraFormatacao()
    {
        if (barraFormatacao is not { Visible: true } b) return;
        var tela = Screen.FromRectangle(camada.Bounds).WorkingArea;
        int espaco = LogicalToDeviceUnits(4);
        int y = camada.Top - b.Height - espaco;
        if (y < tela.Top) y = camada.Bottom + espaco; // sem espaço acima: vai para baixo
        int x = Math.Clamp(camada.Left, tela.Left, Math.Max(tela.Left, tela.Right - b.Width));
        b.Location = new Point(x, y);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Dono = camada: o editor fica sempre logo acima dela e junto no Win+D.
        Native.SetWindowLongPtr(Handle, Native.GWLP_HWNDPARENT, camada.Handle);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_TOOLWINDOW; // fora do Alt+Tab
            return cp;
        }
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        BeginInvoke(VerificarFoco);
    }

    public void VerificarFocoDepois() => BeginInvoke(VerificarFoco);

    void VerificarFoco()
    {
        if (dialogoAberto || !Editando) return;
        var ativa = ActiveForm;
        // A camada fica ativa enquanto a anotação é arrastada: isso faz parte da edição.
        if (ativa != null && (ativa == this || ativa == busca || ativa == barraFormatacao || ativa == camada)) return;
        EncerrarEdicao();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Alt+F4 só termina a edição; para apagar existe "Excluir anotação".
        if (e.CloseReason == CloseReason.UserClosing && !PermitirFechar)
        {
            e.Cancel = true;
            EncerrarEdicao();
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        camada.Close();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Escape: EncerrarEdicao(); return true;
            case Keys.Enter: if (ContinuarTarefa()) return true; break;
            case Keys.Control | Keys.Shift | Keys.T: AlternarTarefas(); return true;
            case Keys.Control | Keys.A: texto.SelectAll(); return true;
            case Keys.Control | Keys.V:
            case Keys.Shift | Keys.Insert: ColarTexto(); return true;
            case Keys.Control | Keys.F: AbrirBusca(false); return true;
            case Keys.Control | Keys.H: AbrirBusca(true); return true;
            case Keys.F3: LocalizarProximaAtalho(); return true;
            case Keys.F5: InserirDataHora(); return true;
            case Keys.Control | Keys.S: SalvarTxt(false); return true;
            case Keys.Control | Keys.Shift | Keys.S: SalvarTxt(true); return true;
            case Keys.Control | Keys.O: AbrirTxt(); return true;
            case Keys.Control | Keys.N: NovaNotaSolicitada?.Invoke(); return true;

            // formatação
            case Keys.Control | Keys.B: AlternarEfeito(Native.CFM_BOLD); return true;
            case Keys.Control | Keys.I: AlternarEfeito(Native.CFM_ITALIC); return true;
            case Keys.Control | Keys.U: AlternarEfeito(Native.CFM_UNDERLINE); return true;
            case Keys.Control | Keys.L: Alinhar(HorizontalAlignment.Left); return true;
            case Keys.Control | Keys.E: Alinhar(HorizontalAlignment.Center); return true;
            case Keys.Control | Keys.R: Alinhar(HorizontalAlignment.Right); return true;
            case Keys.Control | Keys.Shift | Keys.L: AlternarLista(); return true;
            case Keys.Control | Keys.Space: LimparFormatacao(); return true;
            case Keys.Control | Keys.Shift | Keys.OemPeriod:
            case Keys.Control | Keys.Oemplus:
            case Keys.Control | Keys.Add: MudarTamanho(+2); return true;
            case Keys.Control | Keys.Shift | Keys.Oemcomma:
            case Keys.Control | Keys.OemMinus:
            case Keys.Control | Keys.Subtract: MudarTamanho(-2); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ---------- API usada pelo app ----------

    public void TrazerParaFrente() => camada.TrazerParaFrente();

    public void AtualizarDados()
    {
        Dados.X = camada.Left;
        Dados.Y = camada.Top;
        Dados.Largura = camada.Width;
        Dados.Altura = camada.Height;
        Dados.Texto = TextoCrlf;
        Dados.Rtf = texto.Rtf;
    }

    /// <summary>O RichTextBox usa só '\n'; arquivos e o JSON ficam com quebras do Windows.</summary>
    string TextoCrlf => texto.Text.ReplaceLineEndings("\r\n");

    // ---------- formatação (usada pela barra, atalhos e menu) ----------

    /// <summary>
    /// Aplica uma formatação: na edição vale para a seleção (ou para o que for digitado a seguir);
    /// fora da edição, ou com <paramref name="tudoSemSelecao"/> e nada selecionado, vale para o texto todo.
    /// </summary>
    void Formatar(Action aplicar, bool tudoSemSelecao = false)
    {
        if (Editando && !(tudoSemSelecao && texto.SelectionLength == 0))
        {
            aplicar();
            barraFormatacao?.Atualizar();
        }
        else
        {
            int inicio = texto.SelectionStart, tamanho = texto.SelectionLength;
            texto.SelectAll();
            aplicar();
            texto.Select(inicio, tamanho);
            if (!Editando) camada.Redesenhar();
            barraFormatacao?.Atualizar();
        }
        Alterada?.Invoke();
    }

    public Native.CHARFORMAT2 FormatoSelecao()
    {
        var cf = Native.CHARFORMAT2.Novo();
        Native.SendMessage(texto.Handle, Native.EM_GETCHARFORMAT, Native.SCF_SELECTION, ref cf);
        return cf;
    }

    public HorizontalAlignment AlinhamentoSelecao => texto.SelectionAlignment;
    public bool ListaSelecao => texto.SelectionBullet;

    /// <summary>Muda só um atributo (ex.: negrito), mantendo os outros mesmo numa seleção com formatos misturados.</summary>
    void DefinirFormato(Native.CHARFORMAT2 cf) =>
        Native.SendMessage(texto.Handle, Native.EM_SETCHARFORMAT, Native.SCF_SELECTION, ref cf);

    public void AlternarEfeito(uint efeito) => Formatar(() =>
    {
        var atual = FormatoSelecao();
        bool ativo = (atual.dwMask & efeito) != 0 && (atual.dwEffects & efeito) != 0;
        var cf = Native.CHARFORMAT2.Novo();
        cf.dwMask = efeito;
        cf.dwEffects = ativo ? 0 : efeito;
        DefinirFormato(cf);
    });

    public void DefinirFonte(string nome) => Formatar(() =>
    {
        var cf = Native.CHARFORMAT2.Novo();
        cf.dwMask = Native.CFM_FACE;
        cf.szFaceName = nome;
        DefinirFormato(cf);
    });

    public void DefinirTamanho(float pontos) => Formatar(() =>
    {
        var cf = Native.CHARFORMAT2.Novo();
        cf.dwMask = Native.CFM_SIZE;
        cf.yHeight = (int)Math.Round(pontos * 20);
        DefinirFormato(cf);
    });

    /// <summary>Aumenta/diminui cada trecho a partir do tamanho que ele já tem.</summary>
    public void MudarTamanho(int pontos) => Formatar(
        () => Native.SendMessage(texto.Handle, Native.EM_SETFONTSIZE, pontos, IntPtr.Zero),
        tudoSemSelecao: true);

    public void DefinirCor(Color cor) => Formatar(() =>
    {
        var cf = Native.CHARFORMAT2.Novo();
        cf.dwMask = Native.CFM_COLOR;
        cf.crTextColor = ColorTranslator.ToWin32(cor);
        DefinirFormato(cf);
    });

    public void DefinirRealce(Color? cor) => Formatar(() =>
    {
        var cf = Native.CHARFORMAT2.Novo();
        cf.dwMask = Native.CFM_BACKCOLOR;
        if (cor is { } c) cf.crBackColor = ColorTranslator.ToWin32(c);
        else cf.dwEffects = Native.CFE_AUTOBACKCOLOR;
        DefinirFormato(cf);
    });

    public void Alinhar(HorizontalAlignment alinhamento) => Formatar(() => texto.SelectionAlignment = alinhamento);

    public void AlternarLista() => Formatar(() => texto.SelectionBullet = !texto.SelectionBullet);

    // ---------- lista de tarefas: "○ " no início do parágrafo; clicar marca "●" e risca a frase ----------

    const char TarefaAberta = '○';
    const char TarefaFeita = '●';

    static bool EhMarcador(char c) => c is TarefaAberta or TarefaFeita;

    /// <summary>Início e fim (exclusivo, antes do '\n') do parágrafo que contém <paramref name="indice"/>.</summary>
    (int Inicio, int Fim) Paragrafo(int indice)
    {
        var t = texto.Text;
        indice = Math.Clamp(indice, 0, t.Length);
        int inicio = indice == 0 ? 0 : t.LastIndexOf('\n', indice - 1) + 1;
        int fim = t.IndexOf('\n', inicio);
        return (inicio, fim < 0 ? t.Length : fim);
    }

    bool TemMarcador(int inicioParagrafo)
    {
        var t = texto.Text;
        return t.Length > inicioParagrafo + 1 && EhMarcador(t[inicioParagrafo]) && t[inicioParagrafo + 1] == ' ';
    }

    public bool TarefaSelecao => TemMarcador(Paragrafo(texto.SelectionStart).Inicio);

    void Riscar(bool riscar)
    {
        var cf = Native.CHARFORMAT2.Novo();
        cf.dwMask = Native.CFM_STRIKEOUT;
        cf.dwEffects = riscar ? Native.CFM_STRIKEOUT : 0;
        DefinirFormato(cf);
    }

    void RiscarParagrafo(int inicioParagrafo, bool riscar)
    {
        var (_, fim) = Paragrafo(inicioParagrafo);
        int de = inicioParagrafo + 2;
        if (fim <= de) return;
        texto.Select(de, fim - de);
        Riscar(riscar);
    }

    /// <summary>Põe (ou tira, se todas já têm) a bolinha de tarefa nas linhas selecionadas.</summary>
    public void AlternarTarefas()
    {
        int selInicio = texto.SelectionStart, selFim = selInicio + texto.SelectionLength;
        var inicios = new List<int>();
        var (p, f) = Paragrafo(selInicio);
        inicios.Add(p);
        while (f < selFim && f < texto.TextLength)
        {
            (p, f) = Paragrafo(f + 1);
            inicios.Add(p);
        }

        bool tirar = inicios.All(TemMarcador);
        for (int k = inicios.Count - 1; k >= 0; k--) // de trás para frente: os índices anteriores não mudam
        {
            int i = inicios[k];
            if (tirar)
            {
                RiscarParagrafo(i, false);
                texto.Select(i, 2);
                texto.SelectedText = "";
            }
            else if (!TemMarcador(i))
            {
                texto.Select(i, 0);
                texto.SelectedText = TarefaAberta + " ";
                texto.Select(i, 2);
                Riscar(false);
            }
        }
        texto.Select(Paragrafo(inicios[0]).Fim, 0);
        barraFormatacao?.Atualizar();
        Alterada?.Invoke();
    }

    void AlternarTarefaEm(int inicioParagrafo)
    {
        int selInicio = texto.SelectionStart, selTamanho = texto.SelectionLength;
        bool feita = texto.Text[inicioParagrafo] == TarefaFeita;
        texto.Select(inicioParagrafo, 1);
        texto.SelectedText = (feita ? TarefaAberta : TarefaFeita).ToString();
        RiscarParagrafo(inicioParagrafo, !feita);
        texto.Select(selInicio, selTamanho);
        if (!Editando) camada.Redesenhar();
        Alterada?.Invoke();
    }

    /// <summary>Se o ponto está sobre uma bolinha de tarefa, marca/desmarca e devolve true.</summary>
    public bool TentarMarcarTarefa(Point pontoNaTela)
    {
        if (texto.TextLength == 0) return false;
        var p = PontoNoTexto(pontoNaTela);
        int indice = texto.GetCharIndexFromPosition(p);
        foreach (var c in new[] { indice, indice - 1 })
        {
            if (c < 0 || c + 1 >= texto.TextLength || !EhMarcador(texto.Text[c]) || Paragrafo(c).Inicio != c) continue;

            var pos = texto.GetPositionFromCharIndex(c);
            var proximo = texto.GetPositionFromCharIndex(c + 1);
            int linha = texto.GetLineFromCharIndex(c);
            int inicioLinhaSeguinte = texto.GetFirstCharIndexFromLine(linha + 1);
            int altura = inicioLinhaSeguinte > c
                ? texto.GetPositionFromCharIndex(inicioLinhaSeguinte).Y - pos.Y
                : LogicalToDeviceUnits(30);
            int folga = LogicalToDeviceUnits(4);

            if (p.X >= pos.X - folga && p.X <= Math.Max(proximo.X, pos.X + folga * 2) + folga &&
                p.Y >= pos.Y - folga && p.Y <= pos.Y + altura)
            {
                AlternarTarefaEm(c);
                return true;
            }
        }
        return false;
    }

    /// <summary>Enter numa tarefa cria a próxima; Enter numa tarefa vazia encerra a lista.</summary>
    bool ContinuarTarefa()
    {
        if (texto.SelectionLength > 0) return false;
        int cursor = texto.SelectionStart;
        var (inicio, fim) = Paragrafo(cursor);
        if (!TemMarcador(inicio) || cursor < inicio + 2) return false;

        if (fim - inicio <= 2 || texto.Text[(inicio + 2)..fim].Trim().Length == 0)
        {
            texto.Select(inicio, fim - inicio);
            texto.SelectedText = "";
            return true;
        }
        texto.SelectedText = "\n" + TarefaAberta + " ";
        int depois = texto.SelectionStart;
        texto.Select(depois - 2, 2);
        Riscar(false);
        texto.Select(depois, 0);
        Riscar(false); // o que for digitado na tarefa nova não sai riscado
        return true;
    }

    public void LimparFormatacao() => Formatar(() =>
    {
        var cf = Native.CHARFORMAT2.Novo();
        cf.dwMask = Native.CFM_BOLD | Native.CFM_ITALIC | Native.CFM_UNDERLINE | Native.CFM_STRIKEOUT |
                    Native.CFM_COLOR | Native.CFM_SIZE | Native.CFM_FACE | Native.CFM_BACKCOLOR;
        cf.dwEffects = Native.CFE_AUTOBACKCOLOR;
        cf.yHeight = (int)Math.Round(Dados.TamanhoFonte * 20);
        cf.crTextColor = ColorTranslator.ToWin32(Cores.De(Dados.CorTexto, Color.White));
        cf.szFaceName = Dados.Fonte;
        DefinirFormato(cf);
        texto.SelectionBullet = false;
        texto.SelectionAlignment = HorizontalAlignment.Left;
    });

    // ---------- desenho do texto sobre o papel de parede ----------

    /// <summary>
    /// Desenha a anotação com fundo transparente usando o próprio motor do editor (EM_FORMATRANGE),
    /// então negrito, cores, tamanhos e realces saem iguais. O texto é desenhado duas vezes, em fundo
    /// preto e em fundo branco: a diferença entre as duas dá a transparência de cada pixel.
    /// Um contorno suave garante leitura em qualquer papel de parede, e em volta das letras fica uma
    /// faixa quase invisível (alfa mínimo) só para o clique "pegar" perto do texto.
    /// </summary>
    public Bitmap DesenharTexto(Size tamanho)
    {
        int w = tamanho.Width, h = tamanho.Height;
        var area = AreaDoTexto(tamanho);
        // O editor pinta o próprio fundo ao renderizar, então o fundo dele acompanha cada passada.
        var chave = texto.BackColor;
        byte[] emPreto, emBranco;
        try
        {
            texto.BackColor = Color.Black;
            emPreto = Renderizar(w, h, area, Color.Black);
            texto.BackColor = Color.White;
            emBranco = Renderizar(w, h, area, Color.White);
        }
        finally
        {
            texto.BackColor = chave;
        }

        var cobertura = new byte[w * h];
        for (int i = 0; i < cobertura.Length; i++)
        {
            int o = i * 4;
            int diferenca = (emBranco[o] - emPreto[o]) + (emBranco[o + 1] - emPreto[o + 1]) + (emBranco[o + 2] - emPreto[o + 2]);
            cobertura[i] = (byte)Math.Clamp(255 - diferenca / 3, 0, 255);
        }

        var contorno = Dados.Sombra ? Dilatar(cobertura, w, h, 1) : null;
        var faixaClique = Dilatar(cobertura, w, h, LogicalToDeviceUnits(5));
        var corContorno = TextoClaro ? Color.Black : Color.White;
        byte alfaClique = (byte)Math.Clamp(Math.Ceiling(1.0 / Math.Max(Dados.Opacidade, 0.05)), 1, 255);

        var pixels = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x, o = i * 4;
                int cob = cobertura[i];
                float aTexto = cob / 255f;
                float aContorno = contorno != null && x > 0 && y > 0 ? contorno[i - w - 1] * 0.8f / 255f : 0f;
                float a = aTexto + aContorno * (1 - aTexto);
                if (a <= 0f)
                {
                    if (faixaClique[i] > 0) pixels[o + 3] = alfaClique;
                    continue;
                }
                float pc = aContorno * (1 - aTexto);
                for (int c = 0; c < 3; c++)
                {
                    // cor real do pixel = (cor sobre preto) / cobertura
                    float corTexto = cob > 0 ? Math.Min(255f, emPreto[o + c] * 255f / cob) : 0f;
                    float corBorda = c == 0 ? corContorno.B : c == 1 ? corContorno.G : corContorno.R;
                    pixels[o + c] = (byte)((corTexto * aTexto + corBorda * pc) / a);
                }
                pixels[o + 3] = Math.Max(alfaClique, (byte)Math.Round(a * 255));
            }
        }

        var saida = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var bits = saida.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < h; y++)
            Marshal.Copy(pixels, y * w * 4, bits.Scan0 + y * bits.Stride, w * 4);
        saida.UnlockBits(bits);
        return Dados.Fixada ? ComFundoFixado(saida) : saida;
    }

    /// <summary>Anotação fixada: cartão translúcido de cantos arredondados atrás do texto, com o alfinete no canto.</summary>
    Bitmap ComFundoFixado(Bitmap textoDesenhado)
    {
        int w = textoDesenhado.Width, h = textoDesenhado.Height;
        var cartao = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(cartao))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var fundo = TextoClaro ? Color.FromArgb(150, 12, 12, 16) : Color.FromArgb(170, 250, 250, 250);
            using (var caminho = RetanguloArredondado(new Rectangle(0, 0, w - 1, h - 1), LogicalToDeviceUnits(12)))
            using (var pincel = new SolidBrush(fundo))
            using (var borda = new Pen(Color.FromArgb(60, TextoClaro ? Color.White : Color.Black)))
            {
                g.FillPath(pincel, caminho);
                g.DrawPath(borda, caminho);
            }

            using var fonteAlfinete = new Font("Segoe MDL2 Assets", 11f);
            TextRenderer.DrawText(g, "", fonteAlfinete, AreaAlfinete(cartao.Size), CorAlfinete, Color.Transparent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            g.DrawImage(textoDesenhado, 0, 0, w, h);
        }
        textoDesenhado.Dispose();
        return cartao;
    }

    static System.Drawing.Drawing2D.GraphicsPath RetanguloArredondado(Rectangle r, int raio)
    {
        int d = raio * 2;
        var caminho = new System.Drawing.Drawing2D.GraphicsPath();
        caminho.AddArc(r.Left, r.Top, d, d, 180, 90);
        caminho.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        caminho.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        caminho.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        caminho.CloseFigure();
        return caminho;
    }

    /// <summary>Retângulo, nas coordenadas da anotação, onde o editor posiciona o texto.</summary>
    Rectangle AreaDoTexto(Size tamanho)
    {
        int esquerda = Margem, topo = AlturaBarra;
        int largura = tamanho.Width - 2 * Margem, altura = tamanho.Height - AlturaBarra - Margem;
        Native.SendMessage(texto.Handle, Native.EM_GETRECT, IntPtr.Zero, out Native.RECT r);
        if (r.Right > r.Left)
        {
            esquerda += r.Left;
            topo += r.Top;
            largura = r.Right - r.Left;
        }
        if (!Dados.QuebraLinha) largura = 20000;
        return new Rectangle(esquerda, topo, Math.Max(1, largura), Math.Max(1, altura));
    }

    byte[] Renderizar(int w, int h, Rectangle area, Color fundo)
    {
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(fundo);
            IntPtr hdc = g.GetHdc();
            try
            {
                int dpiX = Native.GetDeviceCaps(hdc, Native.LOGPIXELSX), dpiY = Native.GetDeviceCaps(hdc, Native.LOGPIXELSY);
                var rc = new Native.RECT
                {
                    Left = area.Left * 1440 / dpiX,
                    Top = area.Top * 1440 / dpiY,
                    Right = area.Right * 1440 / dpiX,
                    Bottom = area.Bottom * 1440 / dpiY,
                };
                var faixa = new Native.FORMATRANGE
                {
                    hdc = hdc,
                    hdcTarget = hdc,
                    rc = rc,
                    rcPage = rc,
                    chrg = new Native.CHARRANGE { cpMin = 0, cpMax = -1 },
                };
                Native.SendMessage(texto.Handle, Native.EM_FORMATRANGE, 1, ref faixa);
                Native.SendMessage(texto.Handle, Native.EM_FORMATRANGE, IntPtr.Zero, IntPtr.Zero); // libera o cache
            }
            finally
            {
                g.ReleaseHdc(hdc);
            }
        }

        var bits = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        var bytes = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            Marshal.Copy(bits.Scan0 + y * bits.Stride, bytes, y * w * 4, w * 4);
        bmp.UnlockBits(bits);
        return bytes;
    }

    /// <summary>Máximo numa vizinhança quadrada (feito em duas passadas: linhas e colunas).</summary>
    static byte[] Dilatar(byte[] m, int w, int h, int raio)
    {
        var tmp = new byte[m.Length];
        var res = new byte[m.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte max = 0;
                for (int xx = Math.Max(0, x - raio); xx <= Math.Min(w - 1, x + raio); xx++)
                    if (m[y * w + xx] > max) max = m[y * w + xx];
                tmp[y * w + x] = max;
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte max = 0;
                for (int yy = Math.Max(0, y - raio); yy <= Math.Min(h - 1, y + raio); yy++)
                    if (tmp[yy * w + x] > max) max = tmp[yy * w + x];
                res[y * w + x] = max;
            }
        return res;
    }

    // ---------- localizar / substituir (usado pelo FindReplaceForm) ----------

    public bool LocalizarProxima(string termo, bool diferenciarCaso)
    {
        ultimoTermo = termo;
        ultimoCaso = diferenciarCaso;
        var comparacao = diferenciarCaso ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        int i = texto.Text.IndexOf(termo, texto.SelectionStart + texto.SelectionLength, comparacao);
        if (i < 0) i = texto.Text.IndexOf(termo, 0, comparacao); // recomeça do início
        if (i < 0)
        {
            Dialogo(() => MessageBox.Show((IWin32Window?)busca ?? this, $"Não foi possível encontrar \"{termo}\".", "Notas",
                MessageBoxButtons.OK, MessageBoxIcon.Information));
            return false;
        }
        texto.Select(i, termo.Length);
        texto.ScrollToCaret();
        return true;
    }

    public void Substituir(string termo, string novo, bool diferenciarCaso)
    {
        var comparacao = diferenciarCaso ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (texto.SelectionLength > 0 && string.Equals(texto.SelectedText, termo, comparacao))
            texto.SelectedText = novo;
        LocalizarProxima(termo, diferenciarCaso);
    }

    public void SubstituirTudo(string termo, string novo, bool diferenciarCaso)
    {
        var comparacao = diferenciarCaso ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var posicoes = new List<int>();
        for (int i = texto.Text.IndexOf(termo, comparacao); i >= 0; i = texto.Text.IndexOf(termo, i + termo.Length, comparacao))
            posicoes.Add(i);

        // De trás para frente, trecho a trecho, para manter a formatação de cada ocorrência.
        for (int k = posicoes.Count - 1; k >= 0; k--)
        {
            texto.Select(posicoes[k], termo.Length);
            texto.SelectedText = novo;
        }
        Dialogo(() => MessageBox.Show((IWin32Window?)busca ?? this, $"{posicoes.Count} ocorrência(s) substituída(s).", "Notas"));
    }

    // ---------- menu ----------

    ContextMenuStrip CriarMenu()
    {
        var m = new ContextMenuStrip();

        ToolStripMenuItem Item(string nome, string? atalho, Action acao)
        {
            var item = new ToolStripMenuItem(nome, null, (_, _) => acao());
            if (atalho != null) item.ShortcutKeyDisplayString = atalho;
            return item;
        }

        var editar = Item("Editar", null, () => IniciarEdicao(null));
        editar.Font = new Font(editar.Font, FontStyle.Bold);
        var desfazer = Item("Desfazer", "Ctrl+Z", texto.Undo);
        var refazer = Item("Refazer", "Ctrl+Y", texto.Redo);
        var recortar = Item("Recortar", "Ctrl+X", texto.Cut);
        var copiar = Item("Copiar", "Ctrl+C", texto.Copy);
        var colar = Item("Colar", "Ctrl+V", ColarTexto);
        var excluir = Item("Excluir", "Del", () => texto.SelectedText = "");
        var selecionarTudo = Item("Selecionar tudo", "Ctrl+A", texto.SelectAll);
        var copiarTudo = Item("Copiar texto da anotação", null, () => Clipboard.SetText(TextoCrlf));

        var corTexto = new ToolStripMenuItem("Cor do texto");
        foreach (var (nome, hex) in CoresTexto)
            corTexto.DropDownItems.Add(Item(nome, null, () => AplicarCorPeloMenu(Cores.De(hex, Color.White))));
        corTexto.DropDownItems.Add(new ToolStripSeparator());
        corTexto.DropDownItems.Add(Item("Personalizada...", null, EscolherCor));

        var transparencia = new ToolStripMenuItem("Transparência");
        foreach (var percentual in new[] { 100, 85, 70, 55, 40 })
            transparencia.DropDownItems.Add(Item($"{percentual}% visível", null, () => { Dados.Opacidade = percentual / 100.0; AoMudarAparencia(); }));

        itemSombra = Item("Contorno para leitura", null, () => { Dados.Sombra = !Dados.Sombra; AoMudarAparencia(); });
        itemQuebra = Item("Quebra automática de linha", null, () => { Dados.QuebraLinha = !Dados.QuebraLinha; AoMudarAparencia(); });

        var formatar = new ToolStripMenuItem("Formatar");
        formatar.DropDownItems.AddRange(new ToolStripItem[]
        {
            Item("Fonte...", null, EscolherFonte),
            corTexto,
            Item("Negrito", "Ctrl+B", () => AlternarEfeito(Native.CFM_BOLD)),
            Item("Itálico", "Ctrl+I", () => AlternarEfeito(Native.CFM_ITALIC)),
            Item("Sublinhado", "Ctrl+U", () => AlternarEfeito(Native.CFM_UNDERLINE)),
            Item("Lista de tarefas", "Ctrl+Shift+T", () => { if (!Editando) IniciarEdicao(null); AlternarTarefas(); }),
            Item("Limpar formatação", "Ctrl+Espaço", LimparFormatacao),
            new ToolStripSeparator(),
            Item("Aumentar letra", "Ctrl+Shift+>", () => MudarTamanho(+2)),
            Item("Diminuir letra", "Ctrl+Shift+<", () => MudarTamanho(-2)),
            new ToolStripSeparator(),
            itemSombra,
            transparencia,
            itemQuebra,
        });

        var arquivo = new ToolStripMenuItem("Arquivo");
        arquivo.DropDownItems.AddRange(new ToolStripItem[]
        {
            Item("Abrir .txt...", "Ctrl+O", AbrirTxt),
            Item("Salvar como .txt", "Ctrl+S", () => SalvarTxt(false)),
            Item("Salvar como...", "Ctrl+Shift+S", () => SalvarTxt(true)),
        });

        var localizar = Item("Localizar...", "Ctrl+F", () => AbrirBusca(false));
        var proxima = Item("Localizar próxima", "F3", LocalizarProximaAtalho);
        var substituir = Item("Substituir...", "Ctrl+H", () => AbrirBusca(true));
        var dataHora = Item("Data/Hora", "F5", InserirDataHora);

        itemTravar = Item("Travar posição e tamanho", null, () => { Dados.Travada = !Dados.Travada; AoMudarAparencia(); });
        itemFixar = Item("Alfinete (por cima das janelas)", null, AlternarFixar);

        var sepEdicao1 = new ToolStripSeparator();
        var sepEdicao2 = new ToolStripSeparator();
        var sepEdicao3 = new ToolStripSeparator();
        var soEdicao = new ToolStripItem[] { desfazer, refazer, sepEdicao1, recortar, colar, excluir, selecionarTudo, sepEdicao2, localizar, proxima, substituir, dataHora, sepEdicao3 };

        m.Items.AddRange(new ToolStripItem[]
        {
            editar,
            desfazer, refazer,
            sepEdicao1,
            recortar, copiar, colar, excluir,
            selecionarTudo, copiarTudo,
            sepEdicao2,
            localizar, proxima, substituir, dataHora,
            sepEdicao3,
            formatar,
            arquivo,
            new ToolStripSeparator(),
            itemFixar,
            itemTravar,
            Item("Nova anotação", "Ctrl+N", () => NovaNotaSolicitada?.Invoke()),
            Item("Excluir anotação", null, ConfirmarExcluir),
        });

        m.Opening += (_, _) =>
        {
            // Fora da edição o menu mostra só o que faz sentido para o texto pronto.
            foreach (var item in soEdicao) item.Visible = Editando;
            editar.Visible = !Editando;
            copiar.Visible = Editando;
            copiarTudo.Visible = !Editando;

            bool temSelecao = texto.SelectionLength > 0;
            desfazer.Enabled = texto.CanUndo;
            refazer.Enabled = texto.CanRedo;
            recortar.Enabled = copiar.Enabled = excluir.Enabled = temSelecao;
            colar.Enabled = Clipboard.ContainsText();
        };
        return m;
    }

    // ---------- ações ----------

    public T Dialogo<T>(Func<T> abrir)
    {
        dialogoAberto = true;
        try { return abrir(); }
        finally { dialogoAberto = false; }
    }

    void ColarTexto()
    {
        // Cola como texto puro, já com a formatação de onde está o cursor.
        if (Clipboard.ContainsText()) texto.SelectedText = Clipboard.GetText().ReplaceLineEndings("\n");
    }

    void ConfirmarExcluir()
    {
        if (texto.Text.Trim().Length == 0 ||
            Dialogo(() => MessageBox.Show(this, "Excluir esta anotação? O texto será perdido.", "Notas",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question)) == DialogResult.Yes)
        {
            ExcluirSolicitado?.Invoke();
        }
    }

    void AbrirBusca(bool substituir)
    {
        if (!Editando) IniciarEdicao(null);
        if (busca == null || busca.IsDisposed)
            busca = new FindReplaceForm(this) { Location = new Point(Right + 8, Top) };
        var selecao = texto.SelectedText;
        busca.Configurar(substituir, selecao.Length > 0 && !selecao.Contains('\n') ? selecao : ultimoTermo, ultimoCaso);
        if (!busca.Visible) busca.Show(this);
        busca.Activate();
    }

    void LocalizarProximaAtalho()
    {
        if (ultimoTermo.Length == 0) AbrirBusca(false);
        else LocalizarProxima(ultimoTermo, ultimoCaso);
    }

    void InserirDataHora() => texto.SelectedText = DateTime.Now.ToString("HH:mm dd/MM/yyyy");

    void EscolherFonte()
    {
        using var dialogo = new FontDialog { Font = texto.SelectionFont ?? texto.Font, ShowEffects = true };
        if (Dialogo(() => dialogo.ShowDialog(this)) != DialogResult.OK) return;
        var fonte = dialogo.Font;
        Formatar(() => texto.SelectionFont = fonte);
        if (!Editando) GuardarPadrao(fonte);
    }

    void EscolherCor()
    {
        using var dialogo = new ColorDialog { Color = Cores.De(Dados.CorTexto, Color.White), FullOpen = true };
        if (Dialogo(() => dialogo.ShowDialog(this)) != DialogResult.OK) return;
        AplicarCorPeloMenu(dialogo.Color);
    }

    /// <summary>Pelo menu: na edição pinta a seleção; fora dela pinta tudo e vira a cor padrão da anotação.</summary>
    void AplicarCorPeloMenu(Color cor)
    {
        if (!Editando)
        {
            Dados.CorTexto = Cores.Hex(cor);
            AplicarAparencia();
        }
        DefinirCor(cor);
    }

    void GuardarPadrao(Font fonte)
    {
        Dados.Fonte = fonte.FontFamily.Name;
        Dados.TamanhoFonte = fonte.Size;
        Dados.Negrito = fonte.Bold;
        Dados.Italico = fonte.Italic;
    }

    void AbrirTxt()
    {
        using var dialogo = new OpenFileDialog { Filter = "Documentos de texto (*.txt)|*.txt|Todos os arquivos (*.*)|*.*" };
        if (Dialogo(() => dialogo.ShowDialog(this)) != DialogResult.OK) return;
        try
        {
            var conteudo = File.ReadAllText(dialogo.FileName).ReplaceLineEndings("\n");
            texto.SelectAll();
            texto.SelectedText = conteudo;
            texto.Select(0, 0);
            Dados.ArquivoTxt = dialogo.FileName;
            if (!Editando) camada.Redesenhar();
        }
        catch (Exception ex)
        {
            Dialogo(() => MessageBox.Show(this, "Não foi possível abrir o arquivo:\n" + ex.Message, "Notas", MessageBoxButtons.OK, MessageBoxIcon.Error));
        }
    }

    void SalvarTxt(bool escolherLocal)
    {
        var caminho = Dados.ArquivoTxt;
        if (escolherLocal || caminho == null)
        {
            using var dialogo = new SaveFileDialog
            {
                Filter = "Documentos de texto (*.txt)|*.txt|Todos os arquivos (*.*)|*.*",
                FileName = NomeSugerido(),
            };
            if (Dialogo(() => dialogo.ShowDialog(this)) != DialogResult.OK) return;
            caminho = dialogo.FileName;
        }
        try
        {
            File.WriteAllText(caminho, TextoCrlf, new UTF8Encoding(false));
            Dados.ArquivoTxt = caminho;
            Alterada?.Invoke();
        }
        catch (Exception ex)
        {
            Dialogo(() => MessageBox.Show(this, "Não foi possível salvar:\n" + ex.Message, "Notas", MessageBoxButtons.OK, MessageBoxIcon.Error));
        }
    }

    // ---------- aparência ----------

    void AoMudarAparencia()
    {
        AplicarAparencia();
        Alterada?.Invoke();
    }

    void AplicarAparencia()
    {
        var cor = Cores.De(Dados.CorTexto, Color.White);
        TextoClaro = cor.GetBrightness() >= 0.5f;

        // O fundo do editor é a "cor-chave" da transparência: some e deixa ver a área de trabalho.
        // Ela é escura para texto claro (e vice-versa), então a suavização das letras vira um contorno discreto.
        var chave = TextoClaro ? Color.FromArgb(1, 2, 3) : Color.FromArgb(253, 254, 252);
        BackColor = TransparencyKey = barra.BackColor = texto.BackColor = chave;
        btnMenu.ForeColor = btnMover.ForeColor = btnNova.ForeColor = btnFechar.ForeColor = cor;

        texto.WordWrap = Dados.QuebraLinha;
        texto.ScrollBars = Dados.QuebraLinha ? RichTextBoxScrollBars.Vertical : RichTextBoxScrollBars.Both;

        itemQuebra.Checked = Dados.QuebraLinha;
        itemSombra.Checked = Dados.Sombra;
        itemTravar.Checked = Dados.Travada;
        AplicarTemaRolagem();
        AplicarFixacao();
        camada.Redesenhar();
    }

    /// <summary>Barra de rolagem escura quando o texto é claro (o véu de edição é escuro).</summary>
    void AplicarTemaRolagem()
    {
        if (!texto.IsHandleCreated || TextoClaro == temaEscuro) return;
        temaEscuro = TextoClaro;
        Native.SetWindowTheme(texto.Handle, TextoClaro ? "DarkMode_Explorer" : "Explorer", null);
    }

    string NomeSugerido()
    {
        var primeiraLinha = texto.Text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "anotação";
        var nome = string.Concat(primeiraLinha.Split(Path.GetInvalidFileNameChars())).Trim();
        if (nome.Length > 40) nome = nome[..40].Trim();
        return (nome.Length > 0 ? nome : "anotação") + ".txt";
    }

    Label CriarBotao(string simbolo, string dica)
    {
        var botao = new Label
        {
            Text = simbolo,
            Width = LogicalToDeviceUnits(26),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI Symbol", 10f),
            BackColor = Color.Transparent,
        };
        dicas.SetToolTip(botao, dica);
        return botao;
    }

    static Rectangle PosicaoValida(Rectangle r)
    {
        // Se o monitor onde a anotação estava não existe mais, traz para o principal.
        if (!Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(r)))
        {
            var area = Screen.PrimaryScreen!.WorkingArea;
            r.Location = new Point(area.Left + 40, area.Top + 40);
        }
        return r;
    }
}
