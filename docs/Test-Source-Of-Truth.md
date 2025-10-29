# AI4NG Question Engine - Test Source of Truth

## Business Rules & Expected Behaviors

### Questionnaire Management

#### ✅ Data Format Validation
- **PK Format**: Must be `QUESTIONNAIRE#{id}`
- **SK Format**: Must be `METADATA`
- **Type Field**: Must be `"Questionnaire"`
- **Data Structure**: Must contain `name` (required), `description` (optional), `questions` array
- **Question Structure**: Each question must have `id`, `text`, `type`, `required` fields
- **ID Uniqueness**: Questionnaire IDs must be unique across the system

#### ✅ Business Logic Rules
- **Duplicate Prevention**: Cannot create questionnaire with existing ID
- **Name Validation**: Questionnaire name cannot be empty or whitespace only
- **Question ID Uniqueness**: Question IDs must be unique within a questionnaire
- **Required Field Validation**: All required fields must be present and valid
- **Researcher Authorization**: Only researchers can create/update/delete questionnaires
 - **Serialization Integrity**: Questionnaire questions with nested scale objects must serialize/deserialize without data loss (e.g., scale.min/scale.max)
 - **Batch Import Behavior**: Batch uploads must process all entries, producing a summary with processed/success/failed counts; failures in one entry must not stop others
 - **Update Expression Correctness**: Updates must map fields to the correct DynamoDB attributes preserving existing audit metadata

### Experiment Management

#### ✅ Data Format Validation
- **PK Format**: Must be `EXPERIMENT#{experimentId}`
- **SK Formats**: 
  - Metadata: `METADATA`
  - Sessions: `SESSION#{sessionId}`
  - Members: `MEMBER#{userSub}`
- **Type Fields**: `"Experiment"`, `"Session"`, `"Member"`
- **GSI1 Structure**: For sessions - `GSI1PK: EXPERIMENT#{experimentId}`, `GSI1SK: SESSION#{sessionId}`

#### ✅ Business Logic Rules
- **Questionnaire Existence Check**: All questionnaire IDs in `questionnaireConfig` must exist before experiment creation
- **Session Validation**: Session IDs must be unique within an experiment
- **Member Role Validation**: Member roles must be valid (`researcher`, `participant`)
- **Experiment Access Control**: Only experiment creators and members can access experiment data
- **Session Sync Validation**: Cannot sync sessions for non-existent experiments
- **Member Management**: Only authenticated users can modify members; roles limited to `researcher` and `participant`; removing a non-existent member should be idempotent/no-op

### Response Management

#### ✅ Data Format Validation
- **PK Format**: Must be `RESPONSE#{responseId}`
- **SK Format**: Must be `METADATA`
- **Type Field**: Must be `"Response"`
- **GSI1 Structure**: `GSI1PK: EXPERIMENT#{experimentId}`, `GSI1SK: SESSION#{sessionId}`
- **Response Data**: Must contain `experimentId`, `sessionId`, `questionnaireId`, `responses` array

#### ✅ Business Logic Rules
- **Experiment Existence**: Cannot create response for non-existent experiment
- **Session Existence**: Cannot create response for non-existent session
- **Questionnaire Validation**: Response questionnaire ID must match experiment's questionnaire config
- **Question Answer Validation**: All required questions must have answers
- **Answer Type Validation**: Answer types must match question types

### Cross-Entity Validation

#### ✅ Referential Integrity
- **Experiment → Questionnaire**: Experiments can only reference existing questionnaires
- **Response → Experiment**: Responses can only reference existing experiments
- **Response → Session**: Responses can only reference existing sessions within the experiment
- **Response → Questionnaire**: Response questionnaire must be in experiment's questionnaire config

#### ✅ Cascade Operations
- **Questionnaire Deletion**: Should prevent deletion if referenced by experiments
- **Experiment Deletion**: Should handle cleanup of associated sessions and responses
- **Session Deletion**: Should handle cleanup of associated responses

## Implemented Test Coverage

### ✅ DynamoDB Format Tests
- ✅ Validate exact PK/SK format in database
- ✅ Verify GSI3 structure for questionnaires
- ✅ Check attribute types match expected schema
- ✅ Validate timestamp formats (ISO 8601)
- ✅ Test syncMetadata structure for mobile sync

### ✅ Business Rule Tests
- ✅ **Questionnaire Existence Check**: Prevent experiment creation with invalid questionnaire IDs
- ✅ **Duplicate Prevention**: Reject duplicate questionnaire/experiment IDs
- ✅ **Referential Integrity**: Validate cross-entity references
- ✅ **Required Field Validation**: Test all required field combinations
- ✅ **Data Type Validation**: Ensure correct data types in DynamoDB

### ✅ Authorization Tests
- ✅ **Role-Based Access**: Test researcher vs participant permissions
- ✅ **Resource Ownership**: Users can only access their own data
- ✅ **JWT Claims Validation**: Test different JWT claim scenarios
- ✅ **Path-Based Authorization**: Validate `/researcher/` path restrictions

### 🔴 Edge Case Tests
- [ ] **Large Payload Handling**: Test maximum questionnaire/response sizes
- [ ] **Special Characters**: Test Unicode, emojis, special chars in names/descriptions
- [ ] **Concurrent Operations**: Test race conditions in creation/updates
- [ ] **Malformed JSON**: Test invalid JSON payload handling
- [ ] **SQL Injection Attempts**: Test malicious input sanitization

### 🔴 Performance & Limits Tests
- [ ] **Batch Size Limits**: Test maximum batch upload sizes
- [ ] **Query Pagination**: Test large result set handling
- [ ] **Timeout Handling**: Test long-running operations
- [ ] **Rate Limiting**: Test API throttling behavior

### 🔴 Data Consistency Tests
- [ ] **Eventually Consistent Reads**: Test GSI consistency
- [ ] **Transaction Rollback**: Test partial failure scenarios
- [ ] **Duplicate Detection**: Test idempotency keys
- [ ] **Audit Trail**: Test created/updated timestamp accuracy

## Test Data Scenarios

### Valid Test Cases
```json
{
  "validQuestionnaire": {
    "id": "valid-questionnaire-001",
    "data": {
      "name": "Valid Test Questionnaire",
      "description": "A properly formatted questionnaire",
      "questions": [
        {
          "id": "q1",
          "text": "What is your name?",
          "type": "text",
          "required": true
        }
      ]
    }
  },
  "validExperiment": {
    "data": {
      "name": "Valid Experiment",
      "description": "Properly configured experiment"
    },
    "questionnaireConfig": {
      "questionnaireIds": ["valid-questionnaire-001"]
    }
  }
}
```

### Invalid Test Cases
```json
{
  "invalidQuestionnaire": {
    "id": "",
    "data": {
      "name": "",
      "questions": []
    }
  },
  "experimentWithInvalidQuestionnaire": {
    "data": {
      "name": "Invalid Experiment"
    },
    "questionnaireConfig": {
      "questionnaireIds": ["non-existent-questionnaire"]
    }
  }
}
```

## Expected DynamoDB Schema

### Questionnaire Item
```json
{
  "PK": "QUESTIONNAIRE#questionnaire-id",
  "SK": "METADATA",
  "type": "Questionnaire",
  "data": {
    "name": "string",
    "description": "string",
    "questions": [
      {
        "id": "string",
        "text": "string", 
        "type": "text|number|select|boolean",
        "required": "boolean",
        "options": ["array", "for", "select", "type"]
      }
    ]
  },
  "createdBy": "string",
  "createdAt": "ISO8601",
  "updatedBy": "string",
  "updatedAt": "ISO8601"
}
```

### Experiment Item
```json
{
  "PK": "EXPERIMENT#experiment-id",
  "SK": "METADATA",
  "type": "Experiment",
  "data": {
    "name": "string",
    "description": "string"
  },
  "questionnaireConfig": {
    "questionnaireIds": ["array", "of", "questionnaire", "ids"]
  },
  "createdBy": "string",
  "createdAt": "ISO8601"
}
```

### Session Item
```json
{
  "PK": "EXPERIMENT#experiment-id",
  "SK": "SESSION#session-id",
  "type": "Session",
  "data": {
    "sessionId": "string",
    "participantId": "string",
    "startTime": "ISO8601",
    "endTime": "ISO8601",
    "status": "active|completed|cancelled"
  },
  "GSI1PK": "EXPERIMENT#experiment-id",
  "GSI1SK": "SESSION#session-id"
}
```

### Response Item
```json
{
  "PK": "RESPONSE#response-id",
  "SK": "METADATA", 
  "type": "Response",
  "data": {
    "experimentId": "string",
    "sessionId": "string",
    "questionnaireId": "string",
    "responses": [
      {
        "questionId": "string",
        "answer": "any",
        "timestamp": "ISO8601"
      }
    ]
  },
  "GSI1PK": "EXPERIMENT#experiment-id",
  "GSI1SK": "SESSION#session-id",
  "createdBy": "string",
  "createdAt": "ISO8601"
}
```