# AI4NG API Automated Test Script
param(
    [string]$ApiUrl = "https://3mybicfkv2.execute-api.eu-west-2.amazonaws.com/dev",
    [string]$ClientId = "517s6c84jo5i3lqste5idb0o4c",
    [string]$Username = "hss702",
    [string]$Password = "Hardeep123!"
)

Write-Host "🚀 Starting AI4NG API Tests..." -ForegroundColor Green

# Check if Newman is installed
if (!(Get-Command newman -ErrorAction SilentlyContinue)) {
    Write-Host "❌ Newman not found. Installing..." -ForegroundColor Yellow
    npm install -g newman
}

# Run the collection with environment variables
$env:API_URL = $ApiUrl
$env:CLIENT_ID = $ClientId
$env:USERNAME = $Username
$env:PASSWORD = $Password

Write-Host "🔧 Configuration:" -ForegroundColor Cyan
Write-Host "  API URL: $ApiUrl"
Write-Host "  Client ID: $ClientId"
Write-Host "  Username: $Username"
Write-Host ""

# Run Newman with the collection
newman run "../postman/AI4NG Complete API Test Collection.postman_collection.json" `
    --global-var "apiGatewayUrl=$ApiUrl" `
    --global-var "cognitoClientId=$ClientId" `
    --global-var "username=$Username" `
    --global-var "password=$Password" `
    --reporters cli,html `
    --reporter-html-export "./test-results.html" `
    --timeout-request 30000 `
    --delay-request 1000 `
    --verbose

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ All tests passed!" -ForegroundColor Green
    Write-Host "📊 Test report saved to: test-results.html" -ForegroundColor Cyan
} else {
    Write-Host "❌ Some tests failed. Check the output above." -ForegroundColor Red
    exit 1
}