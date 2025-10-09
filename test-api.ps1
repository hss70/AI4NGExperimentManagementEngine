# Quick API test script
param(
    [string]$BaseUrl = "http://localhost:3000"
)

$headers = @{
    "Content-Type" = "application/json"
}

Write-Host "Testing Questionnaires API..." -ForegroundColor Cyan

# Test questionnaire creation
$questionnaire = @{
    id = "test-questionnaire"
    data = @{
        name = "Test Questionnaire"
        description = "Local test questionnaire"
        questions = @(
            @{
                id = "q1"
                text = "Test question?"
                type = "text"
            }
        )
    }
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/questionnaires" -Method POST -Body $questionnaire -Headers $headers
    Write-Host "✅ Questionnaire created: $($response.id)" -ForegroundColor Green
} catch {
    Write-Host "❌ Questionnaire creation failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test questionnaire retrieval
try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/questionnaires" -Method GET -Headers $headers
    Write-Host "✅ Retrieved $($response.Count) questionnaires" -ForegroundColor Green
} catch {
    Write-Host "❌ Questionnaire retrieval failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nTesting Experiments API..." -ForegroundColor Cyan

# Test experiment creation
$experiment = @{
    data = @{
        name = "Test Experiment"
        description = "Local test experiment"
    }
    questionnaireConfig = @{
        questionnaireIds = @("test-questionnaire")
    }
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/researcher/experiments" -Method POST -Body $experiment -Headers $headers
    Write-Host "✅ Experiment created: $($response.id)" -ForegroundColor Green
    $experimentId = $response.id
} catch {
    Write-Host "❌ Experiment creation failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nTesting Responses API..." -ForegroundColor Cyan

# Test response creation
if ($experimentId) {
    $response = @{
        data = @{
            experimentId = $experimentId
            sessionId = "test-session-1"
            questionnaireId = "test-questionnaire"
            responses = @(
                @{
                    questionId = "q1"
                    answer = "Test answer"
                    timestamp = (Get-Date).ToString("O")
                }
            )
        }
    } | ConvertTo-Json -Depth 10

    try {
        $result = Invoke-RestMethod -Uri "$BaseUrl/api/responses" -Method POST -Body $response -Headers $headers
        Write-Host "✅ Response created: $($result.id)" -ForegroundColor Green
    } catch {
        Write-Host "❌ Response creation failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n🎉 Local API testing complete!" -ForegroundColor Green