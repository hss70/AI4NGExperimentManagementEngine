# AI4NG Question Engine API Documentation

## Base URL
`https://{api-gateway-id}.execute-api.eu-west-2.amazonaws.com/dev`

## Authentication
All endpoints require JWT Bearer token in Authorization header:
```
Authorization: Bearer {jwt-token}
```

## Experiments API

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
Create new experiment.

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

**Response:**
```json
{
  "id": "experiment-uuid"
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
Create new questionnaire.

**Request Body:**
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
  }
}
```

**Response:**
```json
{
  "id": "PQ"
}
```

### PUT /api/questionnaires/{questionnaireId}
Update questionnaire definition.

**Request Body:** Same as POST

**Response:**
```json
{
  "message": "Questionnaire updated successfully"
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
- `404` - Not Found
- `405` - Method Not Allowed
- `500` - Internal Server Error