param([string]$CustomRoot = (Split-Path $PSScriptRoot), [string]$UpstreamRoot = 'C:\Users\sihun\Downloads\Aimmy2')
$ErrorActionPreference = 'Stop'
function Get-Inventory([string]$Root) {
    $map = @{}
    Get-ChildItem -LiteralPath $Root -Recurse -File -Force | Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]' } | ForEach-Object {
        $relative = $_.FullName.Substring($Root.TrimEnd('\').Length + 1).Replace('\','/')
        $map[$relative] = [PSCustomObject]@{ Size = $_.Length; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    }
    return $map
}
$custom = Get-Inventory $CustomRoot
$upstream = Get-Inventory $UpstreamRoot
$paths = @($custom.Keys) + @($upstream.Keys) | Sort-Object -Unique
$rows = foreach ($path in $paths) {
    $a = $custom[$path]; $b = $upstream[$path]
    $comparison = if (!$a) { 'Upstream-only' } elseif (!$b) { 'Custom-only' } elseif ($a.SHA256 -eq $b.SHA256) { 'Identical' } else { 'Different-no-common-base' }
    $category = if ($path -match '(^|/)(\.vs|obj|Debug|Release|Build|build|artifacts|TestResults)/') { 'Build-or-runtime-review' } elseif ($path -match '\.(dll|exe|lib)$') { 'Native-or-runtime' } elseif ($path -match '\.(onnx|engine|trt|cfg|json|ini)$') { 'Model-or-config' } elseif ($path -match '^(scratch|tests|tools)/') { 'Test-debug-tool' } else { 'Source-resource' }
    [PSCustomObject]@{ Path=$path; Comparison=$comparison; Category=$category; CustomBytes=$a.Size; UpstreamBytes=$b.Size; CustomSHA256=$a.SHA256; UpstreamSHA256=$b.SHA256 }
}
New-Item -ItemType Directory -Force -Path (Join-Path $CustomRoot 'reports') | Out-Null
$rows | Export-Csv -NoTypeInformation -Encoding utf8 -LiteralPath (Join-Path $CustomRoot 'reports/inventory.csv')
$rows | Group-Object Comparison | Select-Object Name,Count
