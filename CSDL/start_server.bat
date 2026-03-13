@echo off
echo ========================================
echo   Nhom1 API Server - Startup Script
echo ========================================
echo.

cd /d D:\Nhom1

echo [1/4] Checking project...
if not exist "Nhom1.sln" (
    echo ERROR: Nhom1.sln not found!
    pause
    exit
)

echo [2/4] Building project...
dotnet build --configuration Release
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Build failed!
    pause
    exit
)

echo.
echo [3/4] Build successful!
echo.
echo ========================================
echo   IMPORTANT: Conveyor Instructions
echo ========================================
echo.
echo To use Conveyor for mobile testing:
echo.
echo   1. Open Visual Studio 2022
echo   2. File - Open - Project/Solution
echo   3. Select: D:\Nhom1\Nhom1.sln
echo   4. Press F5 to run with debugging
echo   5. Look for Conveyor URL in Output window
echo   6. Copy the URL (e.g., https://xxx.conveyor.cloud)
echo   7. Save URL to current_conveyor_url.txt
echo.
echo ========================================
echo.
echo [4/4] Starting local server (without Conveyor)...
echo.
echo Server URLs:
echo   Local:  https://localhost:7097
echo   HTTP:   http://localhost:5189
echo.
echo Press Ctrl+C to stop server
echo ========================================
echo.

dotnet run
