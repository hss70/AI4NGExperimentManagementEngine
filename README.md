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

## Deployment and tooling overview

This repository uses AWS SAM as the primary deployment mechanism:
- Infrastructure-as-code lives in `infra/ExperimentManagement-template.yaml`.
- Environment and deploy defaults are configured via `samconfig.toml`.
- CI/CD pipelines reference the SAM template and package/deploy the three Lambdas (Experiments, Questionnaires, Responses) behind a shared HTTP API (API Gateway v2, payloadFormatVersion 2.0).

You may see different packaging artifacts in other AI4NG repos (for example, `AI4NGClassifierLambda` includes `serverless.template` and `aws-lambda-tools-defaults.json`, which are for the AWS .NET Lambda tooling or Serverless Application Model packaging used in that project). Those files are not required in this repo because:
- We already centralize infra/routing in the SAM template under `infra/`.
- The Lambda projects here are built and deployed as part of the SAM stack.
- Handlers are aligned to HTTP API v2 via `APIGatewayHttpApiV2ProxyFunction` and do not rely on the legacy serverless.template flow.

If you prefer local iteration with the AWS .NET Lambda tooling, you can add `aws-lambda-tools-defaults.json` per project as an optional convenience. However, the source of truth for deployment remains the SAM template, and the CI/CD workflow assumes SAM packaging/deployment.

### Quick commands
Optional, for local developers (these are examples; the CI pipeline runs equivalents):
- Build solution: `dotnet build AI4NGExperimentManagement.sln -c Debug`
- Package and deploy with SAM: `sam build` then `sam deploy` (uses `samconfig.toml`)


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

### Notable recent change
- Expanded QuestionnaireService test coverage (serialization round-trip for Scale, batch import flow, update expression verification). See `tests/AI4NGQuestionnaires.Tests/QuestionnaireServiceTests.cs`.

## Key Features
- **ASP.NET Core MVC Architecture** - Clean separation of concerns
- **Local Testing Support** - DynamoDB Local integration
- **Comprehensive Logging** - Debug headers and structured logging
- **Postman Collections** - Ready-to-use API testing
- **Batch Operations** - Bulk questionnaire uploads
- **Comprehensive Test Suite** - Unit tests, controller tests, DB schema compliance
- **Business Logic Validation** - Input validation, referential integrity, duplicate prevention
- **Audit Trail** - CreatedBy/UpdatedBy tracking for compliance

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