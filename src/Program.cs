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

        ApplicationConfiguration.Initialize();
        Application.Run(new NotesApp(novaNota: comando == "--nova"));
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
