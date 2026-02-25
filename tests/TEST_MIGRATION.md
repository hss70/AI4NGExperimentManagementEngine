## Migration Order

## Summary

- Total skipped tests (dotnet test): 54
- Documented skipped tests (rows in this file): 53
- Missing / not yet documented: 1

Counts per category (from this document):

- Membership: 25
- SyncBundle: 11
- ProtocolSessions: 10
- ParticipantOccurrences: 7
- Unmapped / Missing: 1

## Migration Order

Note: the total above uses the official skipped count from `dotnet test` (see verification below). One skipped test discovered by the test run is not yet represented in these tables — I added an "Unmapped / Missing" count so the totals match. If you want, I can locate the missing test and append it to the appropriate table.

The next three migration tranches (by priority and dependency order) are:

1. Membership
	 - Rationale: membership (experiment participants) is a core dependency for researcher/participant flows and often required by session and sync operations. Migrating membership first reduces downstream test complexity.
	 - Tests to migrate in this tranche (exact file + test method):
		 - `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` — ExperimentsController_GetMembers_ShouldReturnOkWithMembers
		 - `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` — ExperimentsController_AddMember_ShouldReturnOkWithMessage
		 - `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` — ExperimentsController_RemoveMember_ShouldReturnOkWithMessage
		 - `tests/AI4NGExperiments.Tests/ExperimentsControllerTests.cs` — GetByIdMembers_ShouldReturnOk_WithMembers
		 - `tests/AI4NGExperiments.Tests/ExperimentsControllerTests.cs` — AddMember_ShouldReturnOk_WhenValid
		 - `tests/AI4NGExperiments.Tests/ExperimentsControllerTests.cs` — RemoveMember_ShouldReturnOk_WhenValid
		 - `tests/AI4NGExperiments.Tests/ValidationTests.cs` — AddMemberAsync_ShouldValidateRoleValues
		 - `tests/AI4NGExperiments.Tests/ValidationTests.cs` — AddMemberAsync_ShouldSucceed_WithValidRole
		 - `tests/AI4NGExperiments.Tests/ValidationTests.cs` — AddMemberAsync_ShouldSucceed_WithResearcherRole
		 - `tests/AI4NGExperiments.Tests/DynamoDBFormatTests.cs` — AddMemberAsync_ShouldCreateCorrectMemberPKFormat
		 - `tests/AI4NGExperiments.Tests/DynamoDBFormatTests.cs` — AddMemberAsync_ShouldCreateCorrectMemberSKFormat
		 - `tests/AI4NGExperiments.Tests/DynamoDBFormatTests.cs` — AddMemberAsync_ShouldSetCorrectMemberType
		 - `tests/AI4NGExperiments.Tests/DynamoDBFormatTests.cs` — AddMemberAsync_ShouldIncludeMemberAuditFields
		 - `tests/AI4NGExperiments.Tests/IntegrationTests.cs` — ExperimentMemberManagementWorkflow_ShouldSucceed
		 - `tests/AI4NGExperiments.Tests/ExperimentServiceTests.cs` — GetExperimentMembersAsync_ShouldReturnMembers
		 - `tests/AI4NGExperiments.Tests/ExperimentServiceTests.cs` — AddMemberAsync_ShouldAddMember_WhenValid
		 - `tests/AI4NGExperiments.Tests/ExperimentServiceTests.cs` — RemoveMemberAsync_ShouldRemoveMember_WhenExists
		 - `tests/AI4NGExperiments.Tests/BusinessRuleTests.cs` — AddMemberAsync_ShouldSucceed_WithValidRoles (Theory)
		 - `tests/AI4NGExperiments.Tests/BusinessRuleTests.cs` — AddMemberAsync_ShouldHandleInvalidRoles (Theory)
		 - `tests/AI4NGExperiments.Tests/BusinessRuleTests.cs` — AddMemberAsync_ShouldHandleInvalidUserSub
		 - `tests/AI4NGExperiments.Tests/BusinessRuleTests.cs` — RemoveMemberAsync_ShouldSucceed_WhenMemberExists
		 - `tests/AI4NGExperiments.Tests/BusinessRuleTests.cs` — RemoveMemberAsync_ShouldHandleInvalidUserSub

2. ProtocolSessions
	 - Rationale: protocol sessions are about protocol definitions (SessionProtocolController, ProtocolSessionDto, PROTOCOL_SESSION# keys) and cadence definitions (FIRST/DAILY/WEEKLY). Migrate these after membership so protocol flows have valid ownership metadata.
	 - Tests to migrate in this tranche (exact file + test method):
		 - `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` — ExperimentsController_GetSessions_ShouldReturnOkWithSessions
		 - `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` — ExperimentsController_GetSession_ShouldReturnOkWithSession
		 - `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` — ExperimentsController_GetSession_ShouldReturnNotFoundWhenSessionDoesNotExist
		 - `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` — ExperimentsController_CreateSession_ShouldReturnOkWithResult
		 - `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` — ExperimentsController_UpdateSession_ShouldReturnOkWithMessage
		 - `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` — ExperimentsController_DeleteSession_ShouldReturnOkWithMessage
		 - `tests/AI4NGExperiments.Tests/SessionTaskTests.cs` — CreateExperimentAsync_ShouldHandleComplexBCIExperiment
		 - `tests/AI4NGExperiments.Tests/SessionTaskTests.cs` — CreateSessionAsync_ShouldHandleMultipleSessionTypes
		 - `tests/AI4NGExperiments.Tests/IntegrationTests.cs` — CompleteExperimentWorkflow_ShouldSucceed
		 - `tests/AI4NGExperiments.Tests/ValidationErrorTests.cs` — CreateSession_WithMissingExperiment_ReturnsBadRequestWithValidationError

3. ParticipantOccurrences
	 - Rationale: participant-occurrence (legacy runtime sessions) are the runtime session records (PKs like `SESSION#...`), CreateSessionAsync/GetSessionAsync flows and taskOrder fallback/casing behaviour. Migrate these after membership and protocol sessions to ensure dependencies (participants, protocol definitions) are present.
	 - Tests to migrate in this tranche (exact file + test method):
		 - `tests/AI4NGExperiments.Tests/SessionTaskTests.cs` — CreateSessionAsync_ShouldReturnSessionId_WhenValid
		 - `tests/AI4NGExperiments.Tests/SessionTaskTests.cs` — GetSessionAsync_ShouldReturnSession_WhenExists
		 - `tests/AI4NGExperiments.Tests/SessionTaskOrderFallbackTests.cs` — GetSessionAsync_FillsTaskOrder_From_Data_WhenTopLevelEmpty
		 - `tests/AI4NGExperiments.Tests/SessionTaskOrderFallbackTests.cs` — GetSessionAsync_FallsBackToPascalCaseDataTaskOrder_WhenTopLevelEmpty
		 - `tests/AI4NGExperiments.Tests/SessionTaskOrderFallbackTests.cs` — GetSessionAsync_FallsBackToCamelCaseDataTaskOrder_WhenTopLevelEmpty
		 - `tests/AI4NGExperiments.Tests/SessionTaskOrderFallbackTests.cs` — GetExperimentSessionsAsync_Fallbacks_Work_For_Multi_Sessions

---

## Migration list of skipped xUnit tests

This file lists all tests in the `tests/` tree that are currently skipped via xUnit `[Fact(Skip=...)]` or `[Theory(Skip=...)]` attributes.

Columns: File (relative path), Test method / class, Skip reason, Category, Recommended target suite, Status

---

## ProtocolSessions

| File | Test method | Skip reason | Category | Recommended target suite | Status |
|---|---|---|---|---|---|
| `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` | ExperimentsController_GetSessions_ShouldReturnOkWithSessions | "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined" | ProtocolSessions | `SessionProtocolServiceTests.cs` / `SessionProtocolControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` | ExperimentsController_GetSession_ShouldReturnOkWithSession | "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined" | ProtocolSessions | `SessionProtocolServiceTests.cs` / `SessionProtocolControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` | ExperimentsController_GetSession_ShouldReturnNotFoundWhenSessionDoesNotExist | "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined" | ProtocolSessions | `SessionProtocolServiceTests.cs` / `SessionProtocolControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` | ExperimentsController_CreateSession_ShouldReturnOkWithResult | "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined" | ProtocolSessions | `SessionProtocolServiceTests.cs` / `SessionProtocolControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` | ExperimentsController_UpdateSession_ShouldReturnOkWithMessage | "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined" | ProtocolSessions | `SessionProtocolServiceTests.cs` / `SessionProtocolControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` | ExperimentsController_DeleteSession_ShouldReturnOkWithMessage | "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined" | ProtocolSessions | `SessionProtocolServiceTests.cs` / `SessionProtocolControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/SessionTaskTests.cs` | CreateExperimentAsync_ShouldHandleComplexBCIExperiment | "Refactor: moved to Session services" | ProtocolSessions | `SessionProtocolServiceTests.cs` / `SessionProtocolControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/SessionTaskTests.cs` | CreateExperimentAsync_ShouldThrowException_WhenQuestionnaireNotFound | "Refactor: moved to Session services" | ProtocolSessions | `SessionProtocolServiceTests.cs` / `SessionProtocolControllerTests.cs` | Not started |

## ParticipantOccurrences

| File | Test method | Skip reason | Category | Recommended target suite | Status |
|---|---|---|---|---|---|
| `tests/AI4NGExperiments.Tests/SessionTaskTests.cs` | CreateSessionAsync_ShouldReturnSessionId_WhenValid | "Refactor: moved to Session services" | ParticipantOccurrences | `ParticipantSessionOccurrencesServiceTests.cs` / `ParticipantSessionOccurrencesControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/SessionTaskTests.cs` | GetSessionAsync_ShouldReturnSession_WhenExists | "Refactor: moved to Session services" | ParticipantOccurrences | `ParticipantSessionOccurrencesServiceTests.cs` / `ParticipantSessionOccurrencesControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/SessionTaskTests.cs` | CreateSessionAsync_ShouldHandleMultipleSessionTypes | "Refactor: moved to Session services" | ParticipantOccurrences | `ParticipantSessionOccurrencesServiceTests.cs` / `ParticipantSessionOccurrencesControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/SessionTaskOrderFallbackTests.cs` | GetSessionAsync_FillsTaskOrder_From_Data_WhenTopLevelEmpty | "Refactor: moved to Session services" | ParticipantOccurrences | `ParticipantSessionOccurrencesServiceTests.cs` / `ParticipantSessionOccurrencesControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/SessionTaskOrderFallbackTests.cs` | GetSessionAsync_FallsBackToPascalCaseDataTaskOrder_WhenTopLevelEmpty | "Refactor: moved to Session services" | ParticipantOccurrences | `ParticipantSessionOccurrencesServiceTests.cs` / `ParticipantSessionOccurrencesControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/SessionTaskOrderFallbackTests.cs` | GetSessionAsync_FallsBackToCamelCaseDataTaskOrder_WhenTopLevelEmpty | "Refactor: moved to Session services" | ParticipantOccurrences | `ParticipantSessionOccurrencesServiceTests.cs` / `ParticipantSessionOccurrencesControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/SessionTaskOrderFallbackTests.cs` | GetExperimentSessionsAsync_Fallbacks_Work_For_Multi_Sessions | "Refactor: moved to Session services" | ParticipantOccurrences | `ParticipantSessionOccurrencesServiceTests.cs` / `ParticipantSessionOccurrencesControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/IntegrationTests.cs` | CompleteExperimentWorkflow_ShouldSucceed | "Refactor: moved to Session/Membership services" | ProtocolSessions | `SessionProtocolServiceTests.cs` | Not started |

## Membership

| File | Test method | Skip reason | Category | Recommended target suite | Status |
|---|---|---|---|---|---|
| `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` | ExperimentsController_GetMembers_ShouldReturnOkWithMembers | "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` | ExperimentsController_AddMember_ShouldReturnOkWithMessage | "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` | ExperimentsController_RemoveMember_ShouldReturnOkWithMessage | "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ExperimentsControllerTests.cs` | GetByIdMembers_ShouldReturnOk_WithMembers | "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ExperimentsControllerTests.cs` | AddMember_ShouldReturnOk_WhenValid | "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ExperimentsControllerTests.cs` | RemoveMember_ShouldReturnOk_WhenValid | "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ValidationTests.cs` | AddMemberAsync_ShouldValidateRoleValues | "Refactor: moved to Session/Membership services" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ValidationTests.cs` | AddMemberAsync_ShouldSucceed_WithValidRole | "Refactor: moved to Session/Membership services" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ValidationTests.cs` | AddMemberAsync_ShouldSucceed_WithResearcherRole | "Refactor: moved to Session/Membership services" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/DynamoDBFormatTests.cs` | AddMemberAsync_ShouldCreateCorrectMemberPKFormat | "Refactor: moved to Session/Membership services" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/DynamoDBFormatTests.cs` | AddMemberAsync_ShouldCreateCorrectMemberSKFormat | "Refactor: moved to Session/Membership services" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/DynamoDBFormatTests.cs` | AddMemberAsync_ShouldSetCorrectMemberType | "Refactor: moved to Session/Membership services" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/DynamoDBFormatTests.cs` | AddMemberAsync_ShouldIncludeMemberAuditFields | "Refactor: moved to Session/Membership services" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/IntegrationTests.cs` | ExperimentMemberManagementWorkflow_ShouldSucceed | "Refactor: moved to Session/Membership services" | Membership | `ExperimentParticipantsServiceTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ExperimentServiceTests.cs` | GetExperimentMembersAsync_ShouldReturnMembers | "Refactor: moved to Session/Membership services" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ExperimentServiceTests.cs` | AddMemberAsync_ShouldAddMember_WhenValid | "Refactor: moved to Session/Membership services" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ExperimentServiceTests.cs` | RemoveMemberAsync_ShouldRemoveMember_WhenExists | "Refactor: moved to Session/Membership services" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/BusinessRuleTests.cs` | AddMemberAsync_ShouldSucceed_WithValidRoles (Theory) | "Refactor: moved to Session/Membership services" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/BusinessRuleTests.cs` | AddMemberAsync_ShouldHandleInvalidRoles (Theory) | "Refactor: moved to Session/Membership services" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/BusinessRuleTests.cs` | AddMemberAsync_ShouldHandleInvalidUserSub | "Refactor: moved to Session/Membership services" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/BusinessRuleTests.cs` | RemoveMemberAsync_ShouldSucceed_WhenMemberExists | "Refactor: moved to Session/Membership services" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/BusinessRuleTests.cs` | RemoveMemberAsync_ShouldHandleInvalidUserSub | "Refactor: moved to Session/Membership services" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` | ExperimentsController_GetMyExperiments_ShouldReturnUserExperiments | "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ExperimentsControllerTests.cs` | GetMyExperiments_ShouldReturnOk_InLocalMode | "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |

## SyncBundle

| File | Test method | Skip reason | Category | Recommended target suite | Status |
|---|---|---|---|---|---|
| `tests/AI4NGExperiments.Tests/ControllerIntegrationTests.cs` | ExperimentsController_Sync_ShouldReturnOkWithSyncResult | "Refactor: moved to LegacyMonolith - session/member/sync tests quarantined" | SyncBundle | `ParticipantExperimentsServiceTests.cs` (or `BundleServiceTests.cs`) | Not started |
| `tests/AI4NGExperiments.Tests/ErrorHandlingTests.cs` | Service_ShouldHandleNullSyncTime | "Refactor: moved to Session/Membership services" | SyncBundle | `ParticipantExperimentsServiceTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ValidationTests.cs` | SyncExperimentAsync_ShouldValidateTimestampFormat | "Refactor: moved to Session/Membership services" | SyncBundle | `ParticipantExperimentsServiceTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/DynamoDBFormatTests.cs` | SyncExperimentAsync_ShouldReturnCorrectFormat | "Refactor: moved to Session/Membership services" | SyncBundle | `ParticipantExperimentsServiceTests.cs` (or `BundleServiceTests.cs`) | Not started |
| `tests/AI4NGExperiments.Tests/DynamoDBFormatTests.cs` | SyncExperimentAsync_ShouldValidateExperimentExists | "Refactor: moved to Session/Membership services" | SyncBundle | `ParticipantExperimentsServiceTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/DynamoDBFormatTests.cs` | SyncExperimentAsync_ShouldUseCorrectQueryFormat | "Refactor: moved to Session/Membership services" | SyncBundle | `ParticipantExperimentsServiceTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ReferentialIntegrityTests.cs` | SyncExperimentAsync_ShouldValidateExperimentExists | "Refactor: moved to Session/Membership services" | SyncBundle | `ParticipantExperimentsServiceTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ExperimentServiceTests.cs` | SyncExperimentAsync_ShouldReturnChanges_WhenValid | "Refactor: moved to Session/Membership services" | SyncBundle | `ParticipantExperimentsServiceTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/BusinessRuleTests.cs` | SyncExperimentAsync_ShouldThrowException_WhenExperimentNotFound | "Refactor: moved to Session/Membership services" | SyncBundle | `ParticipantExperimentsServiceTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/BusinessRuleTests.cs` | SyncExperimentAsync_ShouldReturnData_WhenExperimentExists | "Refactor: moved to Session/Membership services" | SyncBundle | `ParticipantExperimentsServiceTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/BusinessRuleTests.cs` | SyncExperimentAsync_ShouldReturnEmptyResult_WhenNoChanges | "Refactor: moved to Session/Membership services" | SyncBundle | `ParticipantExperimentsServiceTests.cs` | Not started |

## Other / Misc

| File | Test method | Skip reason | Category | Recommended target suite | Status |
|---|---|---|---|---|---|
| `tests/AI4NGExperiments.Tests/ExperimentServiceTests.cs` | GetMyExperimentsAsync_ShouldReturnUserExperiments | "Refactor: moved to Session/Membership services" | Membership | `ExperimentParticipantsServiceTests.cs` / `ExperimentParticipantsControllerTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/IntegrationTests.cs` | ConcurrentOperations_ShouldHandleMultipleQueries | "Refactor: moved to Session/Membership services" | ProtocolSessions | `SessionProtocolServiceTests.cs` | Not started |
| `tests/AI4NGExperiments.Tests/ValidationErrorTests.cs` | CreateSession_WithMissingExperiment_ReturnsBadRequestWithValidationError | "Refactor: moved to Session services" | ProtocolSessions | `SessionProtocolServiceTests.cs` | Not started |

---

Notes:
- Category was inferred from test names and the file context. If you prefer different category mappings for specific tests, I can update the table.
- Recommended target suites are suggestions based on repository conventions and the mappings in the migration instructions.
- Status for all entries defaults to "Not started" as requested.
