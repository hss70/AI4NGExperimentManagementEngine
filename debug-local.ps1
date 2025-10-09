# Debug script for local Lambda development
param(
    [string]$Service = "all",
    [switch]$Debug = $false
)

$ErrorActionPreference = "Stop"

Write-Host "🔧 Starting local debugging environment..." -ForegroundColor Cyan

# Start DynamoDB Local
$dynamoProcess = Get-Process -Name "DynamoDBLocal" -ErrorAction SilentlyContinue
if (-not $dynamoProcess) {
    Write-Host "Starting DynamoDB Local on port 8000..." -ForegroundColor Yellow
    Start-Process -FilePath "docker" -ArgumentList "run", "-d", "-p", "8000:8000", "--name", "dynamodb-local", "amazon/dynamodb-local" -WindowStyle Hidden
    Start-Sleep 5
}

# Create tables
Write-Host "Creating local DynamoDB tables..." -ForegroundColor Yellow
aws dynamodb create-table --table-name questionnaires-local --attribute-definitions AttributeName=PK,AttributeType=S AttributeName=SK,AttributeType=S --key-schema AttributeName=PK,KeyType=HASH AttributeName=SK,KeyType=RANGE --billing-mode PAY_PER_REQUEST --endpoint-url http://localhost:8000 2>$null
aws dynamodb create-table --table-name experiments-local --attribute-definitions AttributeName=PK,AttributeType=S AttributeName=SK,AttributeType=S AttributeName=GSI1PK,AttributeType=S AttributeName=GSI1SK,AttributeType=S --key-schema AttributeName=PK,KeyType=HASH AttributeName=SK,KeyType=RANGE --global-secondary-indexes IndexName=GSI1,KeySchema=[{AttributeName=GSI1PK,KeyType=HASH},{AttributeName=GSI1SK,KeyType=RANGE}],Projection={ProjectionType=ALL},BillingMode=PAY_PER_REQUEST --billing-mode PAY_PER_REQUEST --endpoint-url http://localhost:8000 2>$null
aws dynamodb create-table --table-name responses-local --attribute-definitions AttributeName=PK,AttributeType=S AttributeName=SK,AttributeType=S AttributeName=GSI1PK,AttributeType=S AttributeName=GSI1SK,AttributeType=S --key-schema AttributeName=PK,KeyType=HASH AttributeName=SK,KeyType=RANGE --global-secondary-indexes IndexName=GSI1,KeySchema=[{AttributeName=GSI1PK,KeyType=HASH},{AttributeName=GSI1SK,KeyType=RANGE}],Projection={ProjectionType=ALL},BillingMode=PAY_PER_REQUEST --billing-mode PAY_PER_REQUEST --endpoint-url http://localhost:8000 2>$null

if ($Debug) {
    Write-Host "🐛 Starting in DEBUG mode..." -ForegroundColor Red
    Write-Host "Attach your debugger to the Lambda process when it starts" -ForegroundColor Yellow
    Write-Host "Set breakpoints in your controllers/services" -ForegroundColor Yellow
    $env:LAMBDA_DEBUG = "true"
}

# Start SAM Local with debug options
$samArgs = @(
    "local", "start-api",
    "--template-file", "infra/QuestionEngine-template.yaml",
    "--port", "3000",
    "--env-vars", "local-env.json",
    "--host", "0.0.0.0"
)

if ($Debug) {
    $samArgs += @("--debug-port", "5858", "--debug-args", "-agentlib:jdwp=transport=dt_socket,server=y,suspend=y,address=5858")
}

Write-Host "🚀 Starting SAM Local API on http://localhost:3000" -ForegroundColor Green
Write-Host "📋 Import postman-local-collection.json into Postman for testing" -ForegroundColor Cyan
Write-Host "🔍 Check logs below for errors..." -ForegroundColor Yellow

sam @samArgs