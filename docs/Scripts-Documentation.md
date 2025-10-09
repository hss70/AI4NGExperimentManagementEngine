# AI4NG Question Engine - Scripts Documentation

## Overview

This document describes all PowerShell scripts available in the `scripts/` folder for development, testing, and deployment automation.

## Development Scripts

### debug-local.ps1
**Purpose**: Start local development environment with debugging support

**Usage**:
```powershell
.\scripts\debug-local.ps1 [-Debug] [-Service <service>]
```

**Parameters**:
- `-Debug`: Enable debug mode with detailed logging
- `-Service`: Target specific service (questionnaires, experiments, responses, all)

**What it does**:
1. Starts DynamoDB Local (Docker container)
2. Creates local test tables
3. Starts SAM Local API on port 3000
4. Configures environment for local testing

**Example**:
```powershell
# Start in debug mode
.\scripts\debug-local.ps1 -Debug

# Start specific service
.\scripts\debug-local.ps1 -Service questionnaires
```

### test-local.ps1
**Purpose**: Quick local testing without debug overhead

**Usage**:
```powershell
.\scripts\test-local.ps1 [-Service <service>]
```

**What it does**:
1. Sets up local DynamoDB tables
2. Starts SAM Local API
3. Provides different ports for different services

### test-api.ps1
**Purpose**: Automated API testing script

**Usage**:
```powershell
.\scripts\test-api.ps1 [-BaseUrl <url>]
```

**Parameters**:
- `-BaseUrl`: API endpoint URL (default: http://localhost:3000)

**Test Flow**:
1. Creates test questionnaire
2. Creates test experiment
3. Creates test response
4. Validates all operations

## Build Validation Scripts

### validate-build.ps1
**Purpose**: Validate API builds without deployment

**Usage**:
```powershell
.\scripts\validate-build.ps1 [-SkipTests] [-Verbose]
```

**Parameters**:
- `-SkipTests`: Skip test compilation and execution for faster validation
- `-Verbose`: Enable detailed build output

**What it does**:
1. Restores NuGet packages for all projects
2. Compiles .NET Lambda projects
3. Validates SAM build process
4. Validates CloudFormation template syntax
5. Compiles and runs unit tests (unless skipped)

**Examples**:
```powershell
# Quick build validation (30 seconds)
.\scripts\validate-build.ps1

# Skip tests for faster validation
.\scripts\validate-build.ps1 -SkipTests

# Verbose output for debugging
.\scripts\validate-build.ps1 -Verbose
```

### ci-pipeline.ps1
**Purpose**: Complete CI/CD pipeline validation

**Usage**:
```powershell
.\scripts\ci-pipeline.ps1 [-Environment <env>] [-DeploymentTest]
```

**Parameters**:
- `-Environment`: Target environment (default: dev)
- `-DeploymentTest`: Include actual deployment test (requires AWS credentials)

**Pipeline Stages**:
1. Build validation
2. Unit tests with coverage
3. Integration tests (with DynamoDB Local)
4. SAM packaging test
5. Optional deployment test

**Example**:
```powershell
# Full pipeline validation
.\scripts\ci-pipeline.ps1

# Include deployment test
.\scripts\ci-pipeline.ps1 -DeploymentTest
```

## Testing Scripts

### run-tests.ps1
**Purpose**: Execute unit and integration tests

**Usage**:
```powershell
.\scripts\run-tests.ps1 [-Project <name>] [-Coverage] [-Watch]
```

**Parameters**:
- `-Project`: Run specific test project (Questionnaires, Experiments, Responses, all)
- `-Coverage`: Generate code coverage reports
- `-Watch`: Run in watch mode for continuous testing

**Examples**:
```powershell
# Run all tests
.\scripts\run-tests.ps1

# Run with coverage
.\scripts\run-tests.ps1 -Coverage

# Run specific project tests
.\scripts\run-tests.ps1 -Project Questionnaires

# Watch mode for TDD
.\scripts\run-tests.ps1 -Watch
```

**Output**:
- Test results in console
- Coverage reports in `TestResults/` folders
- Exit code 0 for success, 1 for failure

## Data Management Scripts

### upload-questionnaires.ps1
**Purpose**: Batch upload questionnaires from JSON files

**Usage**:
```powershell
.\scripts\upload-questionnaires.ps1 [-ApiUrl <url>] [-InputFolder <path>]
```

**Parameters**:
- `-ApiUrl`: Target API endpoint
- `-InputFolder`: Folder containing questionnaire JSON files

**What it does**:
1. Scans input folder for .json files
2. Validates questionnaire format
3. Uploads via batch API endpoint
4. Reports success/failure for each file

**Example**:
```powershell
# Upload to local environment
.\scripts\upload-questionnaires.ps1 -ApiUrl "http://localhost:3000" -InputFolder "questionnaires-import"

# Upload to production
.\scripts\upload-questionnaires.ps1 -ApiUrl "https://api.example.com" -InputFolder "questionnaires-import"
```

## Script Dependencies

### Required Tools
- **PowerShell 5.1+**: Script execution
- **Docker**: DynamoDB Local container
- **AWS CLI**: DynamoDB table operations
- **SAM CLI**: Local Lambda execution
- **.NET 8 SDK**: Test execution

### Environment Variables
Scripts automatically set these for local testing:
```powershell
$env:QUESTIONNAIRES_TABLE = "questionnaires-local"
$env:EXPERIMENTS_TABLE = "experiments-local"  
$env:RESPONSES_TABLE = "responses-local"
$env:AWS_DEFAULT_REGION = "eu-west-2"
```

## Configuration Files

### local-testing/local-env.json
Environment variables for SAM Local:
```json
{
  "AI4NGQuestionnairesFunction": {
    "QUESTIONNAIRES_TABLE": "questionnaires-local",
    "AWS_ENDPOINT_URL": "http://host.docker.internal:8000"
  }
}
```

### postman/ Collections
- `postman-local-collection.json`: Local testing collection
- `postman-cloud-collection.json`: Production testing collection

## Common Workflows

### Local Development Setup
```powershell
# 1. Start local environment
.\scripts\debug-local.ps1

# 2. Run tests to verify setup
.\scripts\run-tests.ps1

# 3. Upload sample data
.\scripts\upload-questionnaires.ps1 -ApiUrl "http://localhost:3000"

# 4. Test APIs manually
.\scripts\test-api.ps1
```

### Build Validation Workflow
```powershell
# 1. Quick build check before committing
.\scripts\validate-build.ps1

# 2. Full pipeline validation
.\scripts\ci-pipeline.ps1

# 3. Deploy if validation passes
sam build && sam deploy
```

### Continuous Integration
```powershell
# 1. Validate builds first
.\scripts\validate-build.ps1

# 2. Run all tests with coverage
.\scripts\run-tests.ps1 -Coverage

# 3. Validate test results
if ($LASTEXITCODE -ne 0) { exit 1 }

# 4. Build and deploy (if tests pass)
sam build && sam deploy
```

### Production Deployment Testing
```powershell
# 1. Deploy to staging
sam deploy --parameter-overrides Environment=staging

# 2. Run API tests against staging
.\scripts\test-api.ps1 -BaseUrl "https://staging-api.example.com"

# 3. Upload production questionnaires
.\scripts\upload-questionnaires.ps1 -ApiUrl "https://staging-api.example.com"
```

## Troubleshooting

### Common Issues

#### DynamoDB Local Not Starting
```powershell
# Check if port 8000 is in use
netstat -an | findstr :8000

# Stop existing container
docker stop dynamodb-local
docker rm dynamodb-local
```

#### SAM Local Port Conflicts
```powershell
# Check port usage
netstat -an | findstr :3000

# Use different port
sam local start-api --port 3001
```

#### Test Failures
```powershell
# Run with verbose output
.\scripts\run-tests.ps1 -Project Questionnaires --verbosity diagnostic

# Check test logs
Get-Content tests/*/TestResults/*/coverage.cobertura.xml
```

#### Permission Issues
```powershell
# Run as administrator if needed
Start-Process PowerShell -Verb RunAs

# Check execution policy
Get-ExecutionPolicy
Set-ExecutionPolicy RemoteSigned -Scope CurrentUser
```

## Script Maintenance

### Adding New Scripts
1. Place in `scripts/` folder
2. Follow naming convention: `verb-noun.ps1`
3. Include parameter validation
4. Add error handling with `$ErrorActionPreference = "Stop"`
5. Document in this file

### Best Practices
- Use `Write-Host` with colors for user feedback
- Include help comments at script top
- Validate prerequisites before execution
- Provide meaningful error messages
- Support common parameters (-Verbose, -WhatIf where applicable)