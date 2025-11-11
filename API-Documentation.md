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

### POST /api/researcher/experiments
Create new experiment (researchers only).

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

### PUT /api/researcher/experiments/{experimentId}
Update experiment (researchers only).

### DELETE /api/researcher/experiments/{experimentId}
Delete experiment (researchers only).

### POST /api/questionnaires
Create new questionnaire (researchers only). Note: Route does not include `researcher` segment; access is enforced via role check in the controller.

### PUT /api/questionnaires/{questionnaireId}
Update questionnaire (researchers only). Enforced via role check.

### DELETE /api/questionnaires/{questionnaireId}
Delete questionnaire (researchers only). Enforced via role check.

### PUT /api/experiments/{experimentId}/members/{userSub}
Add user to experiment. Note: The current implementation does not explicitly enforce researcher role on this route.

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
Remove user from experiment. Note: The current implementation does not explicitly enforce researcher role on this route.

### GET /api/experiments/{experimentId}/members
List experiment members.

**Response:**
```json
[
  {
    "userSub": "0b8b7c42-3f8b-4f22-9b7a-2f4f4b8a7e5a",
    "role": "participant",
    "status": "active",
    "assignedAt": "2023-11-01T09:00:00Z",
    "cohort": "A",
    "pseudoId": "P-7GQ2K1"
  }
]
```

## Participant API (Read-Only + Responses)

### Experiments API

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

### GET /api/experiments
List all experiments.

**Response:**
```json
[
  {
    "id": "experiment-uuid",
    "name": "BCI Learning Study",
    "description": "A 21-day study on BCI skill acquisition"
  }
]
```

### GET /api/experiments/{experimentId}
Get experiment details with sessions.

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
Not available. Use `POST /api/researcher/experiments` to create experiments.

**Response:**
```json
{
  "error": "Participants cannot create experiments"
}
```

### GET /api/experiments/{experimentId}/sync
Retrieve experiment metadata and sessions, optionally filtered by last sync time.

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

## Session Management API (Researcher Only)

**Note**: Sessions are managed through dedicated endpoints with proper task ordering and session type integration.

### GET /api/experiments/{experimentId}/sessions
Retrieve all sessions for an experiment (researchers only).

**Response:**
```json
[
  {
    "sessionId": "2023-11-07",
    "data": {
      "date": "2023-11-07",
      "sessionType": "DAILY",
      "sequenceNumber": 5,
      "status": "completed",
      "userId": "USER_456",
      "startTime": "2023-11-07T10:00:00Z",
      "endTime": "2023-11-07T10:25:00Z",
      "metadata": {
        "dayOfStudy": 5,
        "weekOfStudy": 1,
        "isRescheduled": false
      }
    },
    "taskOrder": ["TASK#TRAIN_EEG", "TASK#POST_SESSION_QUESTIONS"],
    "createdAt": "2023-11-07T08:00:00Z",
    "updatedAt": "2023-11-07T10:25:00Z"
  }
]
```

### GET /api/experiments/{experimentId}/sessions/{sessionId}
Retrieve a specific session by ID (researchers only).

**Response:**
```json
{
  "sessionId": "2023-11-07",
  "experimentId": "EXP_001",
  "data": {
    "date": "2023-11-07",
    "sessionType": "DAILY",
    "sequenceNumber": 5,
    "status": "completed",
    "userId": "USER_456",
    "startTime": "2023-11-07T10:00:00Z",
    "endTime": "2023-11-07T10:25:00Z",
    "metadata": {
      "dayOfStudy": 5,
      "weekOfStudy": 1,
      "isRescheduled": false
    }
  },
  "taskOrder": ["TASK#TRAIN_EEG", "TASK#POST_SESSION_QUESTIONS"],
  "createdAt": "2023-11-07T08:00:00Z",
  "updatedAt": "2023-11-07T10:25:00Z"
}
```

### POST /api/experiments/{experimentId}/sessions
Create a new session for an experiment (researchers only).

**Request Body:**
```json
{
  "sessionType": "DAILY",
  "date": "2023-11-08"
}
```

**Response:**
```json
{
  "sessionId": "2023-11-08",
  "experimentId": "EXP_001"
}
```

### PUT /api/experiments/{experimentId}/sessions/{sessionId}
Update an existing session (researchers only).

**Request Body:**
```json
{
  "date": "2023-11-08",
  "sessionType": "DAILY",
  "sequenceNumber": 6,
  "status": "scheduled",
  "userId": "USER_456",
  "metadata": {
    "dayOfStudy": 6,
    "weekOfStudy": 1,
    "isRescheduled": false
  }
}
```

**Response:**
```json
{
  "message": "Session updated successfully"
}
```

### DELETE /api/experiments/{experimentId}/sessions/{sessionId}
Delete a session (researchers only).

**Response:**
```json
{
  "message": "Session deleted successfully"
}
```

## Task Management API (Researcher Only)

**Note**: Tasks are reusable components that can be referenced in session types and ordered within sessions.

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
```json
{
  "name": "EEG Training",
  "type": "TRAIN_EEG",
  "description": "Basic EEG neurofeedback training",
  "configuration": {
    "duration": 300,
    "difficulty": "beginner"
  },
  "estimatedDuration": 300
}
```

**Response:**
```json
{
  "id": "task-uuid"
}
```

### PUT /api/tasks/{taskId}
Update an existing task (researchers only).

**Request Body:**
```json
{
  "name": "Advanced EEG Training",
  "type": "TRAIN_EEG",
  "description": "Advanced EEG neurofeedback training",
  "configuration": {
    "duration": 600,
    "difficulty": "advanced"
  },
  "estimatedDuration": 600
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

## Mobile Sync API

Use `GET /api/experiments/{experimentId}/sync` with `lastSyncTime` to drive mobile-side sync for experiments. Questionnaires and responses do not have dedicated sync endpoints; fetch via their standard list endpoints with app-level caching logic.

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