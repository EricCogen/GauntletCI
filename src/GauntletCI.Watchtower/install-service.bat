@echo off
REM Watchtower Windows Service Installation Script
REM Usage: install-service.bat <service-name> <service-path>
REM Example: install-service.bat Watchtower "C:\Services\Watchtower"

if "%1"=="" (
    echo Usage: install-service.bat ^<service-name^> ^<service-path^>
    echo Example: install-service.bat Watchtower "C:\Services\Watchtower"
    exit /b 1
)

if "%2"=="" (
    echo Usage: install-service.bat ^<service-name^> ^<service-path^>
    echo Example: install-service.bat Watchtower "C:\Services\Watchtower"
    exit /b 1
)

setlocal enabledelayedexpansion

set SERVICE_NAME=%1
set SERVICE_PATH=%2
set CLI_EXE="%SERVICE_PATH%\GauntletCI.Watchtower.exe"

echo Installing Windows Service: %SERVICE_NAME%
echo Service Path: %SERVICE_PATH%

REM Check if service already exists
sc query %SERVICE_NAME% >nul 2>&1
if !errorlevel! equ 0 (
    echo Service %SERVICE_NAME% already exists. Stopping and removing...
    net stop %SERVICE_NAME%
    sc delete %SERVICE_NAME%
    timeout /t 2 /nobreak
)

REM Create the service
echo Creating service...
sc create %SERVICE_NAME% binPath= %CLI_EXE% start= delayed-auto
if !errorlevel! neq 0 (
    echo Failed to create service
    exit /b 1
)

REM Set service description
sc description %SERVICE_NAME% "Watchtower - Automated security validation lab for GauntletCI"

REM Start the service
echo Starting service...
net start %SERVICE_NAME%
if !errorlevel! neq 0 (
    echo Warning: Failed to start service automatically
    echo You can start it manually with: net start %SERVICE_NAME%
)

echo Service installation complete!
echo To view service status: sc query %SERVICE_NAME%
echo To view service logs: %SERVICE_PATH%\logs\
