# Notas na Área de Trabalho

Escreva direto na área de trabalho do Windows: o texto aparece sobre o papel de parede, sem caixa nem janela.

## Instalação

Funciona no **Windows 10 e 11**, só para o seu usuário: **não precisa de administrador**.

### 1. Instale o .NET 9 SDK

O instalador compila o programa no seu computador, então ele precisa do **.NET 9 SDK**
(se você já tem, pode pular).

- Baixe em <https://dotnet.microsoft.com/download/dotnet/9.0>: na coluna **SDK**, escolha
  **Windows → x64** (Installer) e instale normalmente; **ou**
- pelo terminal, com o winget:

  ```powershell
  winget install Microsoft.DotNet.SDK.9
  ```

Para conferir, abra um terminal **novo** e rode `dotnet --list-sdks`: deve aparecer uma linha começando com `9.`.

### 2. Baixe o projeto

Com o Git:

```powershell
git clone https://github.com/daniellanunes/desktopnotes.git
cd desktopnotes
```

Sem o Git: no GitHub, clique em **Code → Download ZIP**, extraia o arquivo e abra um terminal dentro da pasta extraída
(no Explorador de Arquivos, clique com o botão direito dentro da pasta → **Abrir no Terminal**).

### 3. Rode o instalador

Dentro da pasta do projeto:

```powershell
powershell -ExecutionPolicy Bypass -File instalar.ps1
```

O instalador:

1. compila o programa e o copia para `%LOCALAPPDATA%\NotasDesktop\app`;
2. adiciona **"Escrever nota aqui"** no menu do botão direito da área de trabalho;
3. faz o programa abrir junto com o Windows (para as anotações reaparecerem ao ligar o PC);
4. já deixa o programa rodando: aparece um ícone de anotação na bandeja, perto do relógio.

Pronto! Clique com o botão direito na área de trabalho → **Escrever nota aqui**.

> **Windows 11:** o menu novo do botão direito não mostra opções de outros programas. Clique em
> **Mostrar mais opções** (ou use `Shift+F10`) para ver **Escrever nota aqui**, ou use o atalho **Ctrl+Alt+N**,
> que funciona de qualquer lugar.

### Atualizar

Baixe a versão nova (`git pull` ou um novo ZIP) e rode o `instalar.ps1` de novo. Ele fecha a versão aberta
e instala por cima, **sem apagar suas anotações**.

### Desinstalar

```powershell
powershell -ExecutionPolicy Bypass -File desinstalar.ps1                    # mantém as anotações guardadas
powershell -ExecutionPolicy Bypass -File desinstalar.ps1 -ApagarAnotacoes   # apaga tudo
```

Isso remove o programa, a opção do botão direito e a inicialização com o Windows.

### Problemas comuns

| Problema | Solução |
|---|---|
| `dotnet` não é reconhecido | Instale o .NET 9 SDK (passo 1) e abra um terminal **novo**. |
| "Falha na compilação" | Confira com `dotnet --list-sdks` se há um SDK `9.x` (só o *Runtime* não basta). |
| "a execução de scripts foi desabilitada" | Rode exatamente como no passo 3, com `-ExecutionPolicy Bypass`. |
| Não aparece "Escrever nota aqui" | No Windows 11, use **Mostrar mais opções** ou **Ctrl+Alt+N**. |
| As anotações sumiram atrás das janelas | É o normal: elas ficam no papel de parede. Use **Win+D**, o ícone da bandeja → **Trazer anotações para a frente**, ou o 📌 alfinete. |

Suas anotações ficam guardadas em `%APPDATA%\NotasDesktop\notas.json`.

## Como usar

1. Clique com o botão direito na área de trabalho → **Escrever nota aqui**
   (no Windows 11: **Mostrar mais opções** ou `Shift+F10`). Atalho global: **Ctrl+Alt+N**.
2. **Clique** onde quer escrever (ou **arraste** para definir a largura da linha) e digite.
3. `Esc` ou clicar fora termina. A anotação fica no papel de parede e é salva sozinha.

Depois:

- **Clique no texto** para editar. Para mover, entre na edição (fora dela a anotação fica parada no lugar).
- Editando: segure o **✥** (ou a faixa pontilhada de cima) para mover, e o canto inferior direito para
  redimensionar; depois de soltar você continua digitando.
- **📌 Alfinete** (na barra da anotação ou no menu): a anotação fica por cima de todas as janelas, até
  das maximizadas, com um fundo escuro translúcido. Clique no alfinete laranja do canto para soltar.
- **Botão direito no texto**: cor, fonte, contorno, transparência, travar, salvar em `.txt`, excluir.
- Anotação que ficar vazia é apagada automaticamente.
- Ícone na bandeja: nova anotação, trazer anotações para a frente, pasta dos dados, sair.

### Formatação (durante a edição)

Enquanto você edita, uma barra flutua acima do texto: fonte, tamanho, aumentar/diminuir letra,
negrito, itálico, sublinhado, tachado, cor do texto, realce (marca-texto), alinhamento, lista e
limpar formatação. Vale para o trecho selecionado (ou para o que você digitar em seguida).

| Atalho | Ação |
|---|---|
| Ctrl+B / Ctrl+I / Ctrl+U | Negrito / itálico / sublinhado |
| Ctrl+Shift+> / Ctrl+Shift+< | Aumentar / diminuir letra |
| Ctrl+L / Ctrl+E / Ctrl+R | Alinhar à esquerda / centro / direita |
| Ctrl+Shift+L | Lista com marcadores |
| Ctrl+Shift+T | Lista de tarefas: clique na bolinha ○ para marcar ● e riscar a frase (funciona até fora da edição) |
| Ctrl+Espaço | Limpar formatação |

### Funções de bloco de notas (durante a edição)

| Atalho | Ação |
|---|---|
| Ctrl+Z / Ctrl+Y | Desfazer / refazer |
| Ctrl+X / C / V | Recortar / copiar / colar (sempre texto puro) |
| Ctrl+A | Selecionar tudo |
| Ctrl+F / F3 / Ctrl+H | Localizar / próxima / substituir |
| F5 | Data e hora |
| Ctrl+S / Ctrl+Shift+S | Salvar como `.txt` |
| Ctrl+O | Abrir `.txt` na anotação |
| Ctrl+N | Nova anotação |

## Como funciona

- `DesktopLayer`: janela com transparência por pixel (`UpdateLayeredWindow`) que desenha o texto sobre o
  papel de parede. Pixels vazios deixam o clique passar para a área de trabalho. A dona da janela é a
  própria área de trabalho (`Progman`), então as anotações ficam atrás das outras janelas e continuam
  visíveis no Win+D.
  Com o alfinete, ela vira "sempre visível" e ganha um cartão translúcido.
- `NoteForm`: editor de texto rico (RichTextBox) com fundo em cor-chave transparente, que aparece por cima
  da camada só enquanto você edita. Fora da edição, o próprio motor do editor (`EM_FORMATRANGE`) desenha o
  texto formatado, em fundo preto e em fundo branco, e a diferença entre os dois dá a transparência.
- `FormatToolbar`: a barra de formatação que flutua acima da anotação durante a edição.
- `NotesApp`: instância única (mutex + named pipe), ícone na bandeja, atalho global e salvamento.
- `PickerOverlay`: a camada escura onde você escolhe o local.
