# AI4NG Question Engine - Testing Documentation

## Overview

This document provides comprehensive testing documentation for the AI4NG Question Engine, including unit tests, integration tests, and test case specifications.

## Test Architecture

### Test Projects Structure
```
tests/
├── AI4NGQuestionnaires.Tests/    # Questionnaire API tests
├── AI4NGExperiments.Tests/       # Experiment API tests  
└── AI4NGResponses.Tests/          # Response API tests
```

### Test Types

#### 1. Unit Tests
- **Purpose**: Test individual components in isolation
- **Dependencies**: Mocked using Moq framework
- **Speed**: Fast (< 1 second per test)
- **Coverage**: Business logic, validation, error handling
- **Status**: ✅ Implemented with comprehensive validation

#### 2. Controller Tests
- **Purpose**: Test HTTP behavior and routing
- **Dependencies**: Mocked services
- **Speed**: Fast (< 1 second per test)
- **Coverage**: Request/response handling, authorization
- **Status**: ✅ Implemented for all controllers

#### 3. DB Schema Compliance Tests
- **Purpose**: Validate complete DB Design specification adherence
- **Dependencies**: Mocked DynamoDB client
- **Speed**: Fast (< 1 second per test)
- **Coverage**: GSI structure, syncMetadata, audit fields
- **Status**: ✅ Implemented

## Test Execution

### Running Tests

```powershell
# Run all tests
.\scripts\run-tests.ps1

# Run specific project
.\scripts\run-tests.ps1 -Project Questionnaires

# Run with code coverage
.\scripts\run-tests.ps1 -Coverage

# Watch mode for continuous testing
.\scripts\run-tests.ps1 -Watch
```

### Prerequisites for Integration Tests

```bash
# Start DynamoDB Local
docker run -p 8000:8000 amazon/dynamodb-local

# Create test tables (automatic in test setup)
```

## Test Framework Stack

- **xUnit**: Test framework
- **Moq**: Mocking framework  
- **Microsoft.AspNetCore.Mvc.Testing**: Integration testing
- **Amazon.Lambda.TestUtilities**: Lambda-specific testing utilities

## Environment Setup

### Test Environment Variables
```json
{
  "AWS_ENDPOINT_URL": "http://localhost:8000",
  "QUESTIONNAIRES_TABLE": "questionnaires-test",
  "EXPERIMENTS_TABLE": "experiments-test", 
  "RESPONSES_TABLE": "responses-test"
}
```

### Local Testing Configuration
- **Authentication**: Bypassed (returns "testuser")
- **DynamoDB**: Local instance on port 8000
- **Logging**: Debug mode enabled automatically

## Continuous Integration

### GitHub Actions Integration
```yaml
# Add to .github/workflows/test.yml
- name: Run Tests
  run: |
    docker run -d -p 8000:8000 amazon/dynamodb-local
    dotnet test --configuration Release --collect:"XPlat Code Coverage"
```

### Test Reports
- **Coverage**: Generated in `TestResults/` folders
- **Format**: Cobertura XML for CI integration
- **Threshold**: Aim for >80% code coverage

## Best Practices

### Test Naming Convention
```csharp
[Fact]
public async Task MethodName_ShouldExpectedBehavior_WhenCondition()
```

### Test Organization
- **Arrange**: Setup test data and mocks
- **Act**: Execute the method under test  
- **Assert**: Verify expected outcomes

### Mock Setup
```csharp
_mockDynamoClient.Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), default))
    .ReturnsAsync(new PutItemResponse());
```

## Debugging Tests

### Visual Studio
1. Set breakpoints in test methods
2. Right-click test → Debug Test(s)
3. Step through code execution

### VS Code  
1. Install C# extension
2. Use "Run and Debug" panel
3. Select ".NET Core Launch (console)" configuration

### Command Line
```powershell
# Run specific test with detailed output
dotnet test --filter "TestMethodName" --verbosity diagnostic
```