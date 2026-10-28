# Remove o "Notas na Área de Trabalho". As anotações ficam guardadas em %APPDATA%\NotasDesktop,
# a não ser que você use -ApagarAnotacoes.
# Uso: powershell -ExecutionPolicy Bypass -File desinstalar.ps1 [-ApagarAnotacoes]
param([switch]$ApagarAnotacoes)

$pasta = Join-Path $env:LOCALAPPDATA 'NotasDesktop'
$exe = Join-Path $pasta 'app\NotasDesktop.exe'

if (Test-Path $exe) {
    & $exe --sair
    Wait-Process -Name NotasDesktop -Timeout 5 -ErrorAction SilentlyContinue
    Get-Process -Name NotasDesktop -ErrorAction SilentlyContinue | Stop-Process -Force
}

Remove-Item -Path 'HKCU:\Software\Classes\DesktopBackground\Shell\NotasDesktop' -Recurse -ErrorAction SilentlyContinue
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'NotasDesktop' -ErrorAction SilentlyContinue
Remove-Item -Path $pasta -Recurse -Force -ErrorAction SilentlyContinue

if ($ApagarAnotacoes) {
    Remove-Item -Path (Join-Path $env:APPDATA 'NotasDesktop') -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host 'Desinstalado.'
