#!/usr/bin/env pwsh

$SHARED_API_AUTHORIZER_ID = aws cloudformation list-exports --profile hardeepGmail --region eu-west-2 --query "Exports[?Name=='SharedApiAuthorizerId'].Value" --output text
$PRIVATE_SG = aws cloudformation list-exports --profile hardeepGmail --region eu-west-2 --query "Exports[?Name=='NetworkStack-PrivateSG'].Value" --output text
$PRIVATE_SUBNETS = aws cloudformation list-exports --profile hardeepGmail --region eu-west-2 --query "Exports[?Name=='NetworkStack-PrivateSubnetIds'].Value" --output text

Write-Host "Update your samconfig.toml with these values:" -ForegroundColor Green
Write-Host "SharedApiAuthorizerId: $SHARED_API_AUTHORIZER_ID" -ForegroundColor Yellow
Write-Host "PrivateSG: $PRIVATE_SG" -ForegroundColor Yellow  
Write-Host "PrivateSubnetIds: $PRIVATE_SUBNETS" -ForegroundColor Yellow