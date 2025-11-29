# Engineering Guidelines

- Do not add code comments to the codebase.
- All code (identifiers, strings, docs) must be written in English.

# GrokCLI Functionality Summary (keep updated as features change)

- Terminal UI chat client for Grok 4.1 Fast with streaming responses and keyboard shortcuts for sending, scrolling, clearing, and quitting.
- Agentic chat loop with tool-calling support, including Python code execution plus placeholder web search, and local helpers for file reading/editing, search, tests, and directory changes.
- Tool call for running arbitrary CLI commands (e.g., build/test workflows) in the working directory via the system shell.
- Tool call to signal completion, rendering a terminal-width “Worked for XXm XXs” line.
- Optional `pre_prompt` config value in `grok.config.json` sends a system prompt before user messages.
- Allowed CLI commands can be restricted via `allowed_commands` in `grok.config.json`; unlisted commands prompt the user to allow once, allow always, or deny.
- Blocked CLI commands via `blocked_commands` in `grok.config.json`; blocked commands are rejected immediately, and the prompt offers “never run this command” to add to the blocklist.
- Shell command execution from the chat via `/cmd ...` (with debug output in Debug mode and compact summaries in Normal mode).
- Two display modes: Normal (summarized tool output) and Debug (full tool call logs), selectable via CLI flags (`--mode normal|debug`, `--debug`, `--normal`) or `GROK_MODE`.
- API key handling: locks the chat when `XAI_API_KEY` is missing, shows guidance, and in Debug builds auto-copies `grok.config.json` from the repo into the working directory when present.
