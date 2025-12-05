# AI4NG API Automated Test Script
param(
    [string]$ApiUrl = "https://3mybicfkv2.execute-api.eu-west-2.amazonaws.com/dev",
    [string]$ClientId = "517s6c84jo5i3lqste5idb0o4c",
    [string]$Username = "hss702",
    [string]$Password = "Hardeep123!"
)

Write-Host "Starting AI4NG API Tests..." -ForegroundColor Green

# Cleanup function
function Invoke-Cleanup {
    Write-Host "Running cleanup..." -ForegroundColor Yellow
    
    if (Test-Path "test-results.json") {
        $results = Get-Content "test-results.json" | ConvertFrom-Json
        $idToken = $null
        
        # Extract idToken from successful authentication
        foreach ($execution in $results.run.executions) {
            if ($execution.item.name -eq "Authentication" -and $execution.response.code -eq 200) {
                try {
                    $authResponse = $execution.response.stream | ConvertFrom-Json
                    $idToken = $authResponse.AuthenticationResult.IdToken
                    break
                } catch { }
            }
        }
        
        if ($idToken) {
            Write-Host "Cleaning up test resources..." -ForegroundColor Yellow
            $cleanupItems = @(
                @{type="questionnaire"; ids=@("PreState","PhysicalState","CurrentState","EndState","PQ","TLX","IPAQ","VVIQ","ATI")},
                @{type="task"; ids=@()},
                @{type="experiment"; ids=@()}
            )
            
            foreach ($item in $cleanupItems) {
                foreach ($id in $item.ids) {
                    try {
                        Invoke-RestMethod -Uri "$ApiUrl/api/$($item.type)s/$id" -Method DELETE -Headers @{Authorization="Bearer $idToken"} -ErrorAction SilentlyContinue
                    } catch { }
                }
            }
            Write-Host "Cleanup completed" -ForegroundColor Green
        }
    }
}

# Register cleanup to run on exit
Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action { Invoke-Cleanup } | Out-Null

# Check if Newman is installed
if (!(Get-Command newman -ErrorAction SilentlyContinue)) {
    Write-Host "Newman not found. Installing..." -ForegroundColor Yellow
    npm install -g newman
}

Write-Host " Configuration:" -ForegroundColor Cyan
Write-Host "  API URL: $ApiUrl"
Write-Host "  Client ID: $ClientId"
Write-Host "  Username: $Username"
Write-Host ""

try {
    # Run Newman with the collection
    newman run "../postman/AI4NG Automated Test Suite.postman_collection.json" --global-var "apiGatewayUrl=$ApiUrl" --global-var "cognitoClientId=$ClientId" --global-var "username=$Username" --global-var "password=$Password" --reporter-cli --reporter-json --reporter-json-export "test-results.json" --bail --timeout-request 30000 --delay-request 1000
} finally {
    Invoke-Cleanup
}

# Parse results and create failure table
if (Test-Path "test-results.json") {
    $results = Get-Content "test-results.json" | ConvertFrom-Json
    $failures = @()
    
    foreach ($execution in $results.run.executions) {
        if ($execution.assertions) {
            foreach ($assertion in $execution.assertions) {
                if ($assertion.error) {
                    $requestBody = if ($execution.request.body.raw) { $execution.request.body.raw } else { "N/A" }
                    $failures += [PSCustomObject]@{
                        Test = $execution.item.name
                        Error = $assertion.error.message
                        Expected = if ($assertion.error.test) { $assertion.error.test } else { "N/A" }
                        Input = $requestBody
                    }
                }
            }
        }
    }
    
    if ($failures.Count -gt 0) {
        Write-Host "Test Failures:" -ForegroundColor Red
        $failures | Format-Table -AutoSize -Wrap
        Write-Host " Detailed JSON report saved to: test-results.json" -ForegroundColor Yellow
        exit 1
    } else {
        Write-Host "All tests passed!" -ForegroundColor Green
        Write-Host " Test report saved to: test-results.json" -ForegroundColor Cyan
    }
} else {
    Write-Host " Newman execution completed but no results file found." -ForegroundColor Yellow
    exit 1
}