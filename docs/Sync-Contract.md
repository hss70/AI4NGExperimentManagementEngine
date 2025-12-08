# Experiment Sync API Contract (v1)

This document defines the contract for synchronizing a participant's view of a single experiment.

Endpoint
- Method: GET
- Path: /api/experiments/{experimentId}/sync
- Auth: Bearer token (participant or researcher). Participant sees only experiments they are a member of.

Query parameters
- lastSyncTime (optional, ISO 8601 UTC): Timestamp for incremental sync semantics.
  - Current behavior: Ignored; the API returns a full payload. Future versions may filter to changes after lastSyncTime based on updatedAt fields.

Response: 200 OK
The response is a single JSON object that contains the experiment, its sessions (with canonical task order), all tasks referenced by those sessions, the set of questionnaire IDs needed on the client, and the server-issued sync timestamp.

Schema
- experiment: object | null
  - id: string
  - data: object  // ExperimentData (opaque to client, may include name, description, status, sessionTypes, questionnaireIds, etc.)
  - questionnaireConfig: object | null // Scheduling/assignment configuration (opaque)
  - updatedAt: string | null // ISO 8601 UTC
- sessions: Session[]
  - sessionId: string // Typically a date (e.g., "YYYY-MM-DD") or provider-defined identifier
  - data: object      // SessionData (opaque to client)
  - taskOrder: string[] // Canonical source of truth for task ordering. Each entry is "TASK#{taskId}".
  - createdAt: string | null // ISO 8601 UTC
  - updatedAt: string | null // ISO 8601 UTC
- tasks: Task[]
  - id: string // Task identifier
  - data: object // TaskData (name, type, description, configuration, estimatedDuration, etc.)
  - createdAt: string | null // ISO 8601 UTC
  - updatedAt: string | null // ISO 8601 UTC
- questionnaires: string[]
  - Set of questionnaire IDs required by the experiment for the client to cache locally.
  - Derived from experiment.data.questionnaireIds and task data (configuration.questionnaireId or flat questionnaireId).
- syncTimestamp: string // ISO 8601 UTC issued by server to represent sync time

Canonical rules
- Session task order
  - The top-level `taskOrder` array on each session is the single authoritative ordering. Clients should ignore any `TaskOrder` nested inside `data` if present for backward compatibility.
  - Each entry MUST be a string in the form `TASK#{taskId}`.
- Opaque data payloads
  - `experiment.data`, `experiment.questionnaireConfig`, `session.data`, and `task.data` are treated as opaque by the contract. Clients should not rely on internal shapes beyond documented keys they own.

Error responses
- 401 Unauthorized: Missing/invalid credentials
- 403 Forbidden: Participant is not a member of the experiment
- 404 Not Found: Experiment does not exist or is not accessible
- 5xx: Server errors

Example (abbreviated)
{
  "experiment": {
    "id": "EXP-20251208204526",
    "data": { "Name": "harness-exp-20251208204526", "Status": "active" },
    "questionnaireConfig": { "Schedule": {} },
    "updatedAt": null
  },
  "sessions": [
    {
      "sessionId": "2025-12-08",
      "data": { "Status": "updated" },
      "taskOrder": [
        "TASK#bdda971a-81a6-4097-aa04-11a6665d63e9",
        "TASK#566beedb-77b5-4b29-bdef-6e5b9a566047",
        "TASK#64d3dc79-5ab3-4491-a5a5-738fc37437e2",
        "TASK#d0241dd9-86a1-430f-949b-a560b348de2d",
        "TASK#9f62c080-1a39-4bcb-8d65-8a27b31a3236"
      ],
      "createdAt": "2025-12-08T20:45:27.882Z",
      "updatedAt": "2025-12-08T20:45:28.047Z"
    }
  ],
  "tasks": [
    {
      "id": "bdda971a-81a6-4097-aa04-11a6665d63e9",
      "data": { "Name": "EEG Training", "Type": "training", "EstimatedDuration": 300 },
      "createdAt": "2025-12-08T20:40:00.000Z",
      "updatedAt": "2025-12-08T20:42:00.000Z"
    }
  ],
  "questionnaires": ["PQ", "PreState"],
  "syncTimestamp": "2025-12-08T20:45:28.185Z"
}

Client guidance
- Daily bootstrap: Call `GET /api/me/experiments` to list experiments for the user, then call `GET /api/experiments/{id}/sync` for each to fully populate local cache.
- Incremental sync: When supported, pass `lastSyncTime` to fetch only changes (fields with `updatedAt > lastSyncTime`). Until then, treat sync as a full refresh.
- Task lookup: Use `sessions[*].taskOrder` to drive the UI sequence, then hydrate task details by mapping `TASK#{taskId}` to the items in `tasks`.
- Questionnaire prefetch: Use the `questionnaires` set to prefetch/cache questionnaire configs from the questionnaires API.

Versioning
- Contract version: v1. Breaking changes will be introduced under a negotiated version or additive fields.
- Additive changes (new fields) may occur; clients should ignore unknown fields.
