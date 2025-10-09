# AI4NG Question Engine - Testing Guide

## Local Testing

### Prerequisites
- Docker (for DynamoDB Local)
- AWS CLI configured
- SAM CLI installed
- .NET 8 SDK

### Quick Start
1. **Start local environment**:
   ```powershell
   .\debug-local.ps1
   ```

2. **Import Postman collection**: `postman-local-collection.json`
   - Set `baseUrl` variable to `http://localhost:3000`

3. **Test APIs** in order:
   - Create Questionnaire → Copy ID
   - Create Experiment → Copy experimentId 
   - Set `experimentId` variable in Postman
   - Create Response

### Debug Mode
```powershell
.\debug-local.ps1 -Debug
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
3. Use production Postman collection (includes auth)

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
3. Create responses → Test data collection
4. Verify data in local DynamoDB

### Cloud Validation Flow
1. Deploy changes
2. Run smoke tests with Postman
3. Verify CloudWatch logs
4. Check DynamoDB tables in AWS Console