# AI4NG Cloud Integration Harness

A small .NET 8 console application that exercises the AI4NG experiment-management API end-to-end. The harness runs a sequence of operations (create, read, update, delete) against the API to verify common flows for experiments, sessions, tasks, memberships and responses.

Files of interest

- `CloudHarness.cs` — the core test harness. Implements the sequence of API calls and assertions.
- `Program.cs` — bootstraps the harness, loads `appsettings.json`, optionally fetches Cognito tokens, and runs the harness.
- `appsettings.json` — configuration for the harness (API URL, Cognito settings, test users, questionnaire IDs).

## Overview of what the harness does

The harness performs the following high-level flow (detailed test list below):

- Optional: obtain researcher and participant JWTs via Cognito (if `UseCognitoAuth` is true in `appsettings.json`).
- Preflight check that the API is reachable.
- Create an experiment that references two questionnaire IDs from configuration.
- Validate access control: ensure a participant cannot see the experiment before membership is added, then add membership and re-check.
- Create a pool of tasks (questionnaires, training, cognitive tasks) and perform some task lifecycle checks (list, get, update).
- Create three session types and set task orders; verify session task order behavior.
- As participant, request a sync for the experiment and ensure sessions are present in the sync payload.
- Submit two responses (one for each questionnaire configured) and verify they can be listed and retrieved.
- Verify session task orders via GET endpoints.
- Clean up: delete created tasks, responses, and the experiment (researcher token required for deletes).

Configuration (`appsettings.json`)

Open `tools/CloudIntegrationHarness/appsettings.json`. Important keys:

- `ApiBaseUrl` (string) — base URL of the deployed API (example: `https://...execute-api.eu-west-2.amazonaws.com/dev`).
- `UseCognitoAuth` (bool) — if true, `Program.cs` will use `CognitoAuthClient` to request IdTokens for the researcher and participant using the credentials provided below. If false, you can modify `Program.cs` to set static tokens or implement another token provider.
- `CognitoClientId` (string) — the Cognito App Client ID used for InitiateAuth.
- `Researcher.Username` / `Researcher.Password` — credentials for the researcher user used to create experiments and manage membership.
- `Participant.Username` / `Participant.Password` / `Participant.Sub` — credentials and Cognito sub for the participant user. `Participant.Sub` is used when adding members by user sub.
- `QuestionnaireIdPQ` and `QuestionnaireIdATI` — identifiers of existing questionnaires in your environment used by the harness when creating experiment tasks and submitting responses.

Note: `Program.cs` loads this file using `HarnessConfig.Load()` and expects the above properties to exist. Keep secrets out of version control.

How authentication is handled

- If `UseCognitoAuth` is true, `Program.cs` uses `CognitoAuthClient.GetIdTokenAsync(...)` to call Cognito's `InitiateAuth` and extract the `IdToken` for both the researcher and participant. Those tokens are passed to `CloudHarness` and applied to the `HttpClient`'s `Authorization` header as needed.
- If you prefer to supply tokens manually, set `UseCognitoAuth` to `false` and modify `Program.cs` to inject tokens or change `CloudHarness.RunAsync` call to set a constant token.

Detailed list of tests the harness runs

Below is an itemized list you can use to check and fix the harness or the API when tests fail. Each item maps to code in `CloudHarness.cs` and the corresponding endpoint(s).

1) Preflight check
  - Purpose: confirm the API root/experiments endpoint is reachable.
  - Endpoint(s): GET `api/me/experiments` (sanity check).

2) Create experiment
  - Purpose: POST an experiment that references `QuestionnaireIdPQ` and `QuestionnaireIdATI`.
  - Endpoint(s): POST `api/experiments`
  - Assert: 2xx response, returned or accepted experiment id.

3) Negative membership visibility check
  - Purpose: ensure the participant cannot see the experiment before membership is added.
  - Endpoint(s): GET `api/me/experiments`
  - Assert: the experiment id is NOT present in the participant's experiments list.

4) Add member
  - Purpose: add the participant as a member of the experiment (requires researcher token).
  - Endpoint(s): PUT `api/experiments/{experimentId}/members/{participantSub}` with role/status body
  - Assert: 2xx response.

5) Positive membership visibility check
  - Purpose: verify the participant now sees the experiment.
  - Endpoint(s): GET `api/me/experiments`
  - Assert: the experiment id IS present in the participant's experiments list.

6) Create tasks (pool)
  - Purpose: create a variety of tasks (training, free_play, questionnaire, cognitive).
  - Endpoint(s): POST `api/tasks` (the harness uses `CreateTaskAsync` which posts to the API — check your API's task creation route if different)
  - Assert: 2xx and returned task ids for each created task.

7) List/Get/Update task sanity checks
  - Purpose: exercise listing, retrieval and update of a task.
  - Endpoint(s): GET `api/tasks`, GET `api/tasks/{id}`, PUT `api/tasks/{id}`
  - Assert: retrieval returns data, update returns success.

8) Create sessions (FIRST, WEEKLY, DAILY)
  - Purpose: create three sessions and store their ids.
  - Endpoint(s): POST `api/experiments/{experimentId}/sessions`
  - Assert: 2xx and a `sessionId` returned by the API.

9) Update session task orders
  - Purpose: update each session with an ordered list of `TASK#{taskId}` entries (5 tasks per session).
  - Endpoint(s): PUT `api/experiments/{experimentId}/sessions/{sessionId}`
  - Assert: 2xx.

10) Participant sync and basic sync validation
   - Purpose: as the participant, GET `api/experiments/{experimentId}/sync` and ensure sessions are included in the returned payload.
   - Endpoint(s): GET `api/experiments/{experimentId}/sync`
   - Assert: payload contains `sessions` (the harness checks for a few common shapes).

11) Submit responses
   - Purpose: POST two responses (one for each configured questionnaire id).
   - Endpoint(s): POST `api/responses`
   - Assert: 2xx and response ids returned.

12) Verify responses (list and get-by-id)
   - Purpose: GET `api/responses?experimentId={experimentId}&sessionId={sessionId}` and GET `api/responses/{id}` for each response.
   - Endpoint(s): GET `api/responses`, GET `api/responses/{id}`
   - Assert: responses are listed and individual GET returns 2xx.

13) Verify session task orders via GET
   - Purpose: GET sessions and ensure the `taskOrder` array matches the order set earlier.
   - Endpoint(s): GET `api/experiments/{experimentId}/sessions/{sessionId}`
   - Assert: `taskOrder` matches expected sequence; harness throws if mismatch.

14) Cleanup
   - Purpose: delete created tasks, delete responses, and delete the experiment using researcher credentials.
   - Endpoint(s): DELETE `api/tasks/{id}`, DELETE `api/responses/{id}`, DELETE `api/experiments/{experimentId}`
   - Assert: 2xx for deletes.

How to run

1. Edit `tools/CloudIntegrationHarness/appsettings.json` to point at your API and provide valid credentials or set `UseCognitoAuth` to `false` and provide tokens via code.
2. From the repository root run (PowerShell):

```powershell
dotnet run --project .\tools\CloudIntegrationHarness\CloudIntegrationHarness.csproj
```

Troubleshooting checklist when tests fail

- Inspect console output; the harness prints the HTTP method, endpoint, request body and response status for each request.
- Confirm `ApiBaseUrl` is correct and reachable from your machine.
- If using Cognito, verify `CognitoClientId`, usernames and passwords are correct and that the users exist and have the expected roles/permissions.
- Check that the API routes and response shapes in the harness match your API implementation (e.g., `sessionId` property name, `taskOrder` structure, response id field names).
- If an assertion throws (e.g., session task order mismatch), replicate the failing GET (URL printed in logs) using curl/Postman and inspect the exact JSON returned.
