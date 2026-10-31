@echo off
setlocal

if exist "%~dp0dist\ApiMock.exe" (
    "%~dp0dist\ApiMock.exe" %*
    exit /b %errorlevel%
)

if exist "%~dp0ApiMock.exe" (
    "%~dp0ApiMock.exe" %*
    exit /b %errorlevel%
)

dotnet run --project "%~dp0ApiMock.csproj" -c Debug -- %*
exit /b %errorlevel%
