@echo off
echo 🚀 AI4NG API Quick Test
echo.

REM Set your API URL here
set API_URL=https://3mybicfkv2.execute-api.eu-west-2.amazonaws.com/dev

echo Running automated API tests...
newman run "../postman/AI4NG Automated Test Suite.postman_collection.json" ^
    --global-var "apiGatewayUrl=%API_URL%" ^
    --reporters cli,json ^
    --reporter-json-export "./test-results.json" ^
    --timeout-request 30000 ^
    --delay-request 1000

if %errorlevel% equ 0 (
    echo.
    echo ✅ All tests passed!
) else (
    echo.
    echo ❌ Some tests failed!
    exit /b 1
)

pause