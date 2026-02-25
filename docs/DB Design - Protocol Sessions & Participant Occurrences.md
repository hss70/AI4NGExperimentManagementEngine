# BCI Research Platform Database Design

## Overview

This document defines the DynamoDB data model for a Brain–Computer Interface (BCI) research platform. The platform manages:

1) **Tasks**: reusable definitions representing training blocks, neurogames, questionnaires, or questionnaire sets.  
2) **Experiments**: study-level metadata and the **protocol**, including a small set of **Protocol Sessions** (global schedule shared by all participants).  
3) **Participants**: membership/enrolment into experiments.  
4) **Participant Session Occurrences**: participant-specific scheduled/actual execution records derived from the protocol.  
5) **Responses**: participant outputs for questionnaire items and other task results.

The design emphasises scalability, Query-first access patterns, mobile synchronisation (delta sync), and historical integrity.

---

## Terminology (Academic Framing)

- **Experiment Protocol**: the canonical specification of what should occur in the study.
- **Protocol Session**: a protocol-level session specification shared across participants (e.g., FIRST / DAILY / WEEKLY), including an ordered **TaskSequence**.
- **Participant Session Occurrence**: a participant-specific instantiation of a protocol session at a scheduled date/time, with execution state (scheduled/in-progress/completed/missed) and links to outputs.

---

## Table Structure

### 1. AI4NGExperiments (Main Table)

Stores experiment protocol definitions, protocol sessions, tasks, participant enrolments, and participant session occurrences. Questionnaires are referenced by ID and stored separately in AI4NGQuestionnaires.

#### Key Structure
- **PK** (Partition Key): entity identifier  
  - `EXPERIMENT#<experiment_id>`
  - `TASK#<task_key>`
  - `PSO#<experiment_id>#<participant_id>` (Participant Session Occurrence collection)
- **SK** (Sort Key): item type / occurrence identifier  
  - `METADATA`
  - `PROTOCOL_SESSION#<protocol_session_key>`
  - `MEMBER#<cognito_sub>`
  - `OCCURRENCE#<occurrence_key>` (under PSO partition)

#### Indexes
- **GSI1PK/GSI1SK**: list pages (query-only; no scans)
  - Experiments list: `GSI1PK="EXPERIMENT"`, `GSI1SK=<createdAtIso>`
  - Tasks list: `GSI1PK="TASK"`, `GSI1SK=<createdAtIso>`
  - Membership-by-user: `GSI1PK="USER#<cognito_sub>"`, `GSI1SK="EXPERIMENT#<experiment_id>"`
- **GSI3PK/GSI3SK**: mobile delta sync (user-based queries by lastModified)
  - Example: `GSI3PK="USER#<cognito_sub>#SYNC#EXPERIMENT"`, `GSI3SK=<lastModifiedIso>`
  - Example: `GSI3PK="USER#<cognito_sub>#SYNC#PSO"`, `GSI3SK=<lastModifiedIso>`
  - Example: `GSI3PK="USER#<cognito_sub>#SYNC#TASK"`, `GSI3SK=<lastModifiedIso>`

#### Common Attributes
- `type`: entity type (Experiment, ProtocolSession, Task, Membership, ParticipantSessionOccurrence)
- `data`: entity-specific payload
- `createdAt`, `updatedAt`
- `syncMetadata`: `{ version, lastModified, isDeleted, modifiedBy, clientId, batchId? }`

---

### 2. AI4NGQuestionnaires (Questionnaire Definitions Table)

Stores reusable questionnaire definitions and configurations.

#### Key Structure
- **PK**: `QUESTIONNAIRE#<questionnaire_id>`
- **SK**: `CONFIG`

#### Attributes
- `type`: "Questionnaire"
- `data`: questionnaire definition and question list
- `createdAt`, `updatedAt`
- `syncMetadata` (delta sync)

---

### 3. AI4NGResponses (Answers / Outputs Table)

Stores participant outputs for questionnaire questions and other task results.

#### Recommended Key Structure (Occurrence-centric)
- **PK**: `RESP#<experiment_id>#<participant_id>#<occurrence_key>`
- **SK**: `<timestampIso>#<task_key>#<response_id_or_question_id>`

This makes a Participant Session Occurrence the natural parent for outputs (academically meaningful and operationally clean).

#### Indexes (examples)
- **GSI1**: query outputs by participant across experiments  
  - `GSI1PK="USER#<participant_id>"`, `GSI1SK=<timestampIso>#<experiment_id>#<occurrence_key>`
- **GSI2**: query outputs by experiment across participants (analysis)  
  - `GSI2PK="EXPERIMENT#<experiment_id>"`, `GSI2SK=<timestampIso>#<participant_id>#<occurrence_key>`
- **GSI3**: mobile sync  
  - `GSI3PK="USER#<participant_id>#SYNC#RESPONSE"`, `GSI3SK=<lastModifiedIso>`

#### Attributes
- Identifiers: `experimentId`, `participantId`, `occurrenceKey`, `protocolSessionKey`, `taskKey`, `questionnaireId?`, `questionId?`
- Historical integrity: `questionText` (for questionnaire items) and optional `answerOptions` snapshot
- Response payload: `answerValue` / `payload` (freeform JSON for non-questionnaire tasks)
- `timestamp`, `responseTimeMs`, `metadata` (device/app version/etc.)
- `syncMetadata`

---

## Entity Models in AI4NGExperiments

### A) Task (Reusable Definition)

**PK**: `TASK#<TASK_KEY>`  
**SK**: `METADATA`  

Key points:
- `TaskKey` is canonical, uppercase snake.
- `TaskData` includes:
  - `Name`, `Type`, `Description`
  - `QuestionnaireIds` (canonical list)
  - `Configuration` (freeform)
  - `EstimatedDuration`
- Validation rules:
  - Training/NeuroGame => QuestionnaireIds must be empty
  - Questionnaire => exactly 1
  - QuestionnaireSet => 1+
- Questionnaire existence validated against AI4NGQuestionnaires on create/update.

---

### B) Experiment (Protocol Container)

**PK**: `EXPERIMENT#<experiment_id>`  
**SK**: `METADATA`  
**type**: `Experiment`

`data` contains:
- `name`, `description`
- `status` (Draft/Active/Paused/Closed)
- optional protocol parameters (durationDays, start windows, etc.)

**Note:** The experiment does **not** embed per-day sessions. Instead it defines a small set of Protocol Sessions (below) that are shared across participants.

---

### C) Protocol Session (Global Schedule Shared by All Participants)

**PK**: `EXPERIMENT#<experiment_id>`  
**SK**: `PROTOCOL_SESSION#<protocol_session_key>` (e.g., FIRST / DAILY / WEEKLY)  
**type**: `ProtocolSession`

`data` contains:
- `protocolSessionKey` (FIRST/DAILY/WEEKLY)
- `name`, `description`
- `cadence`: `once | daily | weekly`
- `taskSequence`: ordered list of **TaskKeys**
- optional windowing: e.g., `windowStartLocal`, `windowEndLocal`
- `estimatedDuration` (optional convenience)

This makes the protocol explicit, stable, and academically interpretable.

---

### D) Membership / Enrolment

**PK**: `EXPERIMENT#<experiment_id>`  
**SK**: `MEMBER#<cognito_sub>`  
**type**: `Membership`  
**GSI1PK**: `USER#<cognito_sub>`  
**GSI1SK**: `EXPERIMENT#<experiment_id>`

`data` contains:
- `role`: participant/researcher
- `status`: active/withdrawn/completed
- `cohort`
- `startDate`, `endDate`, `timezone`
- `pseudoId` (optional, for de-identification)
- optional scheduling seed/config

---

### E) Participant Session Occurrence (Participant-Specific)

**PK**: `PSO#<experiment_id>#<participant_id>`  
**SK**: `OCCURRENCE#<occurrence_key>`  
Examples of `occurrence_key`:
- `FIRST` (one-time)
- `DAILY#2026-03-05` (date-based)
- `WEEKLY#2026-W10` (ISO-week) or `WEEKLY#2026-03-04` (week anchor date)

**type**: `ParticipantSessionOccurrence`

`data` contains:
- `protocolSessionKey`: FIRST/DAILY/WEEKLY
- `scheduledAt` and/or `dateLocal`
- `windowStart`, `windowEnd` (optional)
- `status`: scheduled/available/in_progress/completed/missed/cancelled
- timestamps: `startedAt`, `endedAt`
- `taskState`: per-task status (pending/done/skipped) for the tasks in `taskSequence`
- `isRescheduled` flag (optional)

This is the execution record and the anchor for responses.

---

## Access Patterns

### Experiment Management (Researcher)
- Get experiment metadata:
  - `GetItem PK=EXPERIMENT#<id>, SK=METADATA`
- List experiments (newest first):
  - Query GSI1 where `GSI1PK="EXPERIMENT"` sort by `GSI1SK` descending
- Get protocol sessions for experiment:
  - Query `PK=EXPERIMENT#<id>` with `begins_with(SK,"PROTOCOL_SESSION#")`
- Manage membership:
  - Query `PK=EXPERIMENT#<id>` with `begins_with(SK,"MEMBER#")`

### Participant / Mobile Execution
- List user experiments:
  - Query GSI1 where `GSI1PK="USER#<sub>"`
- Get protocol (experiment + protocol sessions + required tasks):
  - Get experiment metadata
  - Query protocol sessions under experiment PK
  - BatchGet tasks referenced by `taskSequence` (TaskKeys → `TASK#<key>`)
- Get participant session occurrences for a date range:
  - Query `PK=PSO#<experiment_id>#<participant_id>` with `SK` range/prefix (date-based keys recommended)
- Upload responses for a session occurrence:
  - Put into AI4NGResponses using occurrence-centric PK

### Tasks
- Get task:
  - GetItem `PK=TASK#<taskKey>, SK=METADATA`
- List tasks (newest first):
  - Query GSI1 where `GSI1PK="TASK"` descending, exclude deleted

### Mobile Delta Sync (illustrative)
- Sync membership + experiments relevant to user:
  - Query GSI3 `USER#<sub>#SYNC#EXPERIMENT` where `GSI3SK > lastSync`
- Sync participant session occurrences:
  - Query GSI3 `USER#<sub>#SYNC#PSO` where `GSI3SK > lastSync`
- Sync tasks:
  - Query GSI3 `USER#<sub>#SYNC#TASK` where `GSI3SK > lastSync`
- Sync responses:
  - Query responses table GSI3 `USER#<sub>#SYNC#RESPONSE` where `GSI3SK > lastSync`

---

## Data Model Examples

### Experiment Item
```json
{
  "PK": "EXPERIMENT#EXP_001",
  "SK": "METADATA",
  "GSI1PK": "EXPERIMENT",
  "GSI1SK": "2023-11-01T09:00:00Z",
  "type": "Experiment",
  "data": {
    "name": "BCI Learning Study",
    "description": "A 21-day study on BCI skill acquisition",
    "status": "Active"
  },
  "createdAt": "2023-11-01T09:00:00Z",
  "updatedAt": "2023-11-01T09:00:00Z",
  "syncMetadata": {
    "version": 1,
    "lastModified": "2023-11-01T09:00:00Z",
    "isDeleted": false
  }
}
```

### Protocol Session Item (DAILY)
```json
{
  "PK": "EXPERIMENT#EXP_001",
  "SK": "PROTOCOL_SESSION#DAILY",
  "type": "ProtocolSession",
  "data": {
    "protocolSessionKey": "DAILY",
    "name": "Daily Training Session",
    "description": "Daily training + post-session questionnaires",
    "cadence": "daily",
    "taskSequence": ["TRAIN_EEG", "POST_SESSION_QUESTIONS"],
    "estimatedDuration": 25,
    "windowStartLocal": "09:00",
    "windowEndLocal": "21:00"
  },
  "createdAt": "2023-11-01T09:00:00Z",
  "updatedAt": "2023-11-01T09:00:00Z",
  "syncMetadata": {
    "version": 1,
    "lastModified": "2023-11-01T09:00:00Z",
    "isDeleted": false
  }
}
```

### Participant Session Occurrence Item (DAILY on date)
```json
{
  "PK": "PSO#EXP_001#0b8b7c42-3f8b-4f22-9b7a-2f4f4b8a7e5a",
  "SK": "OCCURRENCE#DAILY#2023-11-07",
  "type": "ParticipantSessionOccurrence",
  "data": {
    "protocolSessionKey": "DAILY",
    "occurrenceKey": "DAILY#2023-11-07",
    "dateLocal": "2023-11-07",
    "status": "completed",
    "startedAt": "2023-11-07T10:00:00Z",
    "endedAt": "2023-11-07T10:25:00Z",
    "taskState": [
      { "order": 1, "taskKey": "TRAIN_EEG", "status": "done" },
      { "order": 2, "taskKey": "POST_SESSION_QUESTIONS", "status": "done" }
    ],
    "metadata": {
      "dayOfStudy": 5,
      "weekOfStudy": 1,
      "isRescheduled": false
    }
  },
  "createdAt": "2023-11-07T08:00:00Z",
  "updatedAt": "2023-11-07T10:25:00Z",
  "syncMetadata": {
    "version": 1,
    "lastModified": "2023-11-07T10:25:00Z",
    "modifiedBy": "0b8b7c42-3f8b-4f22-9b7a-2f4f4b8a7e5a",
    "clientId": "mobile-app-123",
    "isDeleted": false
  }
}
```

### Membership Item (Participant)
```json
{
  "PK": "EXPERIMENT#EXP_001",
  "SK": "MEMBER#0b8b7c42-3f8b-4f22-9b7a-2f4f4b8a7e5a",
  "GSI1PK": "USER#0b8b7c42-3f8b-4f22-9b7a-2f4f4b8a7e5a",
  "GSI1SK": "EXPERIMENT#EXP_001",
  "type": "Membership",
  "role": "participant",
  "status": "active",
  "assignedAt": "2023-11-01T09:00:00Z",
  "cohort": "A",
  "startDate": "2023-11-02",
  "endDate": "2023-11-23",
  "timezone": "Europe/London",
  "pseudoId": "P-7GQ2K1",
  "syncMetadata": {
    "version": 1,
    "lastModified": "2023-11-01T09:00:00Z",
    "isDeleted": false
  }
}
```

### Response Item (Questionnaire Answer within an Occurrence)
```json
{
  "PK": "RESP#EXP_001#0b8b7c42-3f8b-4f22-9b7a-2f4f4b8a7e5a#DAILY#2023-11-07",
  "SK": "2023-11-07T10:05:30.123Z#POST_SESSION_QUESTIONS#PQ#1",
  "GSI1PK": "USER#0b8b7c42-3f8b-4f22-9b7a-2f4f4b8a7e5a",
  "GSI1SK": "2023-11-07T10:05:30.123Z#EXP_001#DAILY#2023-11-07",
  "GSI2PK": "EXPERIMENT#EXP_001",
  "GSI2SK": "2023-11-07T10:05:30.123Z#0b8b7c42-3f8b-4f22-9b7a-2f4f4b8a7e5a#DAILY#2023-11-07",
  "GSI3PK": "USER#0b8b7c42-3f8b-4f22-9b7a-2f4f4b8a7e5a#SYNC#RESPONSE",
  "GSI3SK": "2023-11-07T10:05:30.123Z",
  "experimentId": "EXP_001",
  "participantId": "0b8b7c42-3f8b-4f22-9b7a-2f4f4b8a7e5a",
  "occurrenceKey": "DAILY#2023-11-07",
  "protocolSessionKey": "DAILY",
  "taskKey": "POST_SESSION_QUESTIONS",
  "questionnaireId": "PQ",
  "questionId": "1",
  "questionText": "Time seemed to go by",
  "answerValue": "7",
  "answerType": "scale",
  "timestamp": "2023-11-07T10:05:30.123Z",
  "syncMetadata": {
    "version": 1,
    "lastModified": "2023-11-07T10:05:30.123Z",
    "isDeleted": false
  }
}
```

---

## Design Benefits

- **Academic clarity**: protocol-level entities are distinct from participant-level enactment.
- **Reusability**: tasks are reusable definitions; protocol sessions reuse tasks.
- **Scalability**: participant occurrences and responses are partitioned by participant, avoiding hot experiment partitions.
- **Query-first**: experiments/tasks lists use GSI1; mobile sync uses GSI3; occurrences are queryable by date keys.
- **Historical integrity**: responses snapshot question text and key metadata.
- **Robust mobile sync**: `syncMetadata` supports delta sync and offline batching.

---

## Implementation Guidelines

1) **Protocol Authoring**
- Create tasks (validated against questionnaire table as needed)
- Create experiment metadata
- Create/Update three Protocol Sessions (FIRST/DAILY/WEEKLY) with `taskSequence`

2) **Enrolment**
- Add membership items per user with start date/timezone/cohort

3) **Occurrence Generation**
- On enrolment or on-demand, derive participant occurrences for FIRST/DAILY/WEEKLY across the study window
- Store per-occurrence state and taskState list (materialised from the protocol session taskSequence)

4) **Execution and Data Capture**
- Mobile queries occurrences, fetches referenced tasks (batch get)
- Mobile uploads responses keyed by occurrence
