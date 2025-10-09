# AI4NG Question Engine - Test Cases Specification

## Questionnaires API Test Cases

### QuestionnaireService Tests

#### ✅ CreateAsync Tests
| Test Case | Input | Expected Output | Validates |
|-----------|-------|-----------------|-----------|
| `CreateAsync_ShouldReturnId_WhenValidRequest` | Valid questionnaire request | Returns questionnaire ID | Successful creation |
| `CreateAsync_ShouldThrowException_WhenInvalidData` | Invalid questionnaire data | Throws validation exception | Input validation |
| `CreateAsync_ShouldCallDynamoDB_WithCorrectParameters` | Valid request | DynamoDB PutItem called once | Database interaction |

#### ✅ GetAllAsync Tests  
| Test Case | Input | Expected Output | Validates |
|-----------|-------|-----------------|-----------|
| `GetAllAsync_ShouldReturnQuestionnaires_WhenDataExists` | Mock DynamoDB with data | List of questionnaires | Data retrieval |
| `GetAllAsync_ShouldReturnEmpty_WhenNoData` | Empty DynamoDB response | Empty list | Empty state handling |
| `GetAllAsync_ShouldHandleException_WhenDynamoDBFails` | DynamoDB exception | Propagates exception | Error handling |

#### ✅ GetByIdAsync Tests
| Test Case | Input | Expected Output | Validates |
|-----------|-------|-----------------|-----------|
| `GetByIdAsync_ShouldReturnQuestionnaire_WhenExists` | Valid questionnaire ID | Questionnaire object | Single item retrieval |
| `GetByIdAsync_ShouldReturnNull_WhenNotFound` | Non-existent ID | null | Not found handling |
| `GetByIdAsync_ShouldValidateId_WhenEmpty` | Empty/null ID | Validation exception | Input validation |

### QuestionnairesController Tests

#### ✅ Authentication Tests
| Test Case | Input | Expected Output | Validates |
|-----------|-------|-----------------|-----------|
| `GetUsernameFromJwt_ShouldReturnTestUser_InLocalMode` | Local environment | "testuser" | Local testing bypass |
| `GetUsernameFromJwt_ShouldThrowException_WhenNoAuthHeader` | Missing auth header | UnauthorizedAccessException | Auth validation |
| `GetUsernameFromJwt_ShouldThrowException_WhenInvalidToken` | Invalid JWT | UnauthorizedAccessException | Token validation |

#### ✅ CRUD Operations Tests
| Test Case | Input | Expected Output | Validates |
|-----------|-------|-----------------|-----------|
| `GetAll_ShouldReturnOk_WithQuestionnaires` | Valid request | 200 OK with data | GET endpoint |
| `Create_ShouldReturnOk_WhenValidRequest` | Valid questionnaire | 200 OK with ID | POST endpoint |
| `Create_ShouldReturnForbidden_WhenNotResearcher` | Non-researcher path | 403 Forbidden | Authorization |
| `GetById_ShouldReturnNotFound_WhenQuestionnaireDoesNotExist` | Invalid ID | 404 Not Found | Error responses |

## Experiments API Test Cases

### ExperimentService Tests

#### ✅ Experiment Management
| Test Case | Input | Expected Output | Validates |
|-----------|-------|-----------------|-----------|
| `CreateExperimentAsync_ShouldReturnId_WhenValid` | Valid experiment data | Experiment ID | Creation logic |
| `GetExperimentAsync_ShouldReturnExperiment_WhenExists` | Valid experiment ID | Experiment with sessions | Data retrieval |
| `SyncExperimentAsync_ShouldUpdateSessions_WhenValid` | Session data | Success response | Session management |

#### ✅ Member Management  
| Test Case | Input | Expected Output | Validates |
|-----------|-------|-----------------|-----------|
| `AddMemberAsync_ShouldAddMember_WhenValid` | Valid member data | Success | Member addition |
| `RemoveMemberAsync_ShouldRemoveMember_WhenExists` | Existing member | Success | Member removal |
| `GetExperimentMembersAsync_ShouldReturnMembers` | Experiment ID | List of members | Member listing |

### ExperimentsController Tests

#### ✅ Researcher Endpoints
| Test Case | Input | Expected Output | Validates |
|-----------|-------|-----------------|-----------|
| `CreateExperiment_ShouldReturnOk_WhenResearcher` | Researcher request | 200 OK | Researcher access |
| `CreateExperiment_ShouldReturnForbidden_WhenParticipant` | Participant request | 403 Forbidden | Access control |
| `UpdateExperiment_ShouldRequireAuth_WhenNoToken` | No auth header | 401 Unauthorized | Authentication |

## Responses API Test Cases

### ResponseService Tests

#### ✅ Response Management
| Test Case | Input | Expected Output | Validates |
|-----------|-------|-----------------|-----------|
| `CreateResponseAsync_ShouldReturnId_WhenValid` | Valid response data | Response ID | Response creation |
| `GetResponsesAsync_ShouldFilterByExperiment_WhenProvided` | Experiment filter | Filtered responses | Query filtering |
| `GetResponsesAsync_ShouldFilterBySession_WhenProvided` | Session filter | Session responses | Session filtering |

#### ✅ Data Validation
| Test Case | Input | Expected Output | Validates |
|-----------|-------|-----------------|-----------|
| `CreateResponseAsync_ShouldValidateRequired_Fields` | Missing required data | Validation exception | Required field validation |
| `UpdateResponseAsync_ShouldPreserveMetadata` | Update request | Preserves created date | Data integrity |

## Integration Test Cases

### End-to-End Workflows

#### ✅ Complete Questionnaire Workflow
| Step | Action | Expected Result | Validates |
|------|--------|-----------------|-----------|
| 1 | POST /api/questionnaires | 200 OK, returns ID | Questionnaire creation |
| 2 | GET /api/questionnaires/{id} | 200 OK, returns questionnaire | Retrieval by ID |
| 3 | PUT /api/questionnaires/{id} | 200 OK, success message | Update operation |
| 4 | DELETE /api/questionnaires/{id} | 200 OK, success message | Deletion |
| 5 | GET /api/questionnaires/{id} | 404 Not Found | Deletion verification |

#### ✅ Experiment-Response Workflow
| Step | Action | Expected Result | Validates |
|------|--------|-----------------|-----------|
| 1 | Create questionnaire | Returns questionnaire ID | Prerequisites |
| 2 | POST /api/researcher/experiments | 200 OK, returns experiment ID | Experiment creation |
| 3 | POST /api/experiments/{id}/sync | 200 OK, sessions synced | Session management |
| 4 | POST /api/responses | 200 OK, response created | Response collection |
| 5 | GET /api/responses?experimentId={id} | 200 OK, returns responses | Response retrieval |

### Error Handling Test Cases

#### ✅ HTTP Error Responses
| Scenario | Expected Status | Expected Response | Validates |
|----------|----------------|-------------------|-----------|
| Missing auth header | 401 Unauthorized | Error message | Authentication |
| Invalid JWT token | 401 Unauthorized | Token error | Token validation |
| Participant accessing researcher endpoint | 403 Forbidden | Access denied | Authorization |
| Resource not found | 404 Not Found | Not found message | Resource validation |
| Invalid request data | 400 Bad Request | Validation errors | Input validation |
| DynamoDB connection failure | 500 Internal Server Error | Server error | Error handling |

## Performance Test Cases

### Load Testing Scenarios
| Test Case | Load | Expected Response Time | Validates |
|-----------|------|----------------------|-----------|
| Get all questionnaires | 100 concurrent requests | < 2 seconds | Read performance |
| Create questionnaire | 50 concurrent requests | < 3 seconds | Write performance |
| Batch questionnaire upload | 10 batches of 20 items | < 10 seconds | Batch performance |

## Test Data Requirements

### Sample Test Data
```json
{
  "questionnaire": {
    "id": "test-questionnaire-001",
    "data": {
      "name": "Test Questionnaire",
      "description": "Sample questionnaire for testing",
      "questions": [
        {
          "id": "q1",
          "text": "Sample question?",
          "type": "text",
          "required": true
        }
      ]
    }
  }
}
```

### Test Environment Setup
- **DynamoDB Local**: Running on port 8000
- **Test Tables**: Isolated from production
- **Mock Authentication**: Returns "testuser"
- **Debug Logging**: Enabled for all test runs