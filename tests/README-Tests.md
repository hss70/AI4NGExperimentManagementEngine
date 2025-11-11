# AI4NG Experiment Management Tests

## Test Structure

### ExperimentServiceTests.cs
Core experiment functionality tests:
- ✅ Experiment CRUD operations
- ✅ Member management
- ✅ Sync functionality
- ✅ Updated questionnaire validation (sessionTypes + questionnaireConfig.schedule)

### SessionTaskTests.cs
Session and task management tests:
- ✅ Session creation and retrieval
- ✅ Complex BCI experiment creation (3 session types, 15 questionnaires)
- ✅ Multiple session type handling (DAILY, WEEKLY, TRAIT)
- ✅ Questionnaire integration validation
- ✅ Error handling for missing questionnaires

### TaskServiceTests.cs
Dedicated task management tests:
- ✅ Task CRUD operations
- ✅ Single questionnaire tasks (e.g., PreState)
- ✅ Batch questionnaire tasks (e.g., TRAIT_BANK)
- ✅ EEG training tasks (non-questionnaire)
- ✅ Different task types validation

## Key Test Coverage

### Complex Experiment Support
Tests validate complex BCI experiment structures:
- **Multiple session types**: DAILY (25min), WEEKLY (20min), TRAIT (115min)
- **15 unique questionnaires**: PreState, PhysicalState, CurrentState, EndState, PQ, TLX, IPAQ, VVIQ, ATI, FMI, MIQ-RS, Edinburgh, MentalRotation, 16PF5, IndexLearningStyles
- **Different task types**: questionnaire, questionnaire_batch, eeg_training
- **Complex scheduling**: daily, weekly, once, every_session

### Questionnaire Integration
Tests validate questionnaires from both:
- `experiment.Data.SessionTypes[].Questionnaires`
- `experiment.QuestionnaireConfig.Schedule`

### Session Management
- Session creation with experiment validation
- Session retrieval by ID
- Task ordering in sessions

### Task Management
- Task CRUD operations
- Task data persistence validation

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter ExperimentServiceTests
dotnet test --filter SessionTaskTests

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Test Data

Tests use mocked DynamoDB responses matching the production data structure:
- Proper PK/SK patterns (EXPERIMENT#, TASK#, SESSION#)
- Correct attribute value formats
- Realistic BCI experiment scenarios
- Complex multi-session type experiments
- Batch questionnaire task validation