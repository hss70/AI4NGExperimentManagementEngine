# AI4NG Question Engine API Documentation

## Base URL
`https://{api-gateway-id}.execute-api.eu-west-2.amazonaws.com/dev`

## Authentication
All endpoints require JWT Bearer token in Authorization header:
```
Authorization: Bearer {jwt-token}
```

## User Types
- **Researchers**: Admin users with full CRUD permissions (use `/api/researcher/` endpoints)
- **Participants**: Regular users with read-only access + response submission (use `/api/` endpoints)

## Researcher API (Admin Access)

### POST /api/experiments
Create new experiment (researchers only; role enforced in controller).

**Important**: Questionnaires must be created first using `POST /api/questionnaires` before referencing them in experiments.

**Request Body:
```json
{
  "data": {
    "name": "BCI Learning Study",
    "description": "A 21-day study on BCI skill acquisition",
    "sessionTypes": {
      "START": {
        "name": "Initial Session",
        "questionnaires": ["PreState", "PhysicalState", "VVIQ", "16PF5"],
        "tasks": ["TRAIN_EEG_BASELINE", "BASELINE_QUESTIONS"],
        "estimatedDuration": 45
      },
      "DAILY": {
        "name": "Daily Training Session",
        "questionnaires": ["PreState", "PhysicalState", "CurrentState", "EndState", "PQ", "TLX"],
        "tasks": ["TRAIN_EEG", "POST_SESSION_QUESTIONS"],
        "estimatedDuration": 25,
        "schedule": "daily"
      }
    }
  },
  "questionnaireConfig": {
    "schedule": {
      "PreState": "every_session",
      "PhysicalState": "every_session",
      "CurrentState": "every_session",
      "EndState": "every_session",
      "PQ": "every_session",
      "TLX": "every_session",
      "VVIQ": "once",
      "16PF5": "once"
    }
  }
}
```

### PUT /api/experiments/{experimentId}
Update experiment (researchers only; role enforced).

### DELETE /api/experiments/{experimentId}
Delete experiment (researchers only; role enforced).

### POST /api/questionnaires
Create new questionnaire (researchers only). Note: Route does not include `researcher` segment; access is enforced via role check in the controller.

### PUT /api/questionnaires/{questionnaireId}
Update questionnaire (researchers only). Enforced via role check.

### DELETE /api/questionnaires/{questionnaireId}
Delete questionnaire (researchers only). Enforced via role check.

### PUT /api/experiments/{experimentId}/members/{userSub}
Add user to experiment (researchers only; role enforced).

**Request Body:**
```json
{
  "role": "participant",
  "status": "active",
  "cohort": "A",
  "startDate": "2023-11-02",
  "endDate": "2023-11-23",
  "timezone": "Europe/London",
  "pseudoId": "P-7GQ2K1"
}
```

### DELETE /api/experiments/{experimentId}/members/{userSub}
Remove user from experiment (researchers only; role enforced).

### GET /api/experiments/{experimentId}/members
List experiment members.

**Response:**
```json
[
  {
    "userSub": "0b8b7c42-3f8b-4f22-9b7a-2f4f4b8a7e5a",
    "role": "participant",
    "addedAt": "2025-10-13T09:00:00Z"
  }
]
```

## Participant API (Read-Only + Responses)

### GET /api/me/experiments
Get experiments assigned to the current user.

**Response:**
```json
[
  {
    "id": "experiment-uuid",
    "name": "BCI Learning Study",
    "description": "A 21-day study on BCI skill acquisition",
    "membership": {
      "role": "participant",
      "status": "active",
      "cohort": "A",
      "pseudoId": "P-7GQ2K1"
    }
  }
]
```

> Note: Listing all experiments is a researcher-only capability. Participants should use `/api/me/experiments`.

### GET /api/experiments/{experimentId}
Get experiment details with sessions (researchers only).

**Response:**
```json
{
  "id": "experiment-uuid",
  "data": {
    "name": "BCI Learning Study",
    "description": "A 21-day study on BCI skill acquisition"
  },
  "questionnaireConfig": {
    "schedule": {
      "PQ": "every_session",
      "16PF5": "once"
    }
  },
  "sessions": [
    {
      "sessionId": "2023-11-07",
      "date": "2023-11-07",
      "type": "daily",
      "userId": "USER_456"
    }
  ]
}
```

### POST /api/experiments
Researcher-only route (participants will receive 403 Forbidden).

**Response (success):**
```json
{ "id": "experiment-uuid" }
```

### GET /api/experiments/sync
Retrieve experiment metadata and sessions for the authenticated participant, optionally filtered by last sync time.

Query Parameters:
- `lastSyncTime` (optional, ISO-8601) — only return items updated after this timestamp.

Example Response:
```json
{
  "experiment": {
    "id": "experiment-uuid",
    "data": { "name": "BCI Learning Study", "description": "..." },
    "questionnaireConfig": { "schedule": { "PQ": "every_session" } },
    "updatedAt": "2025-10-13T09:00:00Z"
  },
  "sessions": [
    { "data": { "sessionId": "2025-10-12", "type": "daily" }, "updatedAt": "2025-10-12T10:00:00Z" }
  ],
  "syncTimestamp": "2025-10-13T10:00:00Z"
}
```

## Session & Task APIs

Note: Session endpoints (session execution, per-session task ordering endpoints) are partially implemented via the Experiment/session models but task management is implemented in the Experiments service. The Task Management endpoints below reflect the current controller and service behavior (request shapes, validation rules and response types).

## Task Management API (Researcher Only)

**Note**: Tasks are reusable components that can be referenced in session types and ordered within sessions.

Validation and canonical types
- Task creation/update enforces a canonical set of Task Type values. Supported canonical types are: "Training", "NeuroGame", "Questionnaire", and "QuestionnaireSet". The service normalizes common variants but tests and clients should prefer the canonical values.
- Task keys (`TaskKey`) are required and must match the pattern: ^[A-Z0-9_]{3,64}$ (uppercase, digits and underscore only, length 3-64). The service normalizes incoming keys to upper-case.
- Questionnaire-related rules enforced on Task Data:
  - "Questionnaire" tasks must define exactly one QuestionnaireId in `QuestionnaireIds`.
  - "QuestionnaireSet" tasks must define at least one QuestionnaireId in `QuestionnaireIds`.
  - "Training" and "NeuroGame" tasks must NOT define `QuestionnaireIds`.

### GET /api/tasks
Retrieve all tasks (researchers only).

**Response:**
```json
[
  {
    "id": "task-uuid",
    "data": {
      "name": "EEG Training",
      "type": "TRAIN_EEG",
      "description": "Basic EEG neurofeedback training",
      "configuration": {
        "duration": 300,
        "difficulty": "beginner"
      },
      "estimatedDuration": 300
    },
    "createdAt": "2023-11-01T09:00:00Z",
    "updatedAt": "2023-11-01T09:00:00Z"
  }
]
```

### GET /api/tasks/{taskId}
Retrieve a specific task by ID (researchers only).

**Response:**
```json
{
  "id": "task-uuid",
  "data": {
    "name": "EEG Training",
    "type": "TRAIN_EEG",
    "description": "Basic EEG neurofeedback training",
    "configuration": {
      "duration": 300,
      "difficulty": "beginner"
    },
    "estimatedDuration": 300
  },
  "createdAt": "2023-11-01T09:00:00Z",
  "updatedAt": "2023-11-01T09:00:00Z"
}
```

### POST /api/tasks
Create a new task (researchers only).

**Request Body:**
The current service expects a `CreateTaskRequest` shape that contains a `TaskKey` (string) and a nested `Data` object (the task details). Example:

```json
{
  "taskKey": "TRAIN_EEG",
  "data": {
    "name": "EEG Training",
    "type": "Training",
    "description": "Basic EEG neurofeedback training",
    "configuration": {
      "duration": 300,
      "difficulty": "beginner"
    },
    "estimatedDuration": 300
  }
}
```

**Response:**
```json
{
  "id": "TRAIN_EEG"
}
```

The controller returns HTTP 201 Created (CreatedAtAction) with the created task id in the response body. Clients should read the Location header or the returned `id` value.

### PUT /api/tasks/{taskId}
Update an existing task (researchers only).

**Request Body:**
Same `TaskKey` + `Data` shape as POST. Example update payload:

```json
{
  "taskKey": "TRAIN_EEG",
  "data": {
    "name": "Advanced EEG Training",
    "type": "Training",
    "description": "Advanced EEG neurofeedback training",
    "configuration": { "duration": 600, "difficulty": "advanced" },
    "estimatedDuration": 600
  }
}
```

**Response:**
```json
{
  "message": "Task updated successfully"
}
```

### DELETE /api/tasks/{taskId}
Delete a task (researchers only).

**Response:**
```json
{
  "message": "Task deleted successfully"
}
```

Notes about task identifiers
- The API uses the `taskKey` as the canonical identifier for tasks (returned as `id` in responses). The service normalizes to upper-case and validates the format.
- Clients should provide consistent `TaskKey` values when referencing tasks in session types or when performing updates.

Cancellation tokens
- The current API and controllers do not expose or document HTTP-level cancellation tokens for requests. If you plan to add support for request cancellation (e.g., via middleware or explicit controller method signatures), the documentation should be extended to describe how clients can cancel long-running requests.

## Questionnaires API

### GET /api/questionnaires
List all questionnaires.

**Response:**
```json
[
  {
    "id": "PQ",
    "name": "Presence Questionnaire",
    "description": "Measures the sense of presence in a virtual environment",
    "version": "1.0",
    "estimatedTime": "120"
  }
]
```

### GET /api/questionnaires/{questionnaireId}
Get questionnaire definition.

**Response:**
```json
{
  "id": "PQ",
  "data": {
    "name": "Presence Questionnaire",
    "description": "Measures the sense of presence in a virtual environment",
    "estimatedTime": 120,
    "version": "1.0",
    "questions": [
      {
        "id": "1",
        "text": "Time seemed to go by",
        "type": "scale",
        "scale": {
          "min": 1,
          "max": 10,
          "minLabel": "Quickly",
          "maxLabel": "Slowly"
        },
        "required": true
      }
    ]
  },
  "createdAt": "2023-11-01T09:00:00Z",
  "updatedAt": "2023-11-15T14:20:00Z"
}
```

### POST /api/questionnaires
Researcher-only route (role enforced in controller). Creates a questionnaire with the given id and data.

**Response:**
```json
{
  "error": "Participants cannot create questionnaires"
}
```

### PUT /api/questionnaires/{questionnaireId}
Researcher-only route (role enforced in controller). Updates questionnaire data.

**Response:**
```json
{
  "error": "Participants cannot update questionnaires"
}
```

## Responses API

### POST /api/responses
Submit questionnaire responses.

Request Body:
```json
{
  "data": {
    "experimentId": "experiment-uuid",
    "questionnaireId": "PQ",
    "sessionId": "2025-10-13",
    "responses": [
      { "questionId": "1", "answer": 7, "timestamp": "2025-10-13T10:05:30Z" }
    ]
  }
}
```

Response:
```json
{ "id": "response-uuid" }
```

### GET /api/responses
List responses. Supports filtering by query parameters:
- `experimentId` (optional)
- `sessionId` (optional; only valid when `experimentId` is provided)

Response items include metadata: `id`, `data`, `createdBy`, `createdAt`.

### GET /api/responses/{responseId}
Get a single response by id.

### PUT /api/responses/{responseId}
Update an existing response (participants can only update their own).

Request Body:
```json
{
  "experimentId": "experiment-uuid",
  "questionnaireId": "PQ",
  "sessionId": "2025-10-13",
  "responses": [
    { "questionId": "1", "answer": 8, "timestamp": "2025-10-13T11:00:00Z" }
  ]
}
```

Response:
```json
{ "message": "Response updated successfully" }
```

### DELETE /api/responses/{responseId}
Delete a response (soft delete).

Response:
```json
{ "message": "Response deleted successfully" }
```

## Mobile Sync API

Use `GET /api/experiments/sync?lastSyncTime=...` to drive mobile-side sync for experiments. Questionnaires and responses do not have dedicated sync endpoints; fetch via their standard list endpoints with app-level caching logic.

## Authentication Details

### Researcher Authentication
Researchers must authenticate against the researcher user pool to access admin endpoints:
- User Pool: `ai4ng-researchers-{environment}`
- Client ID: Available in CloudFormation outputs
- Endpoints: `/api/researcher/*`

### Participant Authentication
Participants use the shared authentication system:
- Endpoints: `/api/*` (excluding `/api/researcher/*`)
- Limited to read operations and response submission

## Error Responses

All endpoints return errors in this format:
```json
{
  "error": "Error message"
}
```

**Status Codes:**
- `400` - Bad Request
- `401` - Unauthorized
- `403` - Forbidden (insufficient permissions)
- `404` - Not Found
- `405` - Method Not Allowed
- `500` - Internal Server Error