#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Runs all integration tests for the AI4NG Experiment Management system
.DESCRIPTION
    This script runs controller integration tests, route validation tests, and service wiring tests
    to ensure all endpoints are properly wired up and configured.
#>

param(
    [string]$Configuration = "Debug",
    [switch]$Verbose,
    [switch]$Coverage
)

$ErrorActionPreference = "Stop"

Write-Host "🧪 Running AI4NG Experiment Management Integration Tests" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray

# Set working directory to solution root
$SolutionRoot = Split-Path -Parent $PSScriptRoot
Set-Location $SolutionRoot

# Test projects to run
$TestProjects = @(
    "tests/AI4NGExperiments.Tests/AI4NGExperiments.Tests.csproj",
    "tests/AI4NGQuestionnaires.Tests/AI4NGQuestionnaires.Tests.csproj", 
    "tests/AI4NGResponses.Tests/AI4NGResponses.Tests.csproj",
    "tests/AI4NGExperimentManagementTests.Shared/AI4NGExperimentManagementTests.Shared.csproj"
)

# Integration test categories
$IntegrationTestCategories = @(
    "ControllerIntegrationTests",
    "RouteIntegrationTests", 
    "ServiceWiringIntegrationTests",
    "IntegrationTests"
)

$TotalTests = 0
$PassedTests = 0
$FailedTests = 0

try {
    Write-Host "`n📋 Test Summary:" -ForegroundColor Yellow
    
    foreach ($project in $TestProjects) {
        if (Test-Path $project) {
            Write-Host "`n🔍 Running tests in: $(Split-Path -Leaf $project)" -ForegroundColor Green
            
            $testArgs = @(
                "test", $project,
                "--configuration", $Configuration,
                "--logger", "console;verbosity=normal"
            )
            
            if ($Coverage) {
                $testArgs += @("--collect", "XPlat Code Coverage")
            }
            
            if ($Verbose) {
                $testArgs += @("--verbosity", "detailed")
            }
            
            # Filter for integration tests
            $testArgs += @("--filter", "Name~Integration")
            
            $result = & dotnet @testArgs
            $exitCode = $LASTEXITCODE
            
            if ($exitCode -eq 0) {
                Write-Host "✅ Tests passed for $project" -ForegroundColor Green
                $PassedTests++
            } else {
                Write-Host "❌ Tests failed for $project" -ForegroundColor Red
                $FailedTests++
            }
            
            $TotalTests++
        } else {
            Write-Warning "⚠️  Test project not found: $project"
        }
    }
    
    Write-Host "`n📊 Integration Test Results:" -ForegroundColor Cyan
    Write-Host "Total Projects: $TotalTests" -ForegroundColor Gray
    Write-Host "Passed: $PassedTests" -ForegroundColor Green
    Write-Host "Failed: $FailedTests" -ForegroundColor Red
    
    # Run specific integration test validation
    Write-Host "`n🔧 Running Controller Wiring Validation..." -ForegroundColor Yellow
    
    $validationTests = @(
        @{
            Name = "Experiments Controller Routes"
            Filter = "FullyQualifiedName~ExperimentsController"
        },
        @{
            Name = "Tasks Controller Routes" 
            Filter = "FullyQualifiedName~TasksController"
        },
        @{
            Name = "Questionnaires Controller Routes"
            Filter = "FullyQualifiedName~QuestionnairesController"
        },
        @{
            Name = "Responses Controller Routes"
            Filter = "FullyQualifiedName~ResponsesController"
        },
        @{
            Name = "Service Wiring"
            Filter = "FullyQualifiedName~ServiceWiring"
        }
    )
    
    foreach ($test in $validationTests) {
        Write-Host "  🧪 $($test.Name)..." -NoNewline
        
        $testResult = & dotnet test --configuration $Configuration --filter $test.Filter --logger "console;verbosity=quiet" 2>$null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host " ✅" -ForegroundColor Green
        } else {
            Write-Host " ❌" -ForegroundColor Red
        }
    }
    
    if ($FailedTests -eq 0) {
        Write-Host "`n🎉 All integration tests passed! Controllers and endpoints are properly wired." -ForegroundColor Green
        exit 0
    } else {
        Write-Host "`n💥 Some integration tests failed. Check the output above for details." -ForegroundColor Red
        exit 1
    }
    
} catch {
    Write-Error "❌ Error running integration tests: $_"
    exit 1
}