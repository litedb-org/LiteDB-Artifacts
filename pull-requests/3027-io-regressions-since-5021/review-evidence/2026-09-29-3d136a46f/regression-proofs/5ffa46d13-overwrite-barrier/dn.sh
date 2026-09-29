#!/bin/bash
# Run a command with the .NET environment, from the worktree.
export PATH=$DOTNET_ROOT:$PATH DOTNET_ROOT=$DOTNET_ROOT DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
cd $REPO/.claude/worktrees/agent-ae670113747d5f100 || exit 99
exec "$@"
