# BCI Research Platform Database Design

## Overview

This document outlines the database design for a Brain-Computer Interface (BCI) research platform using Amazon DynamoDB. The system manages experiments consisting of sessions that contain ordered tasks, including questionnaires, EEG training, and games. The design focuses on scalability, flexible querying capabilities, and integration with existing infrastructure (S3 for EEG data, separate classifier table).

## Table Structure

### 1. AI4NGExperiments (Main Table)

Stores experiments, sessions, tasks, and experiment configurations. References questionnaire IDs rather than storing full definitions.

#### Key Structure:
- **PK** (Partition Key): Entity identifier (`EXPERIMENT#<id>`, `SESSION#<exp_id>#<session_id>`, `TASK#<id>`)
- **SK** (Sort Key): Item type (`METADATA`, `TASK#<id>`, `CONFIG`)
- **GSI1PK/GSI1SK**: For querying all sessions for an experiment
- **GSI3PK/GSI3SK**: For mobile sync (user-based queries)

#### Key Attributes:
- `type`: Entity type (Experiment, Session, Task)
- `data`: Main data container for entity-specific attributes
- `questionnaireConfig`: Rules for questionnaire availability and scheduling (references questionnaire IDs)
- `taskOrder`: Ordered list of task IDs for sessions
- `createdAt/updatedAt`: Timestamps
- `syncMetadata`: For mobile synchronization

### 2. AI4NGQuestionnaires (Questionnaire Definitions Table)

Stores reusable questionnaire definitions and configurations. Separated from experiments for better maintainability and version control.

#### Key Structure:
- **PK** (Partition Key): `QUESTIONNAIRE#<questionnaire_id>`
- **SK** (Sort Key): `CONFIG`
- **GSI3PK/GSI3SK**: For mobile sync and efficient querying

#### Key Attributes:
- `type`: Always "Questionnaire"
- `data`: Questionnaire definition including name, description, questions, version info
- `version`: Version of the questionnaire for tracking changes
- `createdAt/updatedAt`: Timestamps
- `syncMetadata`: For mobile synchronization

### 3. AI4NGResponses (Separate Answers Table)

Stores user responses to questionnaire questions with multiple access patterns.

#### Key Structure:
- **PK**: `ANSWER#<experiment_id>#<user_id>#<questionnaire_id>`
- **SK**: `<timestamp>#<question_id>`
- **GSI1PK/GSI1SK**: For querying answers by session-task combination
- **GSI2PK/GSI2SK**: For querying all answers by user
- **GSI3PK/GSI3SK**: For mobile sync (user-based queries with timestamps)

#### Key Attributes:
- Experiment, user, session, task, and questionnaire identifiers
- Question text and answer values (with question text for historical integrity)
- Timestamps and response metadata
- Multiple GSIs for flexible querying
- `syncMetadata`: For mobile synchronization

## Access Patterns

### Experiment Management:
- Get experiment by ID: `PK = EXPERIMENT#<id>, SK = METADATA`
- Get all sessions for experiment: Query GSI1 with `GSI1PK = EXPERIMENT#<id>`
- Get experiment questionnaire references: Extract from `questionnaireConfig` and `sessionTypes`

### Session Execution:
- Get session details: `PK = SESSION#<exp_id>#<session_id>, SK = METADATA`
- Get session tasks: Retrieve `taskOrder` list, then batch get items
- Determine session type and required questionnaires: Check session's `sessionType` against experiment config

### Questionnaire Handling:
- Get questionnaire definition: `PK = QUESTIONNAIRE#<id>, SK = CONFIG` from AI4NGQuestionnaires table
- Get all questionnaires for experiment: Batch get from AI4NGQuestionnaires using IDs from experiment config
- Store answers: Put items to AI4NGResponses for each question
- Query answers by user: Query GSI2 with `GSI2PK = USER#<user_id>`
- Query answers by session-task: Query GSI1 with `GSI1PK = SESSION#<exp_id>#<session_id>#TASK#<task_id>`

### Mobile Sync:
- Get modified experiments: Query GSI3 with `GSI3PK = USER#<user_id>#EXPERIMENT` and `GSI3SK > lastSync`
- Get modified questionnaires: Query GSI3 with `GSI3PK = USER#<user_id>#QUESTIONNAIRE` and `GSI3SK > lastSync`
- Get modified responses: Query GSI3 with `GSI3PK = USER#<user_id>#RESPONSE` and `GSI3SK > lastSync`
- Batch write offline responses: Use batch operations with client-generated timestamps

## Data Model Examples

### Experiment Item (in AI4NGExperiments):
```json
{
  "PK": "EXPERIMENT#EXP_001",
  "SK": "METADATA",
  "type": "Experiment",
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
  },
  "createdAt": "2023-11-01T09:00:00Z",
  "updatedAt": "2023-11-01T09:00:00Z",
  "syncMetadata": {
    "version": 1,
    "lastModified": "2023-11-01T09:00:00Z",
    "isDeleted": false
  }
}
### Session Item:
```json
{
  "PK": "SESSION#EXP_001#2023-11-07",
  "SK": "METADATA",
  "type": "Session",
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
  "updatedAt": "2023-11-07T10:25:00Z",
  "syncMetadata": {
    "version": 1,
    "lastModified": "2023-11-07T10:25:00Z",
    "modifiedBy": "USER_456",
    "clientId": "mobile-app-123",
    "isDeleted": false
  }
}

### Answer Item:
```json
{
  "PK": "ANSWER#EXP_001#USER_456#PQ",
  "SK": "2023-11-07T10:05:30.123Z#1",
  "GSI1PK": "SESSION#EXP_001#2023-11-07#TASK#POST_QUESTIONS",
  "GSI1SK": "1",
  "GSI2PK": "USER#USER_456",
  "GSI2SK": "2023-11-07T10:05:30.123Z#PQ",
  "GSI3PK": "USER#USER_456#RESPONSE",
  "GSI3SK": "2023-11-07T10:05:30.123Z",
  "experimentId": "EXP_001",
  "userId": "USER_456",
  "sessionId": "SESSION#EXP_001#2023-11-07",
  "taskId": "TASK#POST_QUESTIONS",
  "questionnaireId": "PQ",
  "questionId": "1",
  "questionText": "Time seemed to go by",
  "answerValue": "7",
  "answerType": "scale",
  "answerOptions": {
    "min": 1,
    "max": 10,
    "minLabel": "Quickly",
    "maxLabel": "Slowly"
  },
  "timestamp": "2023-11-07T10:05:30.123Z",
  "responseTimeMs": 4500,
  "metadata": {
    "device": "mobile",
    "appVersion": "1.2.0",
    "sequenceNumber": 1
  },
  "syncMetadata": {
    "version": 1,
    "lastModified": "2023-11-07T10:05:30.123Z",
    "modifiedBy": "USER_456",
    "clientId": "mobile-app-123",
    "isDeleted": false,
    "batchId": "BATCH_20231107_1005"
  }
}

###Questionnaire Item:
```json
{
  "PK": "QUESTIONNAIRE#PQ",
  "SK": "CONFIG",
  "type": "Questionnaire",
  "data": {
    "name": "Presence Questionnaire",
    "description": "Measures the sense of presence in a virtual environment.",
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
      },
      {
        "id": "2",
        "text": "I felt",
        "type": "scale",
        "scale": {
          "min": 1,
          "max": 10,
          "minLabel": "Calm",
          "maxLabel": "Anxious"
        },
        "required": true
      }
    ],
    "scoring": {
      "type": "sum",
      "description": "Higher scores indicate greater presence"
    }
  },
  "createdAt": "2023-11-01T09:00:00Z",
  "updatedAt": "2023-11-15T14:20:00Z",
  "syncMetadata": {
    "version": 2,
    "lastModified": "2023-11-15T14:20:00Z",
    "isDeleted": false
  }
}


###API Gateway: 
- Existing API Gateway and Cognito authorizer used for all new endpoints
- Consistent authentication and authorization patterns 
### Key Design Benefits Separation of Concerns: 
- Questionnaire definitions separate from experiment configurations and response data 
- Reusability: Questionnaires can be used across multiple experiments 
- Version Control: Questionnaire versions tracked independently 
- Efficient Mobile Sync: Delta updates using sync metadata and GSIs 
- Historical Integrity: Question text stored with responses for data consistency 
- Flexible Querying: Multiple GSIs support all research access patterns 
- Scalability: Separate tables prevent item size limits and optimize performance 
### Implementation Guidelines 
## 1. Session Flow: 
- Retrieve experiment configuration 
- Determine session type based on user progress and experiment rules 
- Create session with appropriate task order from session type configuration 
- Execute tasks in sequence, including questionnaire tasks 
- Store answers in AI4NGResponses as user completes questionnaires 
### 2. Mobile Sync Strategy: 
- Use GSI3 for efficient delta queries 
- Implement conflict resolution (last-write-wins or custom logic) 
- Batch offline responses for network efficiency 
- Maintain sync timestamps per user and entity type 
3. Data Analysis: 
- Use GSIs for research queries across users, experiments, or questionnaires 
- Aggregate responses by session type for comparative analysis 
- Track questionnaire completion rates and response times 

This three-table design provides a robust foundation for BCI research studies while maintaining efficient data management and powerful querying capabilities for research analysis.