# AI4NG Question Engine - Testing Guide

## Local Testing

### Prerequisites
- Docker (for DynamoDB Local)
- AWS CLI configured
- SAM CLI installed
- .NET 8 SDK

### Build Validation (Before Testing)
```powershell
# Quick build check (30 seconds)
.\scripts\validate-build.ps1

# Skip tests for faster validation
.\scripts\validate-build.ps1 -SkipTests

# Full CI pipeline validation
.\scripts\ci-pipeline.ps1
```

### Quick Start
1. **Start local environment**:
   ```powershell
   .\scripts\debug-local.ps1
   ```

2. **Import Postman collection**: `postman/AI4NG-QuestionEngine.postman_collection.json`
   - Set `baseUrl` to your local or cloud API URL
   - Set `jwt_token` if testing against cloud (Cognito JWT)

3. **Test APIs** in order:
   - Create Questionnaire → Copy ID
   - Create Experiment → Copy experimentId 
   - Set `experimentId` variable in Postman
   - Add/Remove Members to the experiment (optional)
   - Sync Experiment via GET `/api/experiments/{id}/sync?lastSyncTime=`
   - Create Response (note: body must use `data` wrapper)

### Debug Mode
```powershell
.\scripts\debug-local.ps1 -Debug
```
- Enables detailed logging
- Add `X-Debug: true` header to requests for extra logs
- Check console output for errors

### Manual DynamoDB Inspection
```bash
# List tables
aws dynamodb list-tables --endpoint-url http://localhost:8000

# Scan questionnaires
aws dynamodb scan --table-name questionnaires-local --endpoint-url http://localhost:8000
```

## Cloud Testing

### Deploy to AWS
```bash
sam build
sam deploy --guided
```

### Postman Setup for Cloud
1. Update `baseUrl` to your API Gateway URL
2. Add Authorization header: `Bearer <your-cognito-jwt-token>`
3. Use unified Postman collection (`postman/AI4NG-QuestionEngine.postman_collection.json`) with cloud baseUrl/auth

### Get Cognito Token
```bash
aws cognito-idp admin-initiate-auth \
  --user-pool-id <pool-id> \
  --client-id <client-id> \
  --auth-flow ADMIN_NO_SRP_AUTH \
  --auth-parameters USERNAME=<username>,PASSWORD=<password>
```

## Debugging Features

### Debug Headers
Add `X-Debug: true` to any request for detailed logging:
```json
{
  "X-Debug": "true",
  "Content-Type": "application/json"
}
```

### Log Levels
- **INFO**: Normal operations
- **DEBUG**: Detailed request/response data (when X-Debug header present)
- **ERROR**: Exceptions and failures

### Common Issues
- **401 Unauthorized**: Missing/invalid JWT token (cloud) or auth bypass not working (local)
- **403 Forbidden**: Researcher endpoints called without researcher path
- **404 Not Found**: Resource doesn't exist or wrong endpoint
- **500 Internal Error**: Check logs for DynamoDB connection issues

## Testing Patterns

### Local Development Flow
1. Create questionnaire → Test CRUD operations
2. Create experiment with questionnaire → Test experiment management  
3. Manage members → Test add/remove/get
4. Sync experiment via GET `/api/experiments/{id}/sync?lastSyncTime=`
5. Create responses → Test data collection (ensure body uses `data` wrapper to match API)
6. Verify data in local DynamoDB

### Pre-Deployment Validation
1. **Build validation**: `.\scripts\validate-build.ps1`
2. **Unit tests**: `.\scripts\run-tests.ps1 -Coverage`
3. **Integration tests**: `.\scripts\run-tests.ps1` (with DynamoDB Local)
4. **Full pipeline**: `.\scripts\ci-pipeline.ps1`

## Test Infrastructure Notes

- Shared helpers live in `AI4NGExperimentManagementTests.Shared`:
   - `ControllerTestBase<TController>` centralizes controller and auth setup
   - Use `CreateControllerWithMocks` for consistent controller/service mocks
- Prefer `[Theory]` with `[InlineData]` for found/not-found and invalid-input patterns
- Nullability in tests is intentional in some cases; we use null-forgiving (`!`) to keep analyzers quiet without altering behavior

### Cloud Validation Flow
1. Deploy changes
2. Run smoke tests with Postman
3. Verify CloudWatch logs
4. Check DynamoDB tables in AWS Console