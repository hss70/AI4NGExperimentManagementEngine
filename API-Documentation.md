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

**Request Body:**
```json
{
  "data": {
    "name": "BCI Learning Study",
    "description": "A 21-day study on BCI skill acquisition"
  },
  "questionnaireConfig": {
    "schedule": {
      "PQ": "every_session",
      "16PF5": "once"
    }
  }
}
```

### PUT /api/researcher/experiments/{experimentId}
Update experiment (researchers only).

### DELETE /api/researcher/experiments/{experimentId}
Delete experiment (researchers only).

### POST /api/researcher/questionnaires
Create new questionnaire (researchers only).

### PUT /api/researcher/questionnaires/{questionnaireId}
Update questionnaire (researchers only).

### DELETE /api/researcher/questionnaires/{questionnaireId}
Delete questionnaire (researchers only).

### PUT /api/experiments/{experimentId}/members/{userSub}
Add user to experiment (researchers only).

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
Remove user from experiment (researchers only).

### GET /api/experiments/{experimentId}/members
List experiment members (researchers only).

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
**RESTRICTED**: Returns 403 Forbidden for participants. Use researcher endpoints for creation.

**Response:**
```json
{
  "error": "Participants cannot create experiments"
}
```

### POST /api/experiments/{experimentId}/sync
Sync experiment sessions.

**Request Body:**
```json
{
  "sessions": [
    {
      "sessionId": "2023-11-07",
      "date": "2023-11-07",
      "type": "daily",
      "userId": "USER_456",
      "taskOrder": ["TASK#TRAIN_EEG", "TASK#POST_QUESTIONS"]
    }
  ]
}
```

**Response:**
```json
{
  "message": "Experiment synced successfully"
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
**RESTRICTED**: Returns 403 Forbidden for participants. Use researcher endpoints for creation.

**Response:**
```json
{
  "error": "Participants cannot create questionnaires"
}
```

### PUT /api/questionnaires/{questionnaireId}
**RESTRICTED**: Returns 403 Forbidden for participants. Use researcher endpoints for updates.

**Response:**
```json
{
  "error": "Participants cannot update questionnaires"
}
```

## Responses API

### POST /api/responses
Submit questionnaire responses.

**Request Body:**
```json
{
  "experimentId": "experiment-uuid",
  "questionnaireId": "PQ",
  "sessionId": "2023-11-07",
  "taskId": "POST_QUESTIONS",
  "answers": [
    {
      "questionId": "1",
      "questionText": "Time seemed to go by",
      "answerValue": "7"
    }
  ]
}
```

**Response:**
```json
{
  "message": "Responses submitted successfully"
}
```

### GET /api/responses/{experimentId}
Get user responses for experiment.

**Response:**
```json
[
  {
    "questionnaireId": "PQ",
    "questionId": "1",
    "questionText": "Time seemed to go by",
    "answerValue": "7",
    "timestamp": "2023-11-07T10:05:30.000Z",
    "sessionId": "2023-11-07",
    "taskId": "POST_QUESTIONS"
  }
]
```

## Mobile Sync API

### GET /api/sync/experiments
Get modified experiments for mobile sync.

**Query Parameters:**
- `lastSync` (optional): ISO timestamp of last sync

**Response:**
```json
[
  {
    "id": "experiment-uuid",
    "data": {...},
    "syncMetadata": {
      "version": 2,
      "lastModified": "2023-11-07T10:25:00Z",
      "isDeleted": false
    }
  }
]
```

### GET /api/sync/questionnaires
Get modified questionnaires for mobile sync.

**Query Parameters:**
- `lastSync` (optional): ISO timestamp of last sync

**Response:**
```json
[
  {
    "id": "PQ",
    "data": {...},
    "syncMetadata": {
      "version": 1,
      "lastModified": "2023-11-01T09:00:00Z",
      "isDeleted": false
    }
  }
]
```

### GET /api/sync/responses
Get modified responses for mobile sync.

**Query Parameters:**
- `lastSync` (optional): ISO timestamp of last sync

**Response:**
```json
[
  {
    "questionnaireId": "PQ",
    "questionId": "1",
    "answerValue": "7",
    "timestamp": "2023-11-07T10:05:30.000Z",
    "syncMetadata": {
      "version": 1,
      "lastModified": "2023-11-07T10:05:30.000Z",
      "isDeleted": false
    }
  }
]
```

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