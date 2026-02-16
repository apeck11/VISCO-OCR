@echo off
setlocal
set PROJECT=%~dp0..\src\Visco.Ocr.Api\Visco.Ocr.Api.csproj
if not exist "%PROJECT%" (
  echo Project file not found: %PROJECT%
  exit /b 1
)

dotnet run --project "%PROJECT%"
