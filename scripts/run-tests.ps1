# Unit test runner script
param(
    [string]$Project = "all",
    [switch]$Coverage = $false,
    [switch]$Watch = $false
)

$ErrorActionPreference = "Stop"

Write-Host "🧪 Running unit tests..." -ForegroundColor Cyan

$testProjects = @(
    "tests/AI4NGQuestionnaires.Tests",
    "tests/AI4NGExperiments.Tests", 
    "tests/AI4NGResponses.Tests"
)

if ($Project -ne "all") {
    $testProjects = $testProjects | Where-Object { $_ -like "*$Project*" }
}

foreach ($proj in $testProjects) {
    if (Test-Path $proj) {
        Write-Host "Running tests for $proj..." -ForegroundColor Yellow
        
        $args = @("test", $proj, "--verbosity", "normal")
        
        if ($Coverage) {
            $args += @("--collect", "XPlat Code Coverage")
        }
        
        if ($Watch) {
            $args += "--watch"
        }
        
        & dotnet @args
        
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Tests failed for $proj"
            exit 1
        }
    }
}

Write-Host "✅ All tests passed!" -ForegroundColor Green

if ($Coverage) {
    Write-Host "📊 Coverage reports generated in TestResults folders" -ForegroundColor Cyan
}