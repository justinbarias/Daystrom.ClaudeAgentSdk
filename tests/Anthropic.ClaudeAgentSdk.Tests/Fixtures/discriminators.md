# Wire-protocol discriminator reference

**Source:** Python SDK `claude-agent-sdk-py` v0.1.72 (commit `b512f25`),
checked out under `external/claude-agent-sdk-py/`. Paired with bundled CLI
`2.1.126` (`eng/Versions.props`).

This is the **authoritative reference** for `[JsonPolymorphic]` and
`[JsonDerivedType(typeDiscriminator: "...")]` strings on the .NET side.
Every wire-polymorphic record in `src/Anthropic.ClaudeAgentSdk/` must match
a row here. When the CLI version bumps, audit this file before touching any
`[JsonDerivedType]` attribute (see `external/README.md`).

---

## Top-level `Message` (discriminator: `type`)

Source: `_internal/message_parser.py:55–285` (the `match message_type:` block).

| .NET record | Wire string | Python class |
|---|---|---|
| `UserMessage` | `user` | `UserMessage` (types.py:962) |
| `AssistantMessage` | `assistant` | `AssistantMessage` (types.py:972) |
| `SystemMessage` | `system` | `SystemMessage` (types.py:987) — see "System subtypes" below |
| `ResultMessage` | `result` | `ResultMessage` (types.py:1078) |
| `StreamEvent` | `stream_event` | `StreamEvent` (types.py:1099) |
| `RateLimitEvent` | `rate_limit_event` | `RateLimitEvent` (types.py:1142) |

**Wire wrapping:** `UserMessage`, `AssistantMessage` — the wire JSON nests
content under a `message` object. Example:

```json
{ "type": "assistant",
  "message": { "model": "...", "content": [...], "usage": {...} },
  "session_id": "...", "uuid": "..." }
```

The Python dataclass flattens. The .NET record must preserve the wire
shape (the source-gen serializer doesn't support dynamic flattening), so
expose the inner body as a nested record:
`AssistantMessage.Message` returns an `AssistantMessageBody`.

**Forward compatibility:** the parser logs and skips unknown `type` values
(message_parser.py:281). The .NET `MessageParser` (Phase 5) must do the
same — unknown variants do not throw.

## System subtypes (`type: "system"`, sub-discriminator: `subtype`)

Source: `_internal/message_parser.py:164–220`.

| .NET record | `subtype` value |
|---|---|
| `TaskStartedMessage` | `task_started` |
| `TaskProgressMessage` | `task_progress` |
| `TaskNotificationMessage` | `task_notification` |
| `MirrorErrorMessage` | `mirror_error` |
| `SystemMessage` (catch-all) | any other value |

Strategy: in .NET, `SystemMessage` is a regular record, and the four
subtypes inherit. Discrimination by `subtype` is a *second* polymorphism
level — handled inside a custom hook in `MessageParser` (Phase 5) since
STJ doesn't natively chain two discriminators on one type.

## `ContentBlock` (discriminator: `type`)

Source: `_internal/message_parser.py:64–146` and `types.py:867–947`.

| .NET record | Wire string |
|---|---|
| `TextBlock` | `text` |
| `ThinkingBlock` | `thinking` |
| `ToolUseBlock` | `tool_use` |
| `ToolResultBlock` | `tool_result` |
| `ServerToolUseBlock` | `server_tool_use` |
| `ServerToolResultBlock` | `advisor_tool_result` ⚠ |

⚠ **`ServerToolResultBlock` wire string is `advisor_tool_result`, NOT
`server_tool_result`** — message_parser.py:140 is the source of truth. The
type name on both sides reflects the broader category but the discriminator
matches what the CLI actually emits.

## `HookInput` (discriminator: `hook_event_name`, snake-case + PascalCase mix)

Source: `types.py:265–365`.

`hook_event_name` values are **PascalCase**, not snake-case (the rest of
the wire is snake-case). Override on the property with `[JsonPropertyName]`
to keep the source-gen `SnakeCaseLower` policy from mangling.

| .NET record | `hook_event_name` value |
|---|---|
| `PreToolUseHookInput` | `PreToolUse` |
| `PostToolUseHookInput` | `PostToolUse` |
| `PostToolUseFailureHookInput` | `PostToolUseFailure` |
| `UserPromptSubmitHookInput` | `UserPromptSubmit` |
| `StopHookInput` | `Stop` |
| `SubagentStopHookInput` | `SubagentStop` |
| `SubagentStartHookInput` | `SubagentStart` |
| `PreCompactHookInput` | `PreCompact` |
| `NotificationHookInput` | `Notification` |
| `PermissionRequestHookInput` | `PermissionRequest` |

## Hook-specific outputs (discriminator: `hookEventName` — camelCase)

Source: `types.py:369–438`.

⚠ Output uses **camelCase `hookEventName`**, not snake-case
`hook_event_name` — opposite-direction asymmetry from the input side.

| .NET record | `hookEventName` value |
|---|---|
| `PreToolUseHookSpecificOutput` | `PreToolUse` |
| `PostToolUseHookSpecificOutput` | `PostToolUse` |
| `PostToolUseFailureHookSpecificOutput` | `PostToolUseFailure` |
| `UserPromptSubmitHookSpecificOutput` | `UserPromptSubmit` |
| `SessionStartHookSpecificOutput` | `SessionStart` |
| `NotificationHookSpecificOutput` | `Notification` |
| `SubagentStartHookSpecificOutput` | `SubagentStart` |
| `PermissionRequestHookSpecificOutput` | `PermissionRequest` |

## `McpServerConfig` (discriminator: `type`)

Source: `types.py:549–584`.

| .NET record | Wire string | Notes |
|---|---|---|
| `McpStdioServerConfig` | `stdio` | `type` is `NotRequired` for backwards compat — accept missing as `stdio`. |
| `McpSseServerConfig` | `sse` | |
| `McpHttpServerConfig` | `http` | |
| `McpSdkServerConfig` | `sdk` | Holds in-process `IMcpServerInstance` — wire shape only carries `name`. |

Status responses additionally include:

| .NET record | Wire string | Notes |
|---|---|---|
| `McpClaudeAIProxyServerConfig` | `claudeai-proxy` | Output-only. types.py:604. |

## `ThinkingConfig` (discriminator: `type`)

Source: `types.py:1441–1456`.

| .NET record | Wire string |
|---|---|
| `ThinkingConfigAdaptive` | `adaptive` |
| `ThinkingConfigEnabled` | `enabled` |
| `ThinkingConfigDisabled` | `disabled` |

## `PermissionResult` (discriminator: `behavior` — NOT `type`!)

Source: `types.py:189–208`.

⚠ Discriminator is **`behavior`**, not `type` (deviation from spec §11
which assumed `type` for all unions). Update spec-derived code accordingly.

| .NET record | Wire string |
|---|---|
| `PermissionResultAllow` | `allow` |
| `PermissionResultDeny` | `deny` |

## `PermissionUpdate` (discriminator: `type`, camelCase values)

Source: `types.py:118–134`.

| Wire string |
|---|
| `addRules` |
| `replaceRules` |
| `removeRules` |
| `setMode` |
| `addDirectories` |
| `removeDirectories` |

These are **camelCase**, not snake-case. Apply `[JsonStringEnumMemberName]`
or property-level overrides as needed.

`PermissionUpdateDestination` (types.py:103) is a string literal:
`userSettings`, `projectSettings`, `localSettings`, `session`. Also
camelCase.

## `PermissionBehavior` enum (types.py:107)

Values: `allow`, `deny`, `ask`. Lower-snake (no separator).

## `HookEvent` enum

Values match the `hook_event_name` table above (PascalCase).

## `PermissionMode` enum (types.py:24–26)

Values: `default`, `acceptEdits`, `plan`, `bypassPermissions`, `dontAsk`,
`auto`. Mixed: lowercase + camelCase.

## `SettingSource` enum (types.py:32)

Values: `user`, `project`, `local`. Lowercase.

## `SdkBeta` enum (types.py:29)

Values: `context-1m-2025-08-07`. Hyphenated date string.

## `RateLimitStatus` (types.py:1109)

Values: `allowed`, `allowed_warning`, `rejected`. Snake-case.

## `RateLimitType` (types.py:1110–1112)

Values: `five_hour`, `seven_day`, `seven_day_opus`, `seven_day_sonnet`,
`overage`. Snake-case.

## `ServerToolName` (types.py:900–909)

Values: `advisor`, `web_search`, `web_fetch`, `code_execution`,
`bash_code_execution`, `text_editor_code_execution`, `tool_search_tool_regex`,
`tool_search_tool_bm25`. Snake-case.

## `McpServerConnectionStatus` (types.py:654–656)

Values: `connected`, `failed`, `needs-auth`, `pending`, `disabled`.
⚠ `needs-auth` is **kebab-case**, not snake-case.

## `TaskNotificationStatus` (types.py:1003)

Values: `completed`, `failed`, `stopped`. Lowercase.

## `ThinkingDisplay` (types.py:1438)

Values: `summarized`, `omitted`. Lowercase.

## `AssistantMessageError` (types.py:951–958)

Values: `authentication_failed`, `billing_error`, `rate_limit`,
`invalid_request`, `server_error`, `unknown`. Snake-case.

---

## Field-level naming exceptions

The wire format is *predominantly* snake_case_lower, but several types use
camelCase. The .NET source-gen context applies `SnakeCaseLower` as the
default policy; these properties need explicit `[JsonPropertyName]`.

### `SandboxSettings` (types.py:820–863) — entirely camelCase

`enabled`, `autoAllowBashIfSandboxed`, `excludedCommands`,
`allowUnsandboxedCommands`, `network`, `ignoreViolations`,
`enableWeakerNestedSandbox`.

### `SandboxNetworkConfig` (types.py:782–805) — entirely camelCase

`allowedDomains`, `deniedDomains`, `allowManagedDomainsOnly`,
`allowUnixSockets`, `allowAllUnixSockets`, `allowLocalBinding`,
`allowMachLookup`, `httpProxyPort`, `socksProxyPort`.

### `RateLimitInfo` wire fields (message_parser.py:265–271)

Wire is camelCase even though the Python dataclass uses snake_case:
- `resetsAt` (Python `resets_at`)
- `rateLimitType` (Python `rate_limit_type`)
- `overageStatus` (Python `overage_status`)
- `overageResetsAt` (Python `overage_resets_at`)
- `overageDisabledReason` (Python `overage_disabled_reason`)

`status`, `utilization` are lowercase (consistent).

### `ResultMessage.modelUsage` (message_parser.py:236)

Wire is camelCase `modelUsage`; Python dataclass uses `model_usage`.

### `ContextUsageResponse` (types.py:706–768) — almost entirely camelCase

`categories`, `totalTokens`, `maxTokens`, `rawMaxTokens`, `percentage`,
`model`, `isAutoCompactEnabled`, `memoryFiles`, `mcpTools`, `agents`,
`gridRows`, `autoCompactThreshold`, `deferredBuiltinTools`, `systemTools`,
`systemPromptSections`, `slashCommands`, `skills`, `messageBreakdown`,
`apiUsage`.

### `McpServerStatus` (types.py:659–684)

`name`, `status`, `error`, `config`, `scope`, `tools` lowercase;
`serverInfo` camelCase.

### `McpToolAnnotations` (types.py:627–635) — camelCase

`readOnly`, `destructive`, `openWorld`.

### `ContextUsageCategory` (types.py:697–703)

`name`, `tokens`, `color` lowercase; `isDeferred` camelCase.

### Hook-specific output fields — all camelCase

(`permissionDecision`, `permissionDecisionReason`, `updatedInput`,
`additionalContext`, `updatedMCPToolOutput`, `decision`, ...). See
`types.py:369–438`.

### `SyncHookJSONOutput` (types.py:463–504)

Mixed: `decision`, `reason` lowercase; `suppressOutput`, `stopReason`,
`systemMessage`, `hookSpecificOutput` camelCase. The Python `continue_`
and `async_` are renamed `continue` and `async` (C# keywords —
`[JsonPropertyName("continue")]` and `[JsonPropertyName("async")]`).

### Control-protocol request shapes (types.py:1781–1879)

Mixed snake_case + camelCase per request type. `serverName` is camelCase
on `mcp_reconnect` and `mcp_toggle` (called out explicitly in types.py
comments at line 1829 and 1837). Most other fields are snake_case.

### `PermissionUpdate.to_dict` outputs camelCase (types.py:136–170)

`toolName` and `ruleContent` for `addRules`/`replaceRules`/`removeRules`
variants.

---

## Open / deferred verification

These items couldn't be audited from the offline Python source alone and
need live-CLI capture (`eng/capture-fixtures.ps1` — run when an
`ANTHROPIC_API_KEY` is available):

- Whether the CLI ever actually emits `mirror_error` system messages or
  whether they're SDK-synthesized only (`message_parser.py:204` comment
  says SDK-synthesized — confirm via capture).
- Real shape of `streamingEvent.event` (currently `dict[str, Any]` opaque).
- Whether `ServerToolResultBlock` wire strings besides `advisor_tool_result`
  exist for non-advisor server tools (the hard-coded match in
  `message_parser.py:140` only handles `advisor_tool_result`; the type
  enumeration in `types.py:900–909` lists 8 server tools).
