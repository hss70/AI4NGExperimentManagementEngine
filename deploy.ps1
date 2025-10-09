#!/usr/bin/env pwsh

Write-Host "Fetching CloudFormation exports..." -ForegroundColor Green

$SHARED_API_ID = aws cloudformation list-exports --profile hardeepGmail --region eu-west-2 --query "Exports[?Name=='SharedApiId'].Value" --output text
$SHARED_API_AUTHORIZER_ID = aws cloudformation list-exports --profile hardeepGmail --region eu-west-2 --query "Exports[?Name=='SharedApiAuthorizerId'].Value" --output text
$PRIVATE_SG = aws cloudformation list-exports --profile hardeepGmail --region eu-west-2 --query "Exports[?Name=='NetworkStack-PrivateSG'].Value" --output text
$PRIVATE_SUBNETS = aws cloudformation list-exports --profile hardeepGmail --region eu-west-2 --query "Exports[?Name=='NetworkStack-PrivateSubnetIds'].Value" --output text

Write-Host "SharedApiId: $SHARED_API_ID" -ForegroundColor Yellow
Write-Host "SharedApiAuthorizerId: $SHARED_API_AUTHORIZER_ID" -ForegroundColor Yellow
Write-Host "PrivateSG: $PRIVATE_SG" -ForegroundColor Yellow
Write-Host "PrivateSubnets: $PRIVATE_SUBNETS" -ForegroundColor Yellow

if ([string]::IsNullOrEmpty($SHARED_API_ID) -or $SHARED_API_ID -eq "None") {
    Write-Error "SharedApiId export not found!"
    exit 1
}

if ([string]::IsNullOrEmpty($SHARED_API_AUTHORIZER_ID) -or $SHARED_API_AUTHORIZER_ID -eq "None") {
    Write-Error "SharedApiAuthorizerId export not found!"
    exit 1
}

Write-Host "Building SAM application..." -ForegroundColor Green
sam build --template-file infra/QuestionEngine-template.yaml --profile hardeepGmail

if ($LASTEXITCODE -ne 0) {
    Write-Error "SAM build failed!"
    exit 1
}

Write-Host "Deploying SAM application..." -ForegroundColor Green
sam deploy --no-confirm-changeset --disable-rollback --profile hardeepGmail --parameter-overrides "Environment=dev" "SharedApiId=$SHARED_API_ID" "SharedApiAuthorizerId=$SHARED_API_AUTHORIZER_ID" "PrivateSG=$PRIVATE_SG" "PrivateSubnetIds=$PRIVATE_SUBNETS"

if ($LASTEXITCODE -eq 0) {
    Write-Host "Deployment completed successfully!" -ForegroundColor Green
} else {
    Write-Error "Deployment failed!"
    exit 1
}