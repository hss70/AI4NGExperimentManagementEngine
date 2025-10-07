# AI4NG Question Engine

Question Engine API for managing experiments and questionnaire responses in the AI4NG BCI research platform.

## Architecture

- **AI4NGExperimentsLambda**: Manages experiments, sessions, and questionnaire configurations
- **AI4NGResponsesLambda**: Handles questionnaire response submission and retrieval
- **DynamoDB Tables**: 
  - AI4NGExperiments: Single-table design for experiments, sessions, and tasks
  - AI4NGResponses: Questionnaire answers with multiple access patterns

## Deployment

Requires AWS credentials to be added to repository secrets:
- AWS_ACCESS_KEY_ID_DEV
- AWS_SECRET_ACCESS_KEY_DEV

Deploy using GitHub Actions or SAM CLI:
```bash
sam build --template-file infra/QuestionEngine-template.yaml
sam deploy --config-env default
```

## API Endpoints

- `GET /api/experiments` - List all experiments
- `GET /api/experiments/{id}` - Get experiment details
- `POST /api/experiments` - Create new experiment
- `POST /api/experiments/{id}/sync` - Sync experiment sessions
- `POST /api/responses` - Submit questionnaire responses
- `GET /api/responses/{experimentId}` - Get user responses for experiment