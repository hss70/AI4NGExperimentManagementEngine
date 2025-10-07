# AI4NG Question Engine

Question Engine API for managing experiments, questionnaires, and responses in the AI4NG BCI research platform.

## Architecture

- **AI4NGExperimentsLambda**: Manages experiments, sessions, and configurations
- **AI4NGResponsesLambda**: Handles questionnaire response submission and retrieval
- **AI4NGQuestionnairesLambda**: Manages questionnaire definitions and configurations
- **DynamoDB Tables**: 
  - AI4NGExperiments: Experiments, sessions, and tasks with GSI support
  - AI4NGResponses: Questionnaire answers with multiple access patterns
  - AI4NGQuestionnaires: Reusable questionnaire definitions with sync support

## Deployment

Requires AWS credentials and CloudFormation exports from shared infrastructure.

Deploy using GitHub Actions (recommended) or SAM CLI with parameters:
```bash
sam build --template-file infra/QuestionEngine-template.yaml
sam deploy --parameter-overrides Environment=dev SharedApiId=xxx SharedApiAuthorizerId=yyy PrivateSG=zzz PrivateSubnetIds="subnet1,subnet2"
```

## API Endpoints

### Experiments
- `GET /api/experiments` - List all experiments
- `GET /api/experiments/{id}` - Get experiment details
- `POST /api/experiments` - Create new experiment
- `POST /api/experiments/{id}/sync` - Sync experiment sessions

### Questionnaires
- `GET /api/questionnaires` - List all questionnaires
- `GET /api/questionnaires/{id}` - Get questionnaire definition
- `POST /api/questionnaires` - Create new questionnaire
- `PUT /api/questionnaires/{id}` - Update questionnaire

### Responses
- `POST /api/responses` - Submit questionnaire responses
- `GET /api/responses/{experimentId}` - Get user responses for experiment

### Mobile Sync
- `GET /api/sync/experiments` - Get modified experiments for sync
- `GET /api/sync/questionnaires` - Get modified questionnaires for sync
- `GET /api/sync/responses` - Get modified responses for sync