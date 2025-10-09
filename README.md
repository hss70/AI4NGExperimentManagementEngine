# AI4NG Experiment Management

## Project Structure

```
AI4NGExperimentManagement/
├── src/                           # Lambda function source code
│   ├── AI4NGQuestionnairesLambda/ # Questionnaire management API
│   ├── AI4NGExperimentsLambda/    # Experiment management API  
│   └── AI4NGResponsesLambda/      # Response collection API
├── infra/                         # CloudFormation templates
├── scripts/                       # PowerShell automation scripts
├── docs/                          # Documentation
├── postman/                       # Postman collections for testing
├── local-testing/                 # Local development configuration
└── questionnaires-import/         # Sample questionnaire data
```

## Quick Start

### Local Development
```powershell
# Start local environment
.\scripts\debug-local.ps1

# Import Postman collection
# File: postman/postman-local-collection.json
```

### Deploy to AWS
```bash
sam build
sam deploy --guided
```

## Documentation
- [Testing Guide](docs/README-Testing.md) - Local and cloud testing instructions
- [Testing Documentation](docs/Testing-Documentation.md) - Comprehensive testing framework guide
- [Test Cases](docs/Test-Cases.md) - Complete test case specifications
- [Test Source of Truth](docs/Test-Source-Of-Truth.md) - Business rules and expected behaviors
- [Scripts Documentation](docs/Scripts-Documentation.md) - PowerShell automation scripts guide

## Key Features
- **ASP.NET Core MVC Architecture** - Clean separation of concerns
- **Local Testing Support** - DynamoDB Local integration
- **Comprehensive Logging** - Debug headers and structured logging
- **Postman Collections** - Ready-to-use API testing
- **Batch Operations** - Bulk questionnaire uploads
- **Unit & Integration Tests** - No deployment required

## Build Validation

### Quick Build Check
```powershell
# Validate builds without deployment (30 seconds)
.\scripts\validate-build.ps1

# Skip tests for faster validation
.\scripts\validate-build.ps1 -SkipTests

# Full CI/CD pipeline validation
.\scripts\ci-pipeline.ps1
```

## Testing

### Unit Tests
```powershell
# Run all tests
.\scripts\run-tests.ps1

# Run specific project tests
.\scripts\run-tests.ps1 -Project Questionnaires

# Run with coverage
.\scripts\run-tests.ps1 -Coverage
```

### Integration Tests
```powershell
# Start DynamoDB Local first
docker run -p 8000:8000 amazon/dynamodb-local

# Run integration tests
.\scripts\run-tests.ps1
```