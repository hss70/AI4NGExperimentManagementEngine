#!/usr/bin/env pwsh
param(
    [string]$BaseUrl = "https://3mybicfkv2.execute-api.eu-west-2.amazonaws.com/dev",
    [string]$JwtToken = $env:JWT_TOKEN,
    [string]$FolderPath = "questionnaires-import"
)

if ([string]::IsNullOrEmpty($JwtToken)) {
    Write-Error "JWT_TOKEN environment variable not set"
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $JwtToken"
    "Content-Type" = "application/json"
}

Write-Host "Reading questionnaire files..." -ForegroundColor Green
$questionnaires = @()

Get-ChildItem -Path $FolderPath -Filter "*.json" | ForEach-Object {
    Write-Host "Reading $($_.Name)..." -ForegroundColor Yellow
    $content = Get-Content $_.FullName -Raw | ConvertFrom-Json
    $questionnaires += $content
}

if ($questionnaires.Count -eq 0) {
    Write-Error "No questionnaire files found in $FolderPath"
    exit 1
}

Write-Host "Uploading $($questionnaires.Count) questionnaires in batch..." -ForegroundColor Green
$batchBody = $questionnaires | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/researcher/questionnaires/batch" -Method POST -Headers $headers -Body $batchBody
    Write-Host "✓ Batch upload completed" -ForegroundColor Green
    $response.results | ForEach-Object {
        if ($_.status -eq "success") {
            Write-Host "  ✓ $($_.id)" -ForegroundColor Green
        } else {
            Write-Host "  ✗ $($_.id): $($_.error)" -ForegroundColor Red
        }
    }
}
catch {
    Write-Host "✗ Batch upload failed: $($_.Exception.Message)" -ForegroundColor Red
}