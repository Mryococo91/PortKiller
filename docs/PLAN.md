# Phase plan — Port Killer

Operational copy of the technical plan. Each Cursor conversation that
receives `Lance la phase X` must follow this document **and** the real code.

Execution rules:

1. Read `docs/STATUS.md` then this file.
2. Verify the real code (source of truth).
3. If the previous phase is not done, stop and list the gaps.
4. Implement only the requested phase.
5. Build (`dotnet build`).
6. Update `docs/STATUS.md`.

Never launch `netstat`, `tasklist`, or `taskkill`.

---

## Phase 1 — Continuity, cleanup, structure, models

**Goal:** Make the project self-documented and introduce types, with no network logic.

**Initial state:** Compilable WinUI 3 template, empty `MainPage`.

**Work:**

- Create `AGENTS.md`, `docs/PLAN.md`, `docs/ARCHITECTURE.md`, `docs/STATUS.md`
- Create `Native/`, `Models/`, `Services/`, `ViewModels/`, `Views/`, `Helpers/`
- Move `MainPage` to `Views/`
- Add `CommunityToolkit.Mvvm`
- Models: `NetworkProtocol`, `TcpConnectionState`, `PortEndpoint`, `ProcessIdentity`, `PortEntry`
- Remove `systemAIModels`; clean up `App.xaml.cs`

**Done when:** build OK; docs present; no network API yet

---

## Phase 2 — Native port enumeration (IP Helper)

**Goal:** Get port + protocol + address + state + PID without a shell.

**Work:**

- `Native/IpHelperNative.cs`: `LibraryImport` `GetExtendedTcpTable` / `GetExtendedUdpTable`
- OWNER_PID structs for IPv4/IPv6 TCP+UDP
- Size/buffer loop; `ntohs` conversion
- `Services/TcpUdpTableReader.cs` → `IReadOnlyList<PortEndpoint>`
- Read off the UI thread

**Done when:** an IPv4 TCP listener (e.g. port 3000) appears with the right PID; IPv6/UDP do not crash

---

## Phase 3 — Process enrichment and snapshot

**Goal:** Join PID → name / path / StartTime.

**Work:**

- `Native/ProcessNative.cs`: `OpenProcess`, `QueryFullProcessImageNameW`, `CloseHandle`
- `ProcessInfoService` with per-scan cache
- `PortSnapshotService.GetSnapshotAsync`

**Done when:** user process: name + path without admin; SYSTEM: no crash if path is empty

---

## Phase 4 — List UI + manual refresh

**Goal:** Clear table + Refresh.

**Work:**

- `MainViewModel`: collection, `RefreshCommand`, busy/empty/error states
- `ListView` + header: Port, Protocol, State, Local address, PID, Process
- Business filter listening+UDP; toggle all TCP connections
- F5 / Refresh button

**Done when:** readable list in light/dark; no UI freeze during the scan

---

## Phase 5 — Search

**Goal:** Filter port, PID, name, path in memory.

**Work:**

- Visible search field
- `Helpers/PortEntryFilter.cs`
- Result counter

**Done when:** `3000` isolates the port; `node` isolates processes

---

## Phase 6 — Terminate a process

**Goal:** Safe kill with confirmation.

**Work:**

- Button + Delete, disabled for PID 0/4/self
- Confirmation `ContentDialog`
- `ProcessTerminationService`: existence, StartTime+name identity, `Kill()`, typed errors
- Refresh after success

**Done when:** killing a user process works; clear messages if already gone / Access Denied / reused PID

---

## Phase 7 — Details pane

**Goal:** Selection shows the process and all of its ports (full snapshot, not only the filter).

**Work:**

- Side pane: name, PID, path, StartTime, ports
- Copy PID / path
- Hide when nothing is selected

**Done when:** a multi-port process shows all of its ports in the details pane

---

## Phase 8 — Auto-refresh, keyboard, permissions, polish

**Goal:** Finished daily utility.

**Work:**

- Auto-refresh toggle 5 s, pause if the window is not visible
- Shortcuts F5, Ctrl+F, `/`, Delete, Escape
- Non-admin banner + relaunch as administrator (never automatic)
- DisplayName "Port Killer"
- Release trimming: disable if it breaks P/Invoke

**Done when:** light at idle; usable from the keyboard; UAC only on request

---

## Phase 9 — Localization

**Goal:** UI follows the Windows display language; repository language is English.

**Work:**

- `Strings/en-US/Resources.resw` (default) and `Strings/fr-FR/Resources.resw`
- `x:Uid` for XAML; `AppStrings` for code
- System language by default; in-app override (System / English / Français)
- Persist override in `%LocalAppData%\PortKiller\language.txt`
- Translate source comments, scripts, installer strings, and docs to English

**Done when:** a French Windows session shows French UI; an English (or unknown) session falls back to English; code and docs are English

---

## NuGet

Required (template): `Microsoft.WindowsAppSDK`, `Microsoft.Windows.SDK.BuildTools`, `Microsoft.Windows.SDK.BuildTools.WinApp`.

Added: `CommunityToolkit.Mvvm`.
