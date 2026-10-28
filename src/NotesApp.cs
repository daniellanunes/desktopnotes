using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.Win32;

namespace NotasDesktop;

/// <summary>Instância principal: guarda as notas abertas, o ícone da bandeja, o atalho global e o pipe.</summary>
sealed class NotesApp : ApplicationContext
{
    readonly List<NoteForm> notas = new();
    readonly Control invocador = new();
    readonly System.Windows.Forms.Timer timerSalvar = new() { Interval = 700 };
    readonly Icon icone;
    readonly HotkeyWindow atalho;
    readonly NotifyIcon bandeja;
    PickerOverlay? seletor;

    public NotesApp(bool novaNota)
    {
        _ = invocador.Handle; // cria o handle para poder usar BeginInvoke a partir do pipe
        icone = CarregarIcone();
        timerSalvar.Tick += (_, _) => { timerSalvar.Stop(); Salvar(); };

        foreach (var dados in Store.Carregar().Where(d => d.Texto.Trim().Length > 0))
            AbrirNota(dados);

        atalho = new HotkeyWindow(IniciarSelecao);
        bandeja = new NotifyIcon
        {
            Icon = new Icon(icone, SystemInformation.SmallIconSize),
            Text = "Notas na Área de Trabalho",
            Visible = true,
            ContextMenuStrip = CriarMenuBandeja(),
        };
        bandeja.DoubleClick += (_, _) => IniciarSelecao();

        SystemEvents.SessionEnding += AoEncerrarSessao;
        _ = Task.Run(ServidorPipeAsync);

        if (novaNota) IniciarSelecao();
    }

    ContextMenuStrip CriarMenuBandeja()
    {
        var menu = new ContextMenuStrip();
        var nova = new ToolStripMenuItem("Escrever na área de trabalho", null, (_, _) => IniciarSelecao());
        if (atalho.Registrado) nova.ShortcutKeyDisplayString = "Ctrl+Alt+N";
        nova.Font = new Font(nova.Font, FontStyle.Bold);
        menu.Items.Add(nova);
        menu.Items.Add("Trazer anotações para a frente", null, (_, _) => MostrarTodas());
        menu.Items.Add("Abrir pasta das notas", null, (_, _) =>
        {
            Directory.CreateDirectory(Store.Pasta);
            Process.Start("explorer.exe", Store.Pasta);
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => Sair());
        return menu;
    }

    void IniciarSelecao()
    {
        if (seletor != null) { seletor.Activate(); return; }
        seletor = new PickerOverlay();
        seletor.AreaEscolhida += CriarNota;
        seletor.FormClosed += (_, _) => seletor = null;
        seletor.Show();
        seletor.Activate();
    }

    void CriarNota(Rectangle area)
    {
        var nota = AbrirNota(new NoteData { X = area.X, Y = area.Y, Largura = area.Width, Altura = area.Height });
        nota.IniciarEdicao(null);
    }

    NoteForm AbrirNota(NoteData dados)
    {
        var nota = new NoteForm(dados, icone);
        nota.Alterada += AgendarSalvar;
        nota.NovaNotaSolicitada += IniciarSelecao;
        nota.ExcluirSolicitado += () =>
        {
            notas.Remove(nota);
            nota.PermitirFechar = true;
            nota.Close();
            Salvar();
        };
        notas.Add(nota);
        nota.Mostrar();
        return nota;
    }

    void MostrarTodas()
    {
        foreach (var nota in notas) nota.Mostrar();
    }

    void AgendarSalvar()
    {
        timerSalvar.Stop();
        timerSalvar.Start();
    }

    void Salvar()
    {
        foreach (var nota in notas) nota.AtualizarDados();
        try
        {
            Store.Salvar(notas.Select(n => n.Dados));
        }
        catch (Exception ex)
        {
            bandeja.ShowBalloonTip(5000, "Notas", "Não foi possível salvar as notas: " + ex.Message, ToolTipIcon.Error);
        }
    }

    void AoEncerrarSessao(object? sender, SessionEndingEventArgs e) => invocador.Invoke(Salvar);

    void Sair()
    {
        timerSalvar.Stop();
        Salvar();
        SystemEvents.SessionEnding -= AoEncerrarSessao;
        bandeja.Visible = false;
        atalho.Dispose();
        foreach (var nota in notas)
        {
            nota.PermitirFechar = true;
            nota.Close();
        }
        ExitThread();
    }

    async Task ServidorPipeAsync()
    {
        while (true)
        {
            try
            {
                using var servidor = new NamedPipeServerStream(Program.NomePipe, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await servidor.WaitForConnectionAsync();
                using var leitor = new StreamReader(servidor);
                var comando = (await leitor.ReadLineAsync())?.Trim();
                invocador.BeginInvoke(() =>
                {
                    switch (comando)
                    {
                        case "nova": IniciarSelecao(); break;
                        case "sair": Sair(); break;
                        default: MostrarTodas(); break;
                    }
                });
            }
            catch
            {
                await Task.Delay(500);
            }
        }
    }

    static Icon CarregarIcone()
    {
        try
        {
            if (Environment.ProcessPath is { } caminho && Icon.ExtractAssociatedIcon(caminho) is { } icone)
                return icone;
        }
        catch { }
        return SystemIcons.Application;
    }
}
