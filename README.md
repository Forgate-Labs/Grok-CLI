# GrokCLI

Terminal chat client for Grok 4.1 Fast with agentic tool-calling, packaged as a .NET global tool (`grok`).

## Quick start
- Requirements: .NET SDK 10+, xAI API key, and the dotnet tools directory on PATH (`~/.dotnet/tools` on Linux/macOS, `%USERPROFILE%\.dotnet\tools` on Windows).
- Install: `dotnet tool install -g grok` (update with `dotnet tool update -g grok`).
- Run: `grok` (or `dotnet run --project GrokCLI/GrokCLI.tui` from source).
- Provide `XAI_API_KEY` via environment variable or `grok.config.json` in the working directory.

## Core features
- Streaming Grok 4.1 Fast chat with an agentic loop and tool calls.
- Tools: Python via `code_execution` (python3), CLI via `run_command`, text search (`search` with ripgrep/grep/PowerShell), file read (`read_local_file`, 200 KB limit, working-dir sandbox), file edit (`edit_file` replace/insert/append/delete/write with optional backups), directory change, tests, workflow completion marker, and a placeholder web search.
- Command gating: `run_command` and `code_execution` respect `allowed_commands`/`blocked_commands` from `grok.config.json`; prompts offer allow once/always/deny/never and reject blocked prefixes by default.
- Pre-prompt: default asks the model to call `workflow_done` when finished and to follow `GROK.md` if present; merges with any `pre_prompt` you set in config.
- Display modes: Normal (summaries) vs Debug (full tool-call detail); set with `--mode normal|debug`, `--debug`, `--normal`, or `GROK_MODE`, or switch in chat by typing `debug`/`normal`.
- In-chat helpers: `cmd <command>` or `/cmd <command>` to run shell commands, `clear` or `/clear` to clear, Ctrl+Enter for a newline.
- API key handling: if missing, chat is locked and guidance is shown; `grok.config.json` is auto-created with defaults if absent.

## Configuration (`grok.config.json`)
Created in the working directory if missing. Keys:
- `XAI_API_KEY`: xAI key (prefer environment variable).
- `pre_prompt`: extra system prompt text, appended after the built-in default.
- `allowed_commands`: prefixes allowed without prompting.
- `blocked_commands`: prefixes rejected immediately (destructive defaults are included).

If `GROK.md` is present in the working directory or ancestors, its contents are appended to the pre-prompt. Do not commit real API keys to version control.

## Usage tips
- Send messages normally; Ctrl+Enter adds a newline.
- Type `debug` or `normal` in chat to toggle display mode.
- Use `cmd <command>`/`/cmd <command>` for shell commands (subject to allow/block rules); `clear` or `/clear` runs the platform clear command.
- Workflow completion is signaled via the `workflow_done` tool call (requested in the default pre-prompt).

## Development
- Build: `dotnet build`
- Tests: `dotnet test`
- Pack (local): `dotnet pack GrokCLI/GrokCLI.tui/GrokCLI.tui.csproj --configuration Release --output out`

## Publishing
`.github/Workflows/nuget-publish.yml` builds/packs `GrokCLI.tui` and pushes to NuGet (tool id `grok`) when the `nuget` branch is pushed or the workflow is manually dispatched. It requires the `NUGET_API_KEY` secret, tags `v<version>`, and creates a GitHub release with the generated `.nupkg`.

## Project structure
- `GrokCLI/GrokCLI.tui`: TUI application and tool entrypoint (`grok`)
- `GrokCLI/GrokCLI.Tests`: test project
- `.github/Workflows/nuget-publish.yml`: publish pipeline
- `AGENTS.md`: engineering rules
- `GrokCLI/GrokCLI.tui/grok.config.json`, `GrokCLI/GrokCLI.tui/GROK.md`: sample config and optional prompt instructions
- `GrokCLI/images`: screenshots/assets
