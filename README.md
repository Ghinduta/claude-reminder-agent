# Claude Reminder Agent

A small C# console app for learning Claude tool use. You type requests like *"remind me about my doctor's appointment a week from Thursday"* and Claude calls local tools to work out the exact date and save the reminder.

It uses the official [Anthropic C# SDK](https://www.nuget.org/packages/Anthropic) (`Anthropic` on NuGet) and the Messages API. The same agent comes in two versions so you can compare them:

| Mode | Class | Who runs the tool-use loop |
|---|---|---|
| `Manual` (default) | `Agent/ManualAgent.cs` | Our code: send, check `StopReason == "tool_use"`, run tools, return `tool_result`s, repeat |
| `ToolRunner` | `Agent/ToolRunnerAgent.cs` | The SDK's beta `BetaToolRunner`, on a server-side message thread |

## Tools

| Tool | What it does |
|---|---|
| `get_current_datetime` | Returns the local date and time, with an optional .NET format string |
| `add_duration_to_datetime` | Adds hours, days, weeks or months to a datetime |
| `set_reminder` | Saves a reminder to `reminders.json` (git-ignored) |
| `web_search` | Anthropic's server-side web search (`web_search_20260209`), for public event dates. Runs on Anthropic's servers, max 3 searches per request |
| `str_replace_based_edit_tool` | Anthropic's text editor tool (`text_editor_20250728`): `view`, `create`, `str_replace`, `insert` on notes in `notes/` (git-ignored). Paths outside `notes/` and links are refused; `create` never overwrites |

## Run it

You need the .NET 10 SDK and an Anthropic API key.

```powershell
cd src/ReminderAgent

# Store the key once with user-secrets. It is saved in your user profile, not in the repo.
dotnet user-secrets set ANTHROPIC_API_KEY "sk-ant-..."
# or set it just for this shell session:
# $env:ANTHROPIC_API_KEY = "sk-ant-..."

dotnet run                                # manual loop
dotnet run -- --AGENT_MODE ToolRunner     # SDK tool runner
```

`AGENT_MODE` can also be set as an environment variable. In VS Code, pick the loop in the Run and Debug dropdown, then press F5.

Type `exit` to quit. Lines starting with `[tool]` and `[result]` show each tool call and what it returned.

## Layout

```
src/ReminderAgent/
  Program.cs                          wires everything together; picks the agent from AGENT_MODE
  Configuration/AppSettings.cs        API key + AGENT_MODE from user-secrets, env vars, command line
  Agent/IAgent.cs                     what ConsoleChat talks to
  Agent/ManualAgent.cs                hand-written agent loop
  Agent/ToolRunnerAgent.cs            same agent on the SDK's BetaToolRunner
  Agent/RunnableToolAdapter.cs        lets the SDK runner call our tools
  Agent/ContentBlockExtensions.cs     response block -> request block (manual loop history)
  Agent/AgentSettings.cs              model, max tokens, max tool rounds (shared)
  Agent/SystemPrompt.cs               system prompt (shared)
  Tools/IAgentTool.cs                 tool contract: definition + Execute
  Tools/ToolExecutor.cs               finds a tool by name, runs it, turns failures into errors
  Tools/ToolInput.cs                  typed input helpers that throw Claude-readable errors
  Tools/IsoDateTime.cs                shared ISO 8601 format/parse
  Tools/GetCurrentDateTimeTool.cs     get_current_datetime
  Tools/AddDurationToDateTimeTool.cs  add_duration_to_datetime
  Tools/SetReminderTool.cs            set_reminder
  Tools/TextEditorTool.cs             str_replace_based_edit_tool, confined to notes/
  Reminders/Reminder.cs               reminder record
  Reminders/ReminderStore.cs          reads/writes reminders.json
  Ui/ConsoleChat.cs                   read-eval-print loop
  Ui/ConsoleView.cs                   all console input/output
```
