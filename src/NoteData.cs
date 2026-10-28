using System.Text.Json;

namespace NotasDesktop;

sealed class NoteData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int X { get; set; }
    public int Y { get; set; }
    public int Largura { get; set; } = 380;
    public int Altura { get; set; } = 200;
    public string Texto { get; set; } = "";
    /// <summary>Texto com formatação (negrito, cores, tamanhos...). Quando existe, tem prioridade sobre <see cref="Texto"/>.</summary>
    public string? Rtf { get; set; }
    public string Fonte { get; set; } = "Segoe UI";
    public float TamanhoFonte { get; set; } = 16f;
    public bool Negrito { get; set; }
    public bool Italico { get; set; }
    public string CorTexto { get; set; } = "#FFFFFF";
    public bool Sombra { get; set; } = true;
    public double Opacidade { get; set; } = 1.0;
    public bool QuebraLinha { get; set; } = true;
    public bool Travada { get; set; }
    /// <summary>Alfinete: fica por cima de todas as janelas, com fundo escuro translúcido.</summary>
    public bool Fixada { get; set; }
    public string? ArquivoTxt { get; set; }
}

static class Store
{
    /// <summary>Pasta dos dados; a variável NOTASDESKTOP_DADOS permite trocar (útil para testes).</summary>
    public static string Pasta { get; } =
        Environment.GetEnvironmentVariable("NOTASDESKTOP_DADOS") is { Length: > 0 } pasta
            ? pasta
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NotasDesktop");

    static string Arquivo => Path.Combine(Pasta, "notas.json");
    static readonly JsonSerializerOptions Opcoes = new() { WriteIndented = true };

    public static List<NoteData> Carregar()
    {
        if (!File.Exists(Arquivo)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<NoteData>>(File.ReadAllText(Arquivo)) ?? new();
        }
        catch
        {
            // Guarda o arquivo ilegível em vez de sobrescrevê-lo no próximo salvamento.
            File.Copy(Arquivo, Path.Combine(Pasta, $"notas.corrompido.{DateTime.Now:yyyyMMddHHmmss}.json"), true);
            return new();
        }
    }

    public static void Salvar(IEnumerable<NoteData> notas)
    {
        Directory.CreateDirectory(Pasta);
        var temporario = Arquivo + ".tmp";
        File.WriteAllText(temporario, JsonSerializer.Serialize(notas, Opcoes));
        File.Move(temporario, Arquivo, true);
    }
}

static class Cores
{
    public static Color De(string hex, Color padrao)
    {
        try { return ColorTranslator.FromHtml(hex); }
        catch { return padrao; }
    }

    public static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>Anda uma fração <paramref name="t"/> de <paramref name="a"/> em direção a <paramref name="b"/>.</summary>
    public static Color Misturar(Color a, Color b, double t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t),
        (int)(a.G + (b.G - a.G) * t),
        (int)(a.B + (b.B - a.B) * t));
}
