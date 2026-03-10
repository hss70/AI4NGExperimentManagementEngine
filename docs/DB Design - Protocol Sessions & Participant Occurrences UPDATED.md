# BCI Research Platform Database Design (AI4NG)

## Overview

This document defines the DynamoDB data model for the AI4NG Brain–Computer Interface (BCI) research platform. The platform manages:

1) **Tasks**: reusable definitions representing training blocks, neurogames, and questionnaires (questionnaires are treated as tasks).  
2) **Experiments**: study-level metadata and **protocol** definitions.  
3) **Participants**: membership/enrolment into experiments (with optional per-participant scheduling overrides).  
4) **Participant Session Occurrences**: participant-specific scheduled/actual execution records derived from the protocol (plus ad-hoc sessions such as demo runs).  
5) **Responses**: participant outputs for questionnaire items and other task results.

The design emphasises **query-first access patterns**, scalability, offline/mobile synchronisation (delta sync), and historical integrity.

---

## Terminology

- **Experiment Protocol**: the canonical specification of what should occur in the study.
- **Session Type (Template)**: “what a session looks like” (task sequence template, estimated duration). Stored inside `Experiment.data.sessionTypes`.
- **Protocol Session (Rule)**: “when/how sessions run” (cadence, ordering, ad-hoc allowances), referencing a session type. Stored as separate items under the experiment partition.
- **Participant Session Occurrence**: a participant-specific instantiation of a protocol session at a scheduled date/time (or ad-hoc runtime), with execution state and a snapshot of the task sequence used for that run.

---

## Table Structure

## 1. AI4NGExperiments (Main Table)

Stores **experiments**, **protocol sessions**, **tasks**, **memberships**, and **participant session occurrences** in a single-table design.

### Key Structure

**PK (Partition Key)** identifies an entity collection:

- `EXPERIMENT#<experiment_id>`
- `TASK#<task_key>`
- `OCCURRENCE#<experiment_id>#<participant_id>` (participant’s occurrences collection)

**SK (Sort Key)** identifies a specific item within a partition:

- `METADATA`
- `PROTOCOL_SESSION#<protocol_key>`
- `MEMBER#<cognito_sub>`
- `<occurrence_key>` (within the occurrence partition)

### Occurrence Key Formats

The `<occurrence_key>` is **not** prefixed with `OCCURRENCE#` (because the PK already scopes to occurrences). Recommended formats:

- One-time scheduled: `FIRST`
- Daily scheduled: `DAILY#YYYY-MM-DD`
- Weekly scheduled (ISO week): `WEEKLY#YYYY-Www`
- Ad-hoc / demo multi-run: `DEMO#YYYY-MM-DDTHH:mm:ssZ#<shortId>` (or another unique suffix)

This supports multiple runs per day for ad-hoc/demo sessions while keeping scheduled sessions deterministic.

---

## Indexes

### Implemented (current)
**GSI1PK / GSI1SK** — list pages and “membership by user” (query-only; no scans)

- Experiments list:  
  - `GSI1PK="EXPERIMENT"`, `GSI1SK=<createdAtIso>`
- Tasks list:  
  - `GSI1PK="TASK"`, `GSI1SK=<createdAtIso>`
- Membership-by-user:  
  - `GSI1PK="USER#<cognito_sub>"`, `GSI1SK="EXPERIMENT#<experiment_id>"`

> Note: Your current CloudFormation/SAM snippet shows **only GSI1**. If/when you add additional GSIs, update the template and this doc together.

### Planned (recommended for mobile delta sync)
If you need user-scoped delta sync across entities, add a “last modified” index (example naming `GSI3`):

- `GSI3PK="USER#<cognito_sub>#SYNC#EXPERIMENT"`, `GSI3SK=<lastModifiedIso>`  
- `GSI3PK="USER#<cognito_sub>#SYNC#OCCURRENCE"`, `GSI3SK=<lastModifiedIso>`  
- `GSI3PK="USER#<cognito_sub>#SYNC#TASK"`, `GSI3SK=<lastModifiedIso>`

This is optional until you actually implement client delta sync.

---

## Common Attributes (convention)

- `type`: entity type (`Experiment`, `ProtocolSession`, `Task`, `Membership`, `ParticipantSessionOccurrence`)
- `createdAt`, `updatedAt`, `createdBy`, `updatedBy` (audit)
- `syncMetadata` (optional / future): `{ version, lastModified, isDeleted, modifiedBy, clientId, batchId? }`

---

## 2. AI4NGQuestionnaires (Questionnaire Definitions Table)

Stores reusable questionnaire definitions and configurations.

### Key Structure
- **PK**: `QUESTIONNAIRE#<questionnaire_id>`
- **SK**: `CONFIG`

### Attributes
- `type`: `Questionnaire`
- `data`: questionnaire definition and question list
- `createdAt`, `updatedAt`
- `syncMetadata` (optional)

> If questionnaires are treated purely as **Tasks** that reference this table, this remains a stable “definition store”.

---

## 3. AI4NGResponses (Answers / Outputs Table)

Stores participant outputs for questionnaire questions and other task results.

### Recommended Key Structure (occurrence-centric)
- **PK**: `RESP#<experiment_id>#<participant_id>#<occurrence_key>`
- **SK**: `<timestampIso>#<task_key>#<response_id_or_question_id>`

### Optional Indexes (examples)
- **GSI1**: query outputs by participant across experiments  
  - `GSI1PK="USER#<participant_id>"`, `GSI1SK=<timestampIso>#<experiment_id>#<occurrence_key>`
- **GSI2**: query outputs by experiment across participants (analysis)  
  - `GSI2PK="EXPERIMENT#<experiment_id>"`, `GSI2SK=<timestampIso>#<participant_id>#<occurrence_key>`
- **GSI3**: mobile delta sync  
  - `GSI3PK="USER#<participant_id>#SYNC#RESPONSE"`, `GSI3SK=<lastModifiedIso>`

### Attributes
- Identifiers: `experimentId`, `participantId`, `occurrenceKey`, `protocolKey?`, `taskKey`, `questionnaireId?`, `questionId?`
- Historical integrity: `questionText` and optional `answerOptions` snapshot (for questionnaire items)
- Payload: `answerValue` / `payload` (freeform JSON for non-questionnaire tasks)
- `timestamp`, `responseTimeMs`, `metadata` (device/app version/etc.)
- `syncMetadata`

---

# Entity Models in AI4NGExperiments

## A) Task (Reusable Definition)

**PK**: `TASK#<TASK_KEY>`  
**SK**: `METADATA`  

Key points:
- `taskKey` is canonical (recommend **UPPER_SNAKE_CASE**).
- `TaskData` includes:
  - `name`, `type`, `description`
  - `estimatedDurationMinutes`
  - `configuration` (freeform JSON; used for questionnaire linkage, parameters, etc.)

### Questionnaire-as-task convention
If a task represents a questionnaire:
- `type = "Questionnaire"` (or `"QuestionnaireSet"`)
- `configuration` contains `questionnaireId` (or `questionnaireIds`)

Validation rules (suggested):
- Training/NeuroGame tasks: must **not** include questionnaire linkage fields
- Questionnaire task: must include **exactly one** `questionnaireId`
- QuestionnaireSet task: must include `questionnaireIds` with **1+** ids
- Questionnaire existence validated against `AI4NGQuestionnaires` on create/update (when applicable)

---

## B) Experiment (Protocol Container)

**PK**: `EXPERIMENT#<experiment_id>`  
**SK**: `METADATA`  
**type**: `Experiment`

### Top-level attributes (operational)
- `status`: `Draft | Active | Paused | Closed`  ✅ (stored top-level for quick list projection)
- audit fields

### `data` (definition/configuration)
- `name`, `description`
- optional study window:
  - `studyStartDate` (YYYY-MM-DD)
  - `studyEndDate` (nullable for ongoing)
  - `enrolmentStartDate`, `enrolmentEndDate` (optional)
- optional participant schedule defaults:
  - `participantDurationDays` (e.g., 56)
- `sessionTypes`: dictionary of templates keyed by session type key (e.g., `FIRST`, `DAILY`, `WEEKLY`, `DEMO`)
  - each session type contains ordered `tasks` + estimated duration

> The experiment **does not** embed scheduled occurrences. Those are derived into participant occurrence partitions.

---

## C) Protocol Session (Rule Layer)

**PK**: `EXPERIMENT#<experiment_id>`  
**SK**: `PROTOCOL_SESSION#<protocol_key>`  
**type**: `ProtocolSession`

This is the global “rule” for generating/allowing occurrences. It references an existing session type template.

`data` contains (minimum):
- `protocolKey`: e.g., `FIRST`, `DAILY`, `WEEKLY`, `DEMO`
- `sessionTypeKey`: key into `Experiment.data.sessionTypes`
- `order`: integer for display/flow ordering (FIRST before DAILY, etc.)
- `cadenceType`: `ONCE | DAILY | WEEKLY | ADHOC`
- `maxPerDay`: nullable int
  - scheduled sessions typically `1`
  - demo/ad-hoc sessions can be `> 1` (or null meaning “unlimited”)
- optional windowing:
  - `windowStartLocal` / `windowEndLocal`
- optional weekly params:
  - `weekday` (0–6 or Mon–Sun) or `isoWeekAnchor` rule

This supports both “normal study protocol” and conference/demo sessions that can be repeated frequently.

---

## D) Membership / Enrolment

**PK**: `EXPERIMENT#<experiment_id>`  
**SK**: `MEMBER#<cognito_sub>`  
**type**: `Membership`  
**GSI1PK**: `USER#<cognito_sub>`  
**GSI1SK**: `EXPERIMENT#<experiment_id>`

Attributes (recommended):
- `role`: `participant | researcher`
- `status`: `active | paused | withdrawn | completed`
- `cohort`: string
- `assignedAt` (ISO)
- optional schedule overrides:
  - `participantStartDate` (YYYY-MM-DD)
  - `participantEndDate` (YYYY-MM-DD)
  - `participantDurationDaysOverride`
  - `timezone`
- `pseudoId` (optional de-identification)

---

## E) Participant Session Occurrence (Participant-Specific)

**PK**: `OCCURRENCE#<experiment_id>#<participant_id>`  
**SK**: `<occurrence_key>`  
**type**: `ParticipantSessionOccurrence`

`data` contains:
- `protocolKey`: links back to `ProtocolSession` (`FIRST`, `DAILY`, `DEMO`, etc.)
- `occurrenceKey`: echo of SK (optional convenience)
- scheduling fields:
  - `scheduledAt` (ISO) and/or `dateLocal` (YYYY-MM-DD)
  - optional local windowing: `windowStartLocal`, `windowEndLocal`
- execution status:
  - `status`: `scheduled | available | in_progress | completed | missed | cancelled`
  - `startedAt`, `endedAt`
- snapshot fields (to preserve historical integrity even if protocol changes later):
  - `taskSequence`: ordered list of `TaskKey`
  - `estimatedDurationMinutes` (optional snapshot)
- per-task state (optional):
  - `taskState`: list or map keyed by taskKey with `pending/done/skipped`

This is the execution record and the anchor for responses.

---

# Access Patterns

## Researcher: Experiments
- Get experiment metadata:
  - `GetItem PK=EXPERIMENT#<id>, SK=METADATA`
- List experiments (newest first):
  - Query GSI1 where `GSI1PK="EXPERIMENT"` order by `GSI1SK` descending
- Get protocol sessions for experiment:
  - Query `PK=EXPERIMENT#<id>` with `begins_with(SK,"PROTOCOL_SESSION#")`
- Manage membership:
  - Query `PK=EXPERIMENT#<id>` with `begins_with(SK,"MEMBER#")`

## Participant: Execution
- List user experiments:
  - Query GSI1 where `GSI1PK="USER#<sub>"`
- Get protocol bundle (experiment + protocol sessions + required tasks):
  - Get experiment metadata
  - Query protocol sessions under experiment PK
  - From protocol sessions, resolve `sessionTypeKey` → template → `taskSequence`
  - BatchGet tasks referenced by all sequences (`TASK#<key>`)
- List participant occurrences:
  - Query `PK=OCCURRENCE#<experiment_id>#<participant_id>`
  - Optional: use SK prefix/range filters if you standardize occurrence keys by date
- Start/complete an occurrence:
  - Update occurrence status + timestamps (conditional updates)
- Upload responses for an occurrence:
  - Put items into `AI4NGResponses` using occurrence-centric PK

## Tasks
- Get task:
  - GetItem `PK=TASK#<taskKey>, SK=METADATA`
- List tasks:
  - Query GSI1 where `GSI1PK="TASK"` order by `GSI1SK` descending

## Mobile Delta Sync (optional / future)
If you add GSI3 (or equivalent), sync by `lastModified` per user scope.

---

# Data Model Examples

## Experiment Item
```json
{
  "PK": "EXPERIMENT#EXP_001",
  "SK": "METADATA",
  "GSI1PK": "EXPERIMENT",
  "GSI1SK": "2026-02-25T10:29:13Z",
  "type": "Experiment",
  "status": "Active",
  "data": {
    "name": "BCI Learning Study",
    "description": "A 21-day study on BCI skill acquisition",
    "participantDurationDays": 21,
    "sessionTypes": {
      "DAILY": {
        "name": "Daily Training",
        "tasks": ["TASK#TRAIN_EEG", "TASK#POST_Q"],
        "estimatedDurationMinutes": 25
      }
    }
  },
  "createdAt": "2026-02-25T10:29:13Z",
  "updatedAt": "2026-02-25T10:29:13Z"
}
```

## Protocol Session Item (DAILY)
```json
{
  "PK": "EXPERIMENT#EXP_001",
  "SK": "PROTOCOL_SESSION#DAILY",
  "type": "ProtocolSession",
  "data": {
    "protocolKey": "DAILY",
    "sessionTypeKey": "DAILY",
    "order": 2,
    "cadenceType": "DAILY",
    "maxPerDay": 1,
    "windowStartLocal": "09:00",
    "windowEndLocal": "21:00"
  },
  "createdAt": "2026-02-25T10:29:13Z",
  "updatedAt": "2026-02-25T10:29:13Z"
}
```

## Participant Session Occurrence Item (DAILY on date)
```json
{
  "PK": "OCCURRENCE#EXP_001#0b8b7c42-3f8b-4f22-9b7a-2f4f4b8a7e5a",
  "SK": "DAILY#2026-03-05",
  "type": "ParticipantSessionOccurrence",
  "data": {
    "protocolKey": "DAILY",
    "occurrenceKey": "DAILY#2026-03-05",
    "dateLocal": "2026-03-05",
    "status": "completed",
    "startedAt": "2026-03-05T10:00:00Z",
    "endedAt": "2026-03-05T10:25:00Z",
    "taskSequence": ["TASK#TRAIN_EEG", "TASK#POST_Q"]
  },
  "createdAt": "2026-03-05T08:00:00Z",
  "updatedAt": "2026-03-05T10:25:00Z"
}
```

## Participant Session Occurrence Item (DEMO multi-run)
```json
{
  "PK": "OCCURRENCE#EXP_DEMO#anon-visitor-001",
  "SK": "DEMO#2026-03-05T10:15:23Z#A1B2",
  "type": "ParticipantSessionOccurrence",
  "data": {
    "protocolKey": "DEMO",
    "status": "completed",
    "startedAt": "2026-03-05T10:15:23Z",
    "endedAt": "2026-03-05T10:19:50Z",
    "taskSequence": ["TASK#DEMO_GAME", "TASK#DEMO_Q"]
  },
  "createdAt": "2026-03-05T10:15:23Z",
  "updatedAt": "2026-03-05T10:19:50Z"
}
```

---

# Implementation Guidelines

1) **Protocol Authoring**
- Create tasks (validate questionnaire linkage where applicable)
- Create experiment metadata (`status = Draft` on create)
- Upsert protocol sessions (DAILY/WEEKLY/FIRST/DEMO etc.) referencing sessionTypes

2) **Enrolment**
- Add membership items per user with cohort and optional startDate/timezone
- Optionally generate occurrences on enrolment

3) **Occurrence Generation**
- Derive participant occurrences from protocol sessions across participant duration/window
- For ADHOC/DEMO protocols, create occurrences on demand, ensuring uniqueness per run

4) **Execution and Data Capture**
- Client queries occurrences; batch-gets tasks referenced by `taskSequence`
- Client uploads responses keyed by occurrence

