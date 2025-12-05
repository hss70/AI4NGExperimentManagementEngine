# AI4NG Cloud Integration Harness

A tiny .NET 8 console app to exercise the deployed API end-to-end:

- Create an experiment with known questionnaire IDs
- Add a participant member
- Submit two responses (PQ and ATI) for the session
- Verify listing and retrieval
- Clean up by deleting responses and the experiment

## Configure

Edit `appsettings.json`:

- `ApiBaseUrl`: Base URL of the deployed API (e.g., https://api.example.com)
- `JwtBearerToken`: A valid JWT with the Researcher role for mutating endpoints
- `TestUserSub`: Cognito `sub` for a test user to add to the experiment
- `QuestionnaireIdPQ` and `QuestionnaireIdATI`: IDs of existing questionnaires in your environment

### Get a JWT via Cognito

You can request a token using Cognito's InitiateAuth endpoint. Example:

```bash
curl --location 'https://cognito-idp.eu-west-2.amazonaws.com' \
  --header 'Content-Type: application/x-amz-json-1.1' \
  --header 'X-Amz-Target: AWSCognitoIdentityProviderService.InitiateAuth' \
  --data '{
    "AuthFlow": "USER_PASSWORD_AUTH",
    "ClientId": "517s6c84jo5i3lqste5idb0o4c",
    "AuthParameters": {
        "USERNAME": "<your-username>",
        "PASSWORD": "<your-password>"
    }
  }'
```

Extract `AuthenticationResult.IdToken` from the response and paste it into `JwtBearerToken`.

## Run

From the repo root:

```powershell
# Build and run (Windows PowerShell)
dotnet run --project .\tools\CloudIntegrationHarness\CloudIntegrationHarness.csproj
```

If it fails, the harness prints error details. All created objects are deleted at the end if the flow succeeds.

## Notes

- The harness uses only the API surface; it does not talk to AWS services directly.
- Keep the token and base URL secure; do not commit secrets.