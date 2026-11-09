$body = @{ action = 'inspect_all'; max_items = 50 } | ConvertTo-Json
$res = Invoke-RestMethod -Uri 'http://127.0.0.1:18888/civil' -Method Post -Body $body -ContentType 'application/json'
Write-Host "Summary of Parcels:"
$res.parcels | Format-Table -AutoSize
Write-Host "Parcel JSON:"
$res.parcels | ConvertTo-Json -Depth 5
