# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Club Poker — a mobile poker game client built with Unity targeting Android. The project uses Socket.io for real-time game state and a REST API for game management, authentication, and economy.

## Build & Development Commands

This is a Unity project. There is no CLI build command for the editor — use Unity Editor directly. However:

```bash
# Build APK via Unity CLI (headless)
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity \
  -quit -batchmode -projectPath . \
  -buildTarget Android \
  -executeMethod BuildScript.BuildAndroid \
  -logFile build.log

# Run tests via Unity Test Runner (headless)
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity \
  -quit -batchmode -projectPath . \
  -runTests -testPlatform EditMode \
  -logFile test.log
```

The build artifact is `clubpoker.apk`. Signing uses `Poker.keystore`.

## Architecture

### Scene Flow

```
Bootstrap → Splash (load config) → Login/Register → Lobby → GameTable
```

Scenes: `Bootstrap`, `Scene_Splash`, `Scene_Login`, `Scene_Register`, `Scene_Lobby`, `Scene_GameTable`, `Scene_Profile`, `Scene_Settings`

Scene transitions are managed by `GameSceneManager` (with loading screen) and loaded via Addressables.

### Module Structure

Scripts are split into separate assembly definitions:

| Assembly | Path | Responsibility |
|---|---|---|
| `ClubPoker.Core` | `Scripts/Core/` | App init, config, scene management, feature flags |
| `ClubPoker.Auth` | `Scripts/Auth/` | Auth flows, token storage |
| `ClubPoker.Networking` | `Scripts/Networking/` | HTTP client, WebSocket, caching |
| `ClubPoker.Game` | `Scripts/Game/` | Table join, reconnect logic |
| `ClubPoker.Lobby` | `Scripts/Lobby/` | Lobby state |
| `ClubPoker.UI` | `Scripts/UI/` | Shared UI components |

Profile/game screen components live in `Scripts/ProfileScreen/`.

### Core Systems

**Config** (`Scripts/Core/`)
- `AppInitializer.cs` — Bootstrap entry point; loads `AppConfig` and feature flags at startup
- `AppConfig.cs` — ScriptableObject loaded via Addressables; contains `apiBaseUrl`, `webSocketUrl`, `environmentName`, `logLevel`, `featureFlags[]`
- `ConfigManager.cs` — Loads `AppConfig` via Addressables
- `FeatureFlagManager.cs` — Named boolean flags evaluated at runtime

**Authentication** (`Scripts/Auth/`)
- `AuthManager.cs` — Singleton for all auth flows: login, register, logout, guest sessions, profile, avatars, chips, tables, leaderboard. Also owns the token refresh concurrency lock (`SemaphoreSlim`)
- `TokenStore.cs` — AES-256-CBC encrypted JWT storage; key is `SHA256(deviceUniqueIdentifier + salt)` with a fresh random IV per write. Only `AuthManager` should read/write this.
- `AuthViewModels.cs` — Result types returned by auth operations

**Networking** (`Scripts/Networking/`)
- `ApiClient.cs` — HTTP REST client; `Get<T>()`, `Post<T>()`, `Put<T>()`, `Delete<T>()`. Intercepts 401s to trigger token refresh, retries with exponential backoff (1s → 2s → 4s), 10-second timeout, TTL response caching
- `SocketManager.cs` — Socket.io v4 WebSocket client; JWT sent in handshake `auth: { token }`. Connection state machine: `Disconnected → Connecting → Connected → Reconnecting`. 60-second reconnect grace period (12 attempts × 5s). Events: `OnStateChanged`, `OnAuthenticated`, `OnReconnectFailed`, `OnAppBackgrounded`
- `ApiException.cs` — Typed exception hierarchy keyed by error code prefix: `A*` (Auth), `G*` (Game), `V*` (Validation), `E*` (Economy), `L*` (Lobby), `S*` (System)
- `ResponseCache.cs` — TTL-based cache for API responses
- `NetworkMonitor.cs` — Connectivity detection; drives `OfflineBanner`
- `UnityThread.cs` — Marshals callbacks from background threads to the Unity main thread

**Game** (`Scripts/Game/`)
- `TableJoinHandler.cs` — Orchestrates table join: waits for buy-in REST call, emits `player:join_table` after `socket:authenticated`, 10-second timeout for `game:state_update`
- `ReconnectHandler.cs` — On app resume: fetches one-time token (`POST /api/reconnect/token`), emits `player:reconnect`, handles A005 rejection and countdown UI

### Key API Endpoints

| Operation | Method | Path |
|---|---|---|
| Login | POST | `/api/auth/login` |
| Register | POST | `/api/auth/register` |
| Token refresh | POST | `/api/auth/refresh` |
| Guest session | POST | `/api/auth/guest` |
| Player profile | GET/PUT | `/api/player/profile` |
| Avatars | GET | `/api/player/avatars` |
| Tables | GET | `/api/tables` |
| Buy-in | POST | (via AuthManager) |
| Reconnect token | POST | `/api/reconnect/token` |
| Leaderboard | GET | `/api/leaderboard/global`, `/api/leaderboard/weekly` |

### Socket.io Events

| Direction | Event |
|---|---|
| Server → Client | `socket:authenticated`, `game:state_update`, `game:error`, `game:your_cards`, `game:player_reconnected` |
| Client → Server | `player:join_table`, `player:reconnect`, `player:action` |

### Key Packages

- **UniTask** (`Cysharp`) — async/await throughout; prefer `UniTask` over `Task`
- **SocketIOUnity** — Socket.io v4 client
- **Newtonsoft.Json** — JSON serialization/deserialization
- **Addressables** — Asset and scene loading
- **DOTween** — Animations
- **URP 14** — Rendering pipeline

## Coding Conventions

- All async methods use `UniTask` / `UniTaskVoid`, not `Task` or coroutines
- Singletons (AuthManager, SocketManager, etc.) are accessed via `.Instance`
- Views do not call API or socket directly — they go through the relevant Manager
- `TokenStore` is private to `AuthManager`; never access it from other classes
- Error codes follow the `[A|G|V|E|L|S]NNN` prefix scheme — match exceptions to the right domain type in `ApiException`
- Guest users are blocked from: leaderboard, hand history, profile editing
