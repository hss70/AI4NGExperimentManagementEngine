# Integration Tests for AI4NG Experiment Management

This document describes the comprehensive integration tests added to ensure all controllers and endpoints are properly wired up and functioning correctly.

## Overview

The integration tests cover three main areas:
1. **Controller Integration Tests** - Test HTTP endpoint behavior and responses
2. **Route Integration Tests** - Validate routing attributes and method signatures  
3. **Service Wiring Tests** - Ensure dependency injection is configured correctly

## Test Structure

### Controller Integration Tests

Located in each test project:
- `AI4NGExperiments.Tests/ControllerIntegrationTests.cs`
- `AI4NGQuestionnaires.Tests/ControllerIntegrationTests.cs` 
- `AI4NGResponses.Tests/ControllerIntegrationTests.cs`

These tests verify:
- ✅ All HTTP methods return correct response types
- ✅ Error handling works properly
- ✅ Authentication/authorization is enforced
- ✅ Request/response models are handled correctly
- ✅ Exception scenarios return appropriate HTTP status codes

### Route Integration Tests

Located in `AI4NGExperimentManagementTests.Shared/RouteIntegrationTests.cs`

These tests verify:
- ✅ Controller route attributes are correctly configured
- ✅ HTTP method attributes are present on all endpoints
- ✅ Controllers inherit from BaseApiController
- ✅ Required CRUD methods exist on all controllers
- ✅ Specialized endpoints exist (e.g., batch operations, sync)

### Service Wiring Integration Tests

Located in `AI4NGExperimentManagementTests.Shared/ServiceWiringIntegrationTests.cs`

These tests verify:
- ✅ All services are registered in dependency injection
- ✅ Service lifetimes are configured correctly (Scoped)
- ✅ No circular dependencies exist
- ✅ Services can be resolved without errors
- ✅ Startup classes inherit from BaseStartup

## Endpoints Covered

### Experiments Controller (`/api/experiments`)
- `GET /api/experiments` - Get all experiments (researcher only)
- `GET /api/experiments/{id}` - Get experiment by ID (researcher only)  
- `GET /api/me/experiments` - Get user's experiments (participant)
- `POST /api/experiments` - Create experiment (researcher only)
- `PUT /api/experiments/{id}` - Update experiment (researcher only)
- `DELETE /api/experiments/{id}` - Delete experiment (researcher only)
- `GET /api/experiments/{id}/sync` - Sync experiment data
- `GET /api/experiments/{id}/members` - Get experiment members (researcher only)
- `PUT /api/experiments/{id}/members/{userSub}` - Add member (researcher only)
- `DELETE /api/experiments/{id}/members/{userSub}` - Remove member (researcher only)
- `GET /api/experiments/{id}/sessions` - Get sessions (researcher only)
- `GET /api/experiments/{id}/sessions/{sessionId}` - Get session (researcher only)
- `POST /api/experiments/{id}/sessions` - Create session (researcher only)
- `PUT /api/experiments/{id}/sessions/{sessionId}` - Update session (researcher only)
- `DELETE /api/experiments/{id}/sessions/{sessionId}` - Delete session (researcher only)

### Tasks Controller (`/api/tasks`)
- `GET /api/tasks` - Get all tasks (researcher only)
- `GET /api/tasks/{id}` - Get task by ID (researcher only)
- `POST /api/tasks` - Create task (researcher only)
- `PUT /api/tasks/{id}` - Update task (researcher only)
- `DELETE /api/tasks/{id}` - Delete task (researcher only)

### Questionnaires Controller (`/api/questionnaires`)
- `GET /api/questionnaires` - Get all questionnaires
- `GET /api/questionnaires/{id}` - Get questionnaire by ID
- `POST /api/questionnaires` - Create questionnaire (researcher only)
- `PUT /api/questionnaires/{id}` - Update questionnaire (researcher only)
- `DELETE /api/questionnaires/{id}` - Delete questionnaire (researcher only)
- `POST /api/questionnaires/batch` - Batch create questionnaires (researcher only)

### Responses Controller (`/api/responses`)
- `GET /api/responses` - Get responses (with optional filters)
- `GET /api/responses/{id}` - Get response by ID
- `POST /api/responses` - Create response
- `PUT /api/responses/{id}` - Update response
- `DELETE /api/responses/{id}` - Delete response

## Running the Tests

### Individual Test Projects
```bash
# Run experiments tests
dotnet test tests/AI4NGExperiments.Tests/

# Run questionnaires tests  
dotnet test tests/AI4NGQuestionnaires.Tests/

# Run responses tests
dotnet test tests/AI4NGResponses.Tests/

# Run shared tests
dotnet test tests/AI4NGExperimentManagementTests.Shared/
```

### Integration Tests Only
```bash
# Run only integration tests across all projects
dotnet test --filter "Name~Integration"
```

### Using the PowerShell Script
```powershell
# Run all integration tests with summary
./scripts/run-integration-tests.ps1

# Run with verbose output
./scripts/run-integration-tests.ps1 -Verbose

# Run with code coverage
./scripts/run-integration-tests.ps1 -Coverage
```

## Test Coverage

The integration tests provide comprehensive coverage of:

### ✅ HTTP Routing
- All endpoints are accessible via correct HTTP methods
- Route parameters are properly bound
- Query parameters are handled correctly

### ✅ Authentication & Authorization  
- Researcher-only endpoints reject non-researcher users
- User context is properly extracted from requests
- Authentication service integration works

### ✅ Request/Response Handling
- JSON serialization/deserialization works
- Model validation is applied
- Error responses have correct format

### ✅ Service Integration
- Controllers properly inject and use services
- Service methods are called with correct parameters
- Service exceptions are handled gracefully

### ✅ Dependency Injection
- All services are registered correctly
- Service lifetimes are appropriate
- No circular dependencies exist

## Key Features Tested

1. **CRUD Operations** - All controllers support Create, Read, Update, Delete
2. **Authentication** - Researcher vs participant access control
3. **Error Handling** - Proper HTTP status codes and error messages
4. **Specialized Endpoints** - Sync, batch operations, member management
5. **Query Filtering** - Response filtering by experiment/session
6. **Model Validation** - Request model validation and error responses

## Benefits

These integration tests ensure:
- 🔒 **Reliability** - Endpoints work as expected under various conditions
- 🚀 **Confidence** - Safe to deploy knowing all wiring is correct
- 🐛 **Early Detection** - Catch configuration issues before deployment
- 📋 **Documentation** - Tests serve as living documentation of API behavior
- 🔄 **Regression Prevention** - Prevent breaking changes to existing functionality

## Maintenance

When adding new endpoints:
1. Add controller integration tests for the new endpoint
2. Update route integration tests if new routing patterns are used
3. Update service wiring tests if new services are added
4. Run the integration test script to verify everything works

The tests are designed to be maintainable and will catch issues early in the development cycle.