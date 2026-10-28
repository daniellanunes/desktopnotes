# Compila e instala o "Notas na Área de Trabalho" para o usuário atual (não precisa de administrador).
#  - copia o programa para %LOCALAPPDATA%\NotasDesktop\app
#  - adiciona "Escrever nota aqui" no menu do botão direito da área de trabalho
#  - inicia junto com o Windows (para as anotações reaparecerem)
# Uso: powershell -ExecutionPolicy Bypass -File instalar.ps1
$ErrorActionPreference = 'Stop'

$destino = Join-Path $env:LOCALAPPDATA 'NotasDesktop\app'
$exe = Join-Path $destino 'NotasDesktop.exe'

if (Test-Path $exe) {
    Write-Host 'Encerrando a versão em execução...'
    & $exe --sair
    Wait-Process -Name NotasDesktop -Timeout 5 -ErrorAction SilentlyContinue
    Get-Process -Name NotasDesktop -ErrorAction SilentlyContinue | Stop-Process -Force
}

Write-Host 'Compilando...'
dotnet publish (Join-Path $PSScriptRoot 'src\NotasDesktop.csproj') -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -o $destino --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação.' }

Write-Host 'Registrando no menu da área de trabalho...'
$menu = 'HKCU:\Software\Classes\DesktopBackground\Shell\NotasDesktop'
New-Item -Path $menu -Force | Out-Null
Set-ItemProperty -Path $menu -Name 'MUIVerb' -Value 'Escrever nota aqui'
Set-ItemProperty -Path $menu -Name 'Icon' -Value "$exe,0"
Set-ItemProperty -Path $menu -Name 'Position' -Value 'Top'
New-Item -Path "$menu\command" -Value "`"$exe`" --nova" -Force | Out-Null

Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'NotasDesktop' -Value "`"$exe`""

Start-Process $exe
Write-Host ''
Write-Host 'Pronto! Clique com o botão direito na área de trabalho > "Escrever nota aqui".'
Write-Host '(No Windows 11 a opção fica em "Mostrar mais opções" ou Shift+F10.) Atalho rápido: Ctrl+Alt+N.'
