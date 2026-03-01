#!/bin/bash
set -euo pipefail

# Only run in remote (web) environments
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

# ------------------------------------------------------------------
# Install .NET 8 SDK if not already available
# ------------------------------------------------------------------
if ! command -v dotnet &>/dev/null; then
  echo "Installing .NET 8 SDK via apt..."
  apt-get update -qq
  apt-get install -y -qq dotnet-sdk-8.0
fi

# Suppress .NET CLI telemetry and welcome messages
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
echo 'export DOTNET_NOLOGO=1' >> "$CLAUDE_ENV_FILE"
echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1' >> "$CLAUDE_ENV_FILE"

# ------------------------------------------------------------------
# Restore and build testable projects (non-WPF)
# ------------------------------------------------------------------
cd "$CLAUDE_PROJECT_DIR"

echo "Restoring NuGet packages for test project..."
dotnet restore tests/LiveCompanion.Tests/LiveCompanion.Tests.csproj

echo "Building test project..."
dotnet build tests/LiveCompanion.Tests/LiveCompanion.Tests.csproj --no-restore -c Debug

echo "Session start hook completed successfully."
