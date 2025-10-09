# Build validation script - checks compilation without deployment
param(
    [switch]$SkipTests = $false,
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"

Write-Host "Building and validating APIs..." -ForegroundColor Cyan

$projects = @(
    "src/AI4NGExperimentManagement.Shared",
    "src/AI4NGQuestionnairesLambda",
    "src/AI4NGExperimentsLambda", 
    "src/AI4NGResponsesLambda"
)

$testProjects = @(
    "tests/AI4NGQuestionnaires.Tests",
    "tests/AI4NGExperiments.Tests",
    "tests/AI4NGResponses.Tests"
)

# 1. Restore packages
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
foreach ($proj in $projects) {
    if (Test-Path $proj) {
        Write-Host "  Restoring $proj"
        dotnet restore $proj
        if ($LASTEXITCODE -ne 0) { throw "Package restore failed for $proj" }
    }
}

# 2. Build all projects
Write-Host "Building projects..." -ForegroundColor Yellow
foreach ($proj in $projects) {
    if (Test-Path $proj) {
        Write-Host "  Building $proj"
        $buildArgs = @("build", $proj, "--configuration", "Release", "--no-restore")
        if ($Verbose) { $buildArgs += "--verbosity", "detailed" }
        
        & dotnet @buildArgs
        if ($LASTEXITCODE -ne 0) { throw "Build failed for $proj" }
    }
}

# 3. SAM build validation
Write-Host "Validating SAM build..." -ForegroundColor Yellow
sam build --template-file infra/ExperimentManagement-template.yaml
if ($LASTEXITCODE -ne 0) { throw "SAM build failed" }


# 4. Build test projects
if (-not $SkipTests) {
    Write-Host "Building test projects..." -ForegroundColor Yellow
    foreach ($proj in $testProjects) {
        if (Test-Path $proj) {
            Write-Host "  Building $proj"
            dotnet build $proj --configuration Release --no-restore
            if ($LASTEXITCODE -ne 0) { throw "Test build failed for $proj" }
        }
    }
    
    # 5. Run compilation tests (fast)
    Write-Host "Running compilation tests..." -ForegroundColor Yellow
    dotnet test --configuration Release --no-build --filter "Category!=Integration"
    if ($LASTEXITCODE -ne 0) { throw "Unit tests failed" }
}

# 6. CloudFormation template validation
Write-Host "Validating CloudFormation template..." -ForegroundColor Yellow
aws cloudformation validate-template --template-body file://infra/ExperimentManagement-template.yaml
if ($LASTEXITCODE -ne 0) { throw "CloudFormation template validation failed" }

Write-Host "All builds validated successfully!" -ForegroundColor Green
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  - NuGet packages restored" -ForegroundColor Green
Write-Host "  - .NET projects compiled" -ForegroundColor Green  
Write-Host "  - SAM build successful" -ForegroundColor Green
if (-not $SkipTests) {
    Write-Host "  - Test projects compiled" -ForegroundColor Green
    Write-Host "  - Unit tests passed" -ForegroundColor Green
}
Write-Host "  - CloudFormation template valid" -ForegroundColor Green