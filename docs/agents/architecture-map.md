# Architecture reading map

[`docs/architecture.md`](../architecture.md) is a reference manual, not required reading for every ticket. Load only the sections relevant to the task.

## How to use

1. Find the topic in the map below.
2. Read the linked ADR first when one is listed.
3. Read only that section of `architecture.md` with `view_range` or a heading search.
4. Refresh the line numbers after edits by searching for `^## ` headings (the grep tool or `Select-String`).

## Map

| Topic | Read | Related |
| --- | --- | --- |
| 启动序列 | `启动序列` (lines 5–96): state machine, splash timing, degradation ladder, handoff, startup failures | [ADR-0002](../adr/0002-splash-startup-sequence.md) |
| 单实例与窗口生命周期 | `单实例与窗口生命周期` (97–172): mutex/pipe, activation, tray, hidden-window errors | [#7](https://github.com/ZLiLiang/JiuMi/issues/7) |
| 错误呈现与诊断 | `错误呈现与诊断` (173–269): levels, carriers, `ErrorRecord`, global hooks, notifications | [ADR-0003](../adr/0003-error-presentation.md) |
| 日志与诊断 | `日志与诊断` (270–377): Serilog boundary, location, levels, fields, redaction, bundle | [ADR-0004](../adr/0004-logging.md) |
| 配置与外置文件 | `配置与外置文件` (378–447): locations, startup probe, file contract, atomic write, visibility | [ADR-0001](../adr/0001-settings-persistence.md), [ADR-0005](../adr/0005-window-state-memory.md) |
| 本地化 | `本地化` (448–546): files, key naming, placeholders, language list, fallback, tests | [#3](https://github.com/ZLiLiang/JiuMi/issues/3) |

## Stop rules

- Do not read `architecture.md` for a code-style-only change; `docs/coding-guidelines/csharp.md` is enough.
- If a ticket touches one subsystem, read that row plus its ADR.
- If a ticket spans several subsystems, read only the affected rows; do not default to the whole file.
- Product contracts remain in `architecture.md` and the ADRs. `csharp.md` is subordinate to them.
