$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot '..\src\Visco.Ocr.Api\Visco.Ocr.Api.csproj'
$projectPath = [System.IO.Path]::GetFullPath($projectPath)

if (-not (Test-Path $projectPath)) {
    Write-Error "Project file not found: $projectPath"
    exit 1
}

dotnet run --project $projectPath
