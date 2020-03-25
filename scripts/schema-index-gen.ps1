$ref = '$ref'
Push-Location ./schemas
Get-ChildItem | where { $_.Name -ne '_index.yaml' } | foreach {
    Write-Output "$($_.BaseName):"
    Write-Output "  ${ref}: './$($_.Name)'"
} | Out-File -FilePath _index.yaml -Encoding utf8
Pop-Location