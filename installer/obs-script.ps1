# Registra (ou remove) o script do OBS na lista Ferramentas > Scripts da colecao de cenas ativa.
# O OBS nao tem pasta de carga automatica: os scripts carregados ficam em "modules"."scripts-tool" do JSON da
# colecao. O JSON e editado como texto (so a lista de scripts), validado antes de gravar, e ganha um backup.
# Chamado pelo instalador; tambem serve para uso manual.
# Saida: 0 ok | 1 erro | 2 OBS sem colecao de cenas | 3 OBS aberto (ele reescreveria o arquivo ao fechar)
param(
    [Parameter(Mandatory)][ValidateSet("Register", "Unregister")][string]$Action,
    [Parameter(Mandatory)][string]$Script,
    [string]$ObsConfig = (Join-Path $env:APPDATA "obs-studio")
)
$ErrorActionPreference = "Stop"
$utf8 = New-Object System.Text.UTF8Encoding($false)
$scenes = Join-Path $ObsConfig "basic\scenes"
$name = [IO.Path]::GetFileName($Script)

function Get-ActiveCollection {
    foreach ($ini in "user.ini", "global.ini") {
        $file = Join-Path $ObsConfig $ini
        if (-not (Test-Path $file)) { continue }
        $line = Get-Content $file -Encoding UTF8 | Where-Object { $_ -match "^SceneCollectionFile=(.+\.json)\s*$" } | Select-Object -First 1
        if ($line) {
            $path = Join-Path $scenes ($Matches[1])
            if (Test-Path $path) { return $path }
        }
    }
    $all = @(Get-ChildItem $scenes -Filter "*.json" -ErrorAction SilentlyContinue | Where-Object { $_.Extension -eq ".json" })
    if ($all.Count -eq 1) { return $all[0].FullName }
    return $null
}

function ConvertTo-JsonString([string]$value) {
    '"' + (($value -replace "\\", "/") -replace '"', '\"') + '"'
}

function Get-ScriptEntries([string]$json) {
    $data = $json | ConvertFrom-Json
    if ($data.modules -and $data.modules."scripts-tool") { return @($data.modules."scripts-tool" | ForEach-Object { $_.path }) }
    return @()
}

function Add-Entry([string]$text, [string]$path) {
    $entry = '{"path": ' + (ConvertTo-JsonString $path) + ', "settings": {}}'
    $same = [regex]::Match($text, '"path"\s*:\s*"([^"]*[/\\]' + [regex]::Escape($name) + ')"')
    if ($same.Success) {
        # Ja registrado (talvez de outra pasta): so troca o caminho e mantem as configuracoes do script.
        $g = $same.Groups[1]
        return $text.Substring(0, $g.Index) + ((ConvertTo-JsonString $path).Trim('"')) + $text.Substring($g.Index + $g.Length)
    }
    $list = [regex]::Match($text, '"scripts-tool"\s*:\s*\[')
    if ($list.Success) {
        $end = $list.Index + $list.Length
        $empty = $text.Substring($end) -match "^\s*\]"
        return $text.Insert($end, $entry + $(if ($empty) { "" } else { "," }))
    }
    $modules = [regex]::Match($text, '"modules"\s*:\s*\{')
    if ($modules.Success) {
        $end = $modules.Index + $modules.Length
        $empty = $text.Substring($end) -match "^\s*\}"
        return $text.Insert($end, '"scripts-tool": [' + $entry + ']' + $(if ($empty) { "" } else { "," }))
    }
    $open = $text.IndexOf("{")
    return $text.Insert($open + 1, '"modules": {"scripts-tool": [' + $entry + ']},')
}

function Remove-Entry([string]$text, [string]$path) {
    $p = [regex]::Escape((ConvertTo-JsonString $path).Trim('"'))
    $obj = '\{\s*"path"\s*:\s*"' + $p + '"\s*,\s*"settings"\s*:\s*\{[^{}]*\}\s*\}'
    $after = [regex]::Replace($text, $obj + '\s*,', "", 1)
    if ($after -ne $text) { return $after }
    $after = [regex]::Replace($text, ',\s*' + $obj, "", 1)
    if ($after -ne $text) { return $after }
    return [regex]::Replace($text, $obj, "", 1)
}

try {
    if (-not (Test-Path $scenes)) { exit 2 }
    $file = Get-ActiveCollection
    if (-not $file) { exit 2 }
    if (Get-Process -Name "obs64", "obs32" -ErrorAction SilentlyContinue) { exit 3 }

    $text = [IO.File]::ReadAllText($file, $utf8)
    $normalized = ($Script -replace "\\", "/")
    if ($Action -eq "Register") { $result = Add-Entry $text $Script } else { $result = Remove-Entry $text $Script }
    if ($result -eq $text) { exit 0 }

    # So grava se continua sendo JSON valido e a lista ficou como esperado.
    $entries = Get-ScriptEntries $result
    $present = @($entries | Where-Object { $_ -eq $normalized }).Count -gt 0
    if ($Action -eq "Register" -and -not $present) { throw "o script nao apareceu na lista" }
    if ($Action -eq "Unregister" -and $present) { throw "o script continuou na lista" }

    Copy-Item $file "$file.scarab-eye.bak" -Force
    $temp = "$file.scarab-eye.tmp"
    [IO.File]::WriteAllText($temp, $result, $utf8)
    Move-Item $temp $file -Force
    exit 0
}
catch {
    Write-Host "obs-script: $($_.Exception.Message)"
    exit 1
}
