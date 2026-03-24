# Postman Manual Requests Guide

This is a manual testing guide (not a Postman collection) based on the current controllers and service behavior.

## Base URLs

- `{{experiments_api}}` = `http://localhost:5050` (run `AI4NGExperimentsLambda`)
- `{{questionnaires_api}}` = `http://localhost:5050` (run `AI4NGQuestionnairesLambda`)
- `{{responses_api}}` = `http://localhost:5050` (run `AI4NGResponsesLambda`)

Note: each project is configured to run on port `5050`, so run one API at a time locally.

## Auth and expected status mapping

- Use `Authorization: Bearer <jwt>` for secured endpoints.
- Researcher-only endpoints require the `Researcher` group.
- Common error mapping:
  - `400`: argument/validation error
  - `401`: missing/invalid auth user
  - `403`: role forbidden
  - `404`: not found
  - `409`: conflict/conditional check failure

---

## Experiments API (`{{experiments_api}}/api/experiments`)

### 1) List experiments
- Method: `GET`
- URL: `/api/experiments`
- Expected: `200 OK`, array of experiments.

### 2) Get experiment by id
- Method: `GET`
- URL: `/api/experiments/{experimentId}`
- Expected:
  - existing id: `200 OK`
  - unknown id: `404 Not Found` (`Experiment not found`)
  - invalid id (empty/whitespace): `400 Bad Request`

### 3) Create experiment
- Method: `POST`
- URL: `/api/experiments`
- Body:
```json
{
  "id": "exp-manual-001",
  "data": {
    "name": "Cognitive Study A",
    "description": "Pilot study",
    "studyStartDate": "2026-03-15",
    "studyEndDate": "2026-06-15",
    "participantDurationDays": 56,
    "sessionTypes": {
      "FIRST": {
        "name": "Baseline",
        "description": "Initial setup",
        "tasks": ["TRAIN_EEG"],
        "estimatedDurationMinutes": 20,
        "schedule": "ONCE"
      },
      "DAILY": {
        "name": "Daily Check",
        "description": "Daily protocol",
        "tasks": ["MOOD_Q"],
        "estimatedDurationMinutes": 10,
        "schedule": "DAILY"
      }
    }
  }
}
```
- Expected:
  - valid: `200 OK` with `{ "id": "..." }`
  - invalid `name`/`description`: `400`
  - invalid dates: `400`
  - `participantDurationDays <= 0`: `400`

### 4) Update experiment
- Method: `PUT`
- URL: `/api/experiments/{experimentId}`
- Body:
```json
{
  "data": {
    "description": "Updated description",
    "participantDurationDays": 60
  }
}
```
- Expected:
  - valid existing experiment: `200 OK`
  - missing experiment: `404`
  - invalid payload values: `400`

### 5) Delete experiment
- Method: `DELETE`
- URL: `/api/experiments/{experimentId}`
- Expected:
  - valid existing id: `200 OK`
  - invalid id: `400`

### 6) Status transitions
- Activate: `POST /api/experiments/{experimentId}/activate`
  - allowed from `Draft` or `Paused`: `200`
  - otherwise: `409`
- Pause: `POST /api/experiments/{experimentId}/pause`
  - allowed from `Active`: `200`
  - otherwise: `409`
- Close: `POST /api/experiments/{experimentId}/close`
  - allowed from `Active` or `Paused`: `200`
  - otherwise: `409`

---

## Protocol Sessions (`{{experiments_api}}/api/experiments/{experimentId}/protocol-sessions`)

### 1) List protocol sessions
- Method: `GET`
- URL: `/api/experiments/{experimentId}/protocol-sessions`
- Expected: `200 OK` (ordered by `order`).

### 2) Get protocol session by key
- Method: `GET`
- URL: `/api/experiments/{experimentId}/protocol-sessions/{protocolSessionKey}`
- Expected:
  - found: `200`
  - not found: `404` (`Session protocol not found`)

### 3) Create protocol session
- Method: `POST`
- URL: `/api/experiments/{experimentId}/protocol-sessions/{protocolSessionKey}`
- Body:
```json
{
  "sessionTypeKey": "DAILY",
  "order": 1,
  "cadenceType": "DAILY",
  "maxPerDay": 1,
  "windowStartLocal": "09:00",
  "windowEndLocal": "18:00",
  "weekday": null
}
```
- Expected:
  - valid: `201 Created`
  - invalid cadence (`ONCE|DAILY|WEEKLY|ADHOC` only): `400`
  - `maxPerDay <= 0`: `400`
  - `sessionTypeKey` not in experiment `data.sessionTypes`: `400`
  - existing same key: `409`

### 4) Update protocol session
- Method: `PUT`
- URL: `/api/experiments/{experimentId}/protocol-sessions/{protocolSessionKey}`
- Body: same as create
- Expected:
  - existing item: `200`
  - missing item: `409` (conditional failure)

### 5) Delete protocol session
- Method: `DELETE`
- URL: `/api/experiments/{experimentId}/protocol-sessions/{protocolSessionKey}`
- Expected:
  - existing item: `200`
  - missing item: `409`

---

## Tasks API (`{{experiments_api}}/api/tasks`)

### 1) List tasks
- Method: `GET`
- URL: `/api/tasks`
- Expected: `200 OK`

### 2) Get task by key
- Method: `GET`
- URL: `/api/tasks/{taskKey}`
- Expected:
  - found: `200`
  - not found: `404`

### 3) Create task
- Method: `POST`
- URL: `/api/tasks`
- Body:
```json
{
  "taskKey": "MOOD_Q",
  "data": {
    "name": "Mood Questionnaire",
    "type": "Questionnaire",
    "description": "Daily mood",
    "questionnaireIds": ["mood-v1"],
    "configuration": {},
    "estimatedDuration": 5
  }
}
```
- Expected:
  - valid: `201`
  - invalid `taskKey` format: `400` (must be `A-Z`, `0-9`, `_`, length 3-64)
  - invalid type/use of questionnaire ids: `400`
  - missing referenced questionnaire id: `400`

### 4) Update task
- Method: `PUT`
- URL: `/api/tasks/{taskKey}`
- Body (TaskData only):
```json
{
  "name": "Mood Questionnaire Updated",
  "type": "Questionnaire",
  "description": "Daily mood update",
  "questionnaireIds": ["mood-v1"],
  "configuration": {},
  "estimatedDuration": 6
}
```
- Expected:
  - existing: `200`
  - missing/deleted: `409`

### 5) Delete task
- Method: `DELETE`
- URL: `/api/tasks/{taskKey}`
- Expected:
  - existing: `200`
  - missing: `409`

---

## Researcher User Lookup (`{{experiments_api}}/api/researcher/users`)

### 1) Get by email
- Method: `GET`
- URL: `/api/researcher/users/by-email?email=user@example.com`
- Expected:
  - found: `200`
  - not found: `404`
  - empty email: `400`

### 2) Get by username
- Method: `GET`
- URL: `/api/researcher/users/by-username?username=testuser`
- Expected:
  - found: `200`
  - not found: `404`
  - empty username: `400`

---

## Participant and Participant-Management APIs

### Participant self
- `GET /api/me/experiments` -> `200`
- `GET /api/me/experiments/{experimentId}/bundle?since=2026-03-01T00:00:00Z` -> `200`
- `GET /api/me/experiments/{experimentId}/occurrences?from=2026-03-01&to=2026-03-31` -> `200`
- `GET /api/me/experiments/{experimentId}/occurrences/{occurrenceKey}` -> `200` or `404`
- `GET /api/me/experiments/{experimentId}/occurrences/current` -> `200`
- `POST /api/me/experiments/{experimentId}/occurrences` -> `200`
- `POST /api/me/experiments/{experimentId}/occurrences/{occurrenceKey}/start` -> `200`
- `POST /api/me/experiments/{experimentId}/occurrences/{occurrenceKey}/complete` -> `200`
- `POST /api/me/experiments/{experimentId}/occurrences/{occurrenceKey}/tasks/{taskKey}/responses` -> `200`

Task response body example:
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

### Researcher participant management
- `GET /api/experiments/{experimentId}/participants` -> `200`
- `GET /api/experiments/{experimentId}/participants/{participantId}` -> `200` or `404`
- `PUT /api/experiments/{experimentId}/participants/{participantId}` -> `200`
- `POST /api/experiments/{experimentId}/participants/batch` -> `200`
- `DELETE /api/experiments/{experimentId}/participants/{participantId}` -> `204`

---

## Questionnaires API (`{{questionnaires_api}}/api/questionnaires`)

### 1) List questionnaires
- `GET /api/questionnaires` -> `200`

### 2) Get questionnaire by id
- `GET /api/questionnaires/{id}`
- found: `200`, missing: `404`

### 3) Get by IDs
- `POST /api/questionnaires/by-ids`
- Body:
```json
["mood-v1", "sleep-v1"]
```
- Expected:
  - valid: `200`
  - empty array/all whitespace ids: `400`

### 4) Create questionnaire
- `POST /api/questionnaires`
- Body:
```json
{
  "id": "mood-v1",
  "data": {
    "name": "Mood Check",
    "description": "Daily mood capture",
    "estimatedTime": 3,
    "version": "1.0",
    "questions": [
      {
        "id": "q1",
        "text": "How is your mood?",
        "type": "scale",
        "required": true,
        "scale": { "min": 1, "max": 5, "minLabel": "Low", "maxLabel": "High" }
      }
    ]
  }
}
```
- Expected:
  - valid: `201`
  - duplicate id: `409`
  - invalid questions/shape: `400`

### 5) Update questionnaire
- `PUT /api/questionnaires/{id}`
- Body:
```json
{
  "data": {
    "name": "Mood Check Updated",
    "description": "Updated description",
    "estimatedTime": 4,
    "version": "1.1",
    "questions": [
      {
        "id": "q1",
        "text": "Mood now?",
        "type": "choice",
        "required": true,
        "options": ["Good", "Neutral", "Low"]
      }
    ]
  }
}
```
- Expected:
  - existing: `200`
  - missing/deleted: `404`
  - invalid payload: `400`

### 6) Delete questionnaire
- `DELETE /api/questionnaires/{id}`
- Expected:
  - existing: `200`
  - already deleted/missing: `200` (idempotent behavior)

### 7) Batch create questionnaires
- `POST /api/questionnaires/batch`
- Body:
```json
[
  {
    "id": "qset-1",
    "data": {
      "name": "Set 1",
      "description": "",
      "estimatedTime": 2,
      "version": "1.0",
      "questions": [
        { "id": "q1", "text": "Any notes?", "type": "text", "required": true }
      ]
    }
  },
  {
    "id": "qset-2",
    "data": {
      "name": "Set 2",
      "description": "",
      "estimatedTime": 2,
      "version": "1.0",
      "questions": [
        { "id": "q1", "text": "How was sleep?", "type": "text", "required": true }
      ]
    }
  }
]
```
- Expected:
  - all successful: `200`
  - all failed: `400`
  - partial success/failure: `207`

---

## Legacy Responses API note

- `/api/responses/*` routes are not declared in the current `infra/ExperimentManagement-template.yaml`.
- Use occurrence-task submission route instead:
  - `POST /api/me/experiments/{experimentId}/occurrences/{occurrenceKey}/tasks/{taskKey}/responses`
