$ref = '$ref'

Push-Location $PSScriptRoot/../swagger/parameters
Get-ChildItem -Recurse | where { $_.Extension -eq '.yaml' } | where { $_.Name -ne '_index.yaml' } | foreach {
    Write-Output "  $($_.BaseName):"
    $relativePath = Resolve-Path -Relative -Path $_.FullName
    $relativePath = $relativePath -replace "\\", "/"
    Write-Output "    ${ref}: '$($relativePath)'"
} | Out-File -FilePath _index.yaml -Encoding utf8
Pop-Location