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