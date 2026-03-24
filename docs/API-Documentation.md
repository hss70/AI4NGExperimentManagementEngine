# AI4NG API Documentation (Current Snapshot)

Last updated: 2026-03-17
Source of truth: controller routes in `src/*/Controllers` and API Gateway routes in `infra/ExperimentManagement-template.yaml`.

## Base URL
- `https://{api-id}.execute-api.eu-west-2.amazonaws.com/{stage}`

## Authentication
- Header: `Authorization: Bearer <jwt>`
- Researcher-only endpoints call `RequireResearcher()`.
- Participant endpoints derive identity from JWT `sub` (`GetAuthenticatedUserSub()`).

## Implemented Endpoints (Controllers + Infra)

### Experiments (researcher)
- `GET /api/experiments`
- `GET /api/experiments/{experimentId}`
- `POST /api/experiments`
- `PUT /api/experiments/{experimentId}`
- `DELETE /api/experiments/{experimentId}`
- `POST /api/experiments/{experimentId}/activate`
- `POST /api/experiments/{experimentId}/pause`
- `POST /api/experiments/{experimentId}/close`

### Protocol sessions (researcher)
- `GET /api/experiments/{experimentId}/protocol-sessions`
- `GET /api/experiments/{experimentId}/protocol-sessions/{protocolSessionKey}`
- `POST /api/experiments/{experimentId}/protocol-sessions/{protocolSessionKey}`
- `PUT /api/experiments/{experimentId}/protocol-sessions/{protocolSessionKey}`
- `DELETE /api/experiments/{experimentId}/protocol-sessions/{protocolSessionKey}`

### Tasks (researcher)
- `GET /api/tasks`
- `GET /api/tasks/{taskKey}`
- `POST /api/tasks`
- `POST /api/tasks/batch`
- `PUT /api/tasks/{taskKey}`
- `DELETE /api/tasks/{taskKey}`

### Experiment participants (researcher)
- `GET /api/experiments/{experimentId}/participants`
- `GET /api/experiments/{experimentId}/participants/{participantId}`
- `PUT /api/experiments/{experimentId}/participants/{participantId}`
- `POST /api/experiments/{experimentId}/participants/batch`
- `DELETE /api/experiments/{experimentId}/participants/{participantId}`
  - Returns `204 No Content` from controller.

### Researcher user lookup
- `GET /api/researcher/users/by-email?email=...`
- `GET /api/researcher/users/by-username?username=...`
- Note: current controller intentionally throws `InvalidOperationException` for both (VPC/Cognito lookup restriction).

### Participant experiments
- `GET /api/me/experiments`
- `GET /api/me/experiments/{experimentId}/bundle?since={ISO-8601 optional}`

### Participant occurrences
- `GET /api/me/experiments/{experimentId}/occurrences?from={optional}&to={optional}`
- `GET /api/me/experiments/{experimentId}/occurrences/{occurrenceKey}`
- `GET /api/me/experiments/{experimentId}/occurrences/current`
- `POST /api/me/experiments/{experimentId}/occurrences`
- `POST /api/me/experiments/{experimentId}/occurrences/{occurrenceKey}/start`
- `POST /api/me/experiments/{experimentId}/occurrences/{occurrenceKey}/complete`
- `POST /api/me/experiments/{experimentId}/occurrences/{occurrenceKey}/tasks/{taskKey}/responses`

Request body for task response submission:
```json
{
  "clientSubmissionId": "f51aa6df-c020-4e93-bc06-f046a665a157",
  "clientSubmittedAt": "2026-03-17T09:30:00Z",
  "questionnaireId": "mood-v1",
  "payload": {
    "answer": 4,
    "notes": "felt focused"
  }
}
```

## Implemented in Controllers but NOT routed in Infra
These controller endpoints exist but are currently missing corresponding `RouteKey` entries in `infra/ExperimentManagement-template.yaml`.
- None identified.

## Routed in Infra but NOT implemented in current controllers
These routes are defined in infra but do not currently have matching controller actions in this repo state.
- None identified.

## Questionnaires API
Controller currently exposes:
- `GET /api/questionnaires`
- `GET /api/questionnaires/{id}`
- `POST /api/questionnaires/by-ids`
- `POST /api/questionnaires`
- `PUT /api/questionnaires/{id}`
- `DELETE /api/questionnaires/{id}`
- `POST /api/questionnaires/batch`

These are routed in infra to `QuestionnairesApi`.

## Legacy/Separate responses API
No `/api/responses/*` routes are currently declared in infra template.
If `AI4NGResponsesLambda` is still used in another stack, document that separately from this template.
