# GrokCLI

Terminal UI chat client for Grok 4.1 Fast with agentic (tool-calling) support, built on .NET and Terminal.Gui.

## Features
- Grok 4.1 Fast chat with streaming updates.
- Agentic loop with tool calls (Python code execution, web search placeholder).
- Terminal UI with keyboard shortcuts (Enter to send, Ctrl+↑/Ctrl+↓ scroll, Ctrl+L clear, Ctrl+Q exit).
- Safety: app locks when `XAI_API_KEY` is missing and shows an in-chat error; Debug build copies `grok.config.json` from the repo into the working directory if present.
- Display modes: Normal (summaries) and Debug (full tool call logs). Use `--mode normal|debug`, `--debug`, or `GROK_MODE=debug`.

## Requirements
- .NET 10 SDK
- xAI API key (`XAI_API_KEY`)

## Setup
Install the .NET SDK 10+ and ensure `~/.dotnet/tools` (Linux/macOS) or `%USERPROFILE%\.dotnet\tools` (Windows) is on your PATH.

### Install as a global tool
```bash
dotnet tool install -g grok
```

If you already have it installed and want the latest version:
```bash
dotnet tool update -g grok
```

### Configure the API key
Set the API key as an environment variable (recommended):
```bash
export XAI_API_KEY=your_key_here
```

Or add a `grok.config.json` in the repo root:
```json
{
  "XAI_API_KEY": "your_key_here"
}
```
In Debug builds, this file is auto-copied into the working directory at startup. In Release, only the environment variable (or a file you place manually) is used.

## Run
```bash
dotnet run --project GrokCLI/GrokCLI.tui
```

## Keybindings
- Enter: send message
- Ctrl+↑ / Ctrl+↓: scroll chat
- Ctrl+L: clear chat
- Ctrl+Q: exit

## Project Structure
- `GrokCLI/GrokCLI.tui`: TUI application
- `GrokCLI/GrokCLI.Tests`: tests (placeholder)
- `GrokCLI/ARCHITECTURE.md`: detailed architecture
- `AGENTS.md`: engineering rules (English code only, no code comments)

## Notes
- Tools: `CodeExecutionTool` (Python via `python3`), `WebSearchTool` (placeholder).
- Locked mode: if no API key is found, input is disabled and a message prompts you to set `XAI_API_KEY` (Ctrl+Q remains active).
