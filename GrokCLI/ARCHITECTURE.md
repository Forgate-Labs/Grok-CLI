# GrokCLI - Architecture

## Overview

GrokCLI is a terminal user interface (TUI) application that implements a conversational agent using the Grok 4.1 Fast model from xAI with agentic capabilities (tool execution).

## Project Structure

```
GrokCLI.tui/
├── Models/              # Data models
│   ├── ToolCallInfo.cs
│   └── ToolExecutionResult.cs
├── Services/            # Business logic
│   ├── IGrokClient.cs
│   ├── GrokClient.cs
│   ├── IToolExecutor.cs
│   ├── ToolExecutor.cs
│   ├── IChatService.cs
│   └── ChatService.cs
├── Tools/               # Agentic tools
│   ├── ITool.cs
│   ├── CodeExecutionTool.cs
│   └── WebSearchTool.cs
├── UI/                  # User interface
│   ├── ChatViewController.cs
│   └── ChatWindow.cs
└── Program.cs           # Entry point and DI setup
```

## Application Layers

### 1. **Models**
POCO classes representing domain data:
- `ToolCallInfo`: Information about a tool call
- `ToolExecutionResult`: Result of a tool execution

### 2. **Tools**
Agentic tools that Grok can use:
- `ITool`: Base interface for all tools
- `CodeExecutionTool`: Executes Python code locally
- `WebSearchTool`: Performs web search (placeholder)

Each tool:
- Defines its JSON schema for Grok
- Implements the execution logic
- Returns structured results

### 3. **Services**
Business logic and orchestration:

#### `GrokClient`
- Abstraction over the xAI API
- Manages connection and streaming

#### `ToolExecutor`
- Executes tools based on name
- Uses DI to resolve available tools

#### `ChatService`
- Orchestrates the full agentic loop:
  1. Sends a message to Grok
  2. Detects tool calls in the stream
  3. Executes tools locally
  4. Sends results back to Grok
  5. Repeats until there are no more tool calls
- Emits events for the UI (`OnTextReceived`, `OnToolCalled`, `OnToolResult`)

### 4. **UI**
User interface separated from logic:

#### `ChatViewController`
- Controls interaction between UI and services
- Subscribes to `ChatService` events
- Manages conversation state
- Updates the UI in response to events

#### `ChatWindow`
- Terminal.Gui visual components
- Window layout, input fields, labels
- Keyboard shortcuts configuration

### 5. **Program.cs**
Application entry point:
- Sets up Dependency Injection
- Registers services and tools
- Initializes Terminal.Gui

## Patterns Used

### Dependency Injection (DI)
All services are registered in the DI container:
```csharp
services.AddSingleton<ITool, CodeExecutionTool>();
services.AddSingleton<IGrokClient>(sp => new GrokClient(apiKey));
services.AddSingleton<IToolExecutor, ToolExecutor>();
services.AddSingleton<IChatService, ChatService>();
```

### Repository/Service Pattern
- Services encapsulate business logic
- Tools implement a common interface
- UI consumes services via DI

### Observer Pattern
`ChatService` emits events that the UI observes:
- `OnTextReceived`: Text streaming from Grok
- `OnToolCalled`: Tool being called
- `OnToolResult`: Tool result

### Strategy Pattern
Tools implement `ITool` and are interchangeable:
- Easy to add new tools
- `ToolExecutor` resolves them dynamically

## Execution Flow

1. **User types a message** → `ChatViewController.SendMessageAsync()`
2. **Controller calls service** → `ChatService.SendMessageAsync()`
3. **Service sends to Grok** → `GrokClient.StreamChatAsync()`
4. **Streaming returns updates**:
   - Text → `OnTextReceived` → UI updates
   - Tool call → `OnToolCalled` → UI shows tool
5. **Service executes tool** → `ToolExecutor.ExecuteAsync()`
6. **Result goes back to Grok** → Loop continues
7. **Final response** → UI displays

## How to Add a New Tool

1. Create a class that implements `ITool` in `/Tools`
2. Implement:
   - `Name`: Tool name
   - `Description`: Description
   - `GetChatTool()`: JSON schema
   - `ExecuteAsync()`: Execution logic
3. Register it in DI in `Program.cs`:
   ```csharp
   services.AddSingleton<ITool, MyNewTool>();
   ```

## Technologies

- **.NET 10**: Base framework
- **OpenAI SDK 2.7.0**: Communication with xAI API
- **Terminal.Gui 1.19.0**: Terminal UI
- **Microsoft.Extensions.DependencyInjection 10.0.0**: DI container

## SOLID Principles

- **S**ingle Responsibility: Each class has a clear responsibility
- **O**pen/Closed: Tools can be added without modifying existing code
- **L**iskov Substitution: All tools are replaceable via `ITool`
- **I**nterface Segregation: Small, focused interfaces
- **D**ependency Inversion: Dependencies via interfaces, not implementations
