# BCI Research Platform Database Design

## Overview

This document outlines the database design for a Brain-Computer Interface (BCI) research platform using Amazon DynamoDB. The system manages experiments consisting of sessions that contain ordered tasks, including questionnaires, EEG training, and games. The design focuses on scalability, flexible querying capabilities, and integration with existing infrastructure (S3 for EEG data, separate classifier table).

## Table Structure

### 1. AI4NGExperiments (Main Table)

Stores experiments, sessions, tasks, and questionnaire configurations using a single-table design pattern.

#### Key Structure:
- **PK** (Partition Key): Entity identifier (`EXPERIMENT#<id>`, `SESSION#<exp_id>#<session_id>`, `QUESTIONNAIRE#<id>`)
- **SK** (Sort Key): Item type (`METADATA`, `TASK#<id>`, `CONFIG`)
- **GSI1PK/GSI1SK**: For querying all sessions for an experiment

#### Key Attributes:
- `type`: Entity type (Experiment, Session, Task, QuestionnaireConfig)
- `data`: Main data container for entity-specific attributes
- `questionnaireConfig`: Rules for questionnaire availability and scheduling
- `taskOrder`: Ordered list of task IDs for sessions
- `createdAt/updatedAt`: Timestamps

### 2. AI4NGResponses (Separate Answers Table)

Stores user responses to questionnaire questions with multiple access patterns.

#### Key Structure:
- **PK**: `ANSWER#<experiment_id>#<user_id>#<questionnaire_id>`
- **SK**: `<timestamp>#<question_id>`
- **GSI1PK/GSI1SK**: For querying answers by session-task combination
- **GSI2PK/GSI2SK**: For querying all answers by user

#### Key Attributes:
- Experiment, user, session, task, and questionnaire identifiers
- Question text and answer values
- Timestamps and metadata
- Multiple GSIs for flexible querying

## Access Patterns

### Experiment Management:
- Get experiment by ID: `PK = EXPERIMENT#<id>, SK = METADATA`
- Get all sessions for experiment: Query GSI1 with `GSI1PK = EXPERIMENT#<id>`

### Session Execution:
- Get session details: `PK = SESSION#<exp_id>#<session_id>, SK = METADATA`
- Get session tasks: Retrieve `taskOrder` list, then batch get items

### Questionnaire Handling:
- Determine available questionnaires: Check experiment's `questionnaireConfig`
- Store answers: Put items to AI4NGResponses for each question
- Query answers by user: Query GSI2 with `GSI2PK = USER#<user_id>`
- Query answers by session-task: Query GSI1 with `GSI1PK = SESSION#<exp_id>#<session_id>#TASK#<task_id>`

## Data Model Examples

### Experiment Item:
```json
{
  "PK": "EXPERIMENT#EXP_001",
  "SK": "METADATA",
  "type": "Experiment",
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

### Session Item:
```json
{
  "PK": "SESSION#EXP_001#2023-11-07",
  "SK": "METADATA",
  "type": "Session",
  "taskOrder": ["TASK#TRAIN_EEG", "TASK#POST_QUESTIONS"],
  "data": {
    "date": "2023-11-07",
    "type": "daily",
    "userId": "USER_456"
  }
}

### Answer Item:
```json
{
  "PK": "ANSWER#EXP_001#USER_456#PQ",
  "SK": "2023-11-07T10:05:30.000Z#1",
  "GSI1PK": "SESSION#EXP_001#2023-11-07#TASK#POST_QUESTIONS",
  "GSI1SK": "1",
  "questionnaireId": "PQ",
  "questionId": "1",
  "answerValue": "7",
  "questionText": "Time seemed to go by"
}


