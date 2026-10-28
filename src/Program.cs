using System.IO.Pipes;

namespace NotasDesktop;

static class Program
{
    const string NomeMutex = "NotasDesktop.InstanciaUnica";
    public const string NomePipe = "NotasDesktop.Pipe";

    // Uso:
    //   NotasDesktop.exe          -> abre as notas salvas (usado na inicialização do Windows)
    //   NotasDesktop.exe --nova   -> escolher um local na tela e criar uma nota
    //   NotasDesktop.exe --sair   -> salva e encerra a instância em execução
    [STAThread]
    static void Main(string[] args)
    {
        var comando = args.Select(a => a.ToLowerInvariant())
                          .FirstOrDefault(a => a is "--nova" or "--sair" or "--mostrar") ?? "--mostrar";

        using var mutex = new Mutex(true, NomeMutex, out bool primeiraInstancia);
        if (!primeiraInstancia)
        {
            EnviarParaInstancia(comando.TrimStart('-'));
            return;
        }
        if (comando == "--sair") return;

        RegistrarInicio(args);
        ApplicationConfiguration.Initialize();
        Application.Run(new NotesApp(novaNota: comando == "--nova"));
    }

    /// <summary>Anota cada abertura em inicio.log (ajuda a conferir se abriu junto com o Windows).</summary>
    static void RegistrarInicio(string[] args)
    {
        try
        {
            Directory.CreateDirectory(Store.Pasta);
            var log = Path.Combine(Store.Pasta, "inicio.log");
            if (File.Exists(log) && new FileInfo(log).Length > 64 * 1024) File.Delete(log);
            var ligadoHa = TimeSpan.FromMilliseconds(Environment.TickCount64);
            File.AppendAllText(log, $"{DateTime.Now:dd/MM/yyyy HH:mm:ss}  aberto {string.Join(' ', args)}  (PC ligado há {ligadoHa:hh\\:mm\\:ss})\n");
        }
        catch { }
    }

    static void EnviarParaInstancia(string comando)
    {
        try
        {
            using var cliente = new NamedPipeClientStream(".", NomePipe, PipeDirection.Out);
            cliente.Connect(3000);
            using var escritor = new StreamWriter(cliente);
            escritor.WriteLine(comando);
        }
        catch
        {
            // A instância principal pode estar encerrando; não há o que fazer.
        }
    }
}
