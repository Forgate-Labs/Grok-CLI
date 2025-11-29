# GrokCLI (grok dotnet tool)

Terminal chat client for Grok 4.1 Fast with agentic tool-calling, packaged as a .NET global tool named `grok`.

## Install
- Prerequisite: .NET SDK 10+ and the dotnet tools path on PATH (`~/.dotnet/tools` on Linux/macOS, `%USERPROFILE%\.dotnet\tools` on Windows).
- Install: `dotnet tool install -g grok-cli`
- Update: `dotnet tool update -g grok-cli`

## Run
```bash
grok
```
Or from source:
```bash
dotnet run --project GrokCLI/GrokCLI.tui
```

## Configure
- Set `XAI_API_KEY` in the environment (recommended) or place `grok.config.json` in the working directory with:
  ```json
  {
    "XAI_API_KEY": "your_key_here"
  }
  ```
- Optional: add `allowed_commands`, `blocked_commands`, and `pre_prompt`. If `GROK.md` exists in the working directory or ancestors, its contents are appended to the pre-prompt.

## Usage
- Chat normally; Ctrl+Enter inserts a newline.
- `cmd <command>` or `/cmd <command>` runs shell commands (subject to allow/block rules). `clear` or `/clear` clears the terminal.
- Toggle modes: type `debug` or `normal` in chat, or use `--mode normal|debug`, `--debug`, `--normal`, or `GROK_MODE`.
- Tools: python (`code_execution`), shell (`run_command`), search (ripgrep/grep/PowerShell), file read (`read_local_file` with 200 KB limit and working-dir sandbox), file edit (`edit_file` replace/insert/append/delete/write with optional backups), change directory, tests, workflow completion marker, placeholder web search.

## Safety and defaults
- If `XAI_API_KEY` is missing, chat is locked and guidance is shown.
- Default pre-prompt requests the model to call `workflow_done` when finished and to follow `GROK.md` if present.
- Destructive commands are blocked by default; other commands may prompt for approval.
