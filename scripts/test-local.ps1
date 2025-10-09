# Local testing script for Question Engine APIs
param(
    [string]$Service = "all"  # questionnaires, experiments, responses, or all
)

$ErrorActionPreference = "Stop"

# Start DynamoDB Local if not running
$dynamoProcess = Get-Process -Name "DynamoDBLocal" -ErrorAction SilentlyContinue
if (-not $dynamoProcess) {
    Write-Host "Starting DynamoDB Local..." -ForegroundColor Yellow
    Start-Process -FilePath "java" -ArgumentList "-Djava.library.path=./DynamoDBLocal_lib -jar DynamoDBLocal.jar -sharedDb -port 8000" -WorkingDirectory "C:\tools\dynamodb_local" -WindowStyle Hidden
    Start-Sleep 5
}

# Set environment variables for local testing
$env:QUESTIONNAIRES_TABLE = "questionnaires-local"
$env:EXPERIMENTS_TABLE = "experiments-local"
$env:RESPONSES_TABLE = "responses-local"
$env:AWS_DEFAULT_REGION = "eu-west-2"

# Create local tables if they don't exist
aws dynamodb create-table --table-name questionnaires-local --attribute-definitions AttributeName=PK,AttributeType=S AttributeName=SK,AttributeType=S --key-schema AttributeName=PK,KeyType=HASH AttributeName=SK,KeyType=RANGE --billing-mode PAY_PER_REQUEST --endpoint-url http://localhost:8000 2>$null
aws dynamodb create-table --table-name experiments-local --attribute-definitions AttributeName=PK,AttributeType=S AttributeName=SK,AttributeType=S AttributeName=GSI1PK,AttributeType=S AttributeName=GSI1SK,AttributeType=S --key-schema AttributeName=PK,KeyType=HASH AttributeName=SK,KeyType=RANGE --global-secondary-indexes IndexName=GSI1,KeySchema=[{AttributeName=GSI1PK,KeyType=HASH},{AttributeName=GSI1SK,KeyType=RANGE}],Projection={ProjectionType=ALL},BillingMode=PAY_PER_REQUEST --billing-mode PAY_PER_REQUEST --endpoint-url http://localhost:8000 2>$null
aws dynamodb create-table --table-name responses-local --attribute-definitions AttributeName=PK,AttributeType=S AttributeName=SK,AttributeType=S AttributeName=GSI1PK,AttributeType=S AttributeName=GSI1SK,AttributeType=S --key-schema AttributeName=PK,KeyType=HASH AttributeName=SK,KeyType=RANGE --global-secondary-indexes IndexName=GSI1,KeySchema=[{AttributeName=GSI1PK,KeyType=HASH},{AttributeName=GSI1SK,KeyType=RANGE}],Projection={ProjectionType=ALL},BillingMode=PAY_PER_REQUEST --billing-mode PAY_PER_REQUEST --endpoint-url http://localhost:8000 2>$null

Write-Host "Starting SAM Local API..." -ForegroundColor Green

switch ($Service) {
    "questionnaires" { sam local start-api --template-file infra/ExperimentManagement-template.yaml --port 3001 --env-vars ../local-testing/local-env.json }
    "experiments" { sam local start-api --template-file infra/ExperimentManagement-template.yaml --port 3002 --env-vars ../local-testing/local-env.json }
    "responses" { sam local start-api --template-file infra/ExperimentManagement-template.yaml --port 3003 --env-vars ../local-testing/local-env.json }
    default { sam local start-api --template-file infra/ExperimentManagement-template.yaml --port 3000 --env-vars ../local-testing/local-env.json }
}