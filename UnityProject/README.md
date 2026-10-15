# Club Poker — Unity Client

Mobile poker client (Android + iOS) built in Unity. Talks to the backend over a
REST API (accounts, tables, clubs, economy) and Socket.io (live game state).

This README is the starting point for new developers: how to run the project,

---

## 1. Getting started

**Requirements**
- Unity **2022.3.62f2** (install through Unity Hub — other versions will re-import the whole project)
- Android Build Support, and iOS Build Support (on a Mac) — iOS is a primary platform, not an extra
- An IDE: VS Code with the Unity extension, or Rider

**First run**
1. Clone the repo and open the `UnityProject` folder in Unity Hub.
2. Wait for the first import (several minutes).
3. Open `Assets/Scenes/Bootstrap.unity` and press Play.

> Always press Play from **Bootstrap**. It loads the config and the managers
> every other scene depends on. Playing another scene directly will break.

---

## 2. Servers and config

Which server the app talks to is decided by **one field**: `configAddress` on the
`ConfigManager` object in `Bootstrap.unity` (e.g. `AppConfig_Dev`).

The configs live in `Assets/AddressableAssetsData/` (`Config_Dev`, `Config_Staging`,
`Config_Production`, `Config_Test`) and hold `apiBaseUrl`, `webSocketUrl`,
log level and feature flags.

| Server | Use |
|---|---|
| `https://www.poker.anivalegames.com:3005` | Dev — newest backend features |
| `http://143.110.247.128:3050` | Production — older backend code |

Configs are loaded through Addressables, so after changing a config value you
must **rebuild Addressables** (see Building) or the build keeps the old value.

---

## 3. Scene flow

```
Bootstrap → Splash → Login / Register → MainMenu → Lobby / Club → GameTable
```

- Only `Bootstrap` is in Build Settings. Every other scene is an **Addressable**
  and is loaded with:
  ```csharp
  GameSceneManager.Instance.LoadScene("Scene_MainMenu");
  ```
  The string is the Addressables address (`Scene_GameTable`), not the file name
  (`GameTableScene.unity`).
- Splash goes straight to MainMenu if a saved login session exists.

---

## 4. Code structure

All game code is in `Assets/Scripts/`, split into assemblies (`.asmdef`):

| Folder | Assembly | What's in it |
|---|---|---|
| `Core/` | ClubPoker.Core | App start, config, scenes, feature flags, toasts, messages, themes, sound triggers. **Every other assembly references Core.** |
| `Networking/` | ClubPoker.Networking | `ApiClient` (REST), `SocketManager` (Socket.io), payload models, errors |
| `Auth/` | ClubPoker.Auth | `AuthManager` — login + most API calls, token storage |
| `Game/` | ClubPoker.Game | Table: join, socket events, turn timer, seats, cards, pots |
| `Lobby/` | ClubPoker.Lobby | Lobby and table list |
| `UI/` | ClubPoker.UI | Shared screens and widgets (login, main menu, shop, toasts) |
| `Audio/` | ClubPoker.Audio | Sound effects and music playback |
| `Club/`, `ProfileScreen/` | (no asmdef) | Club screens, profile, leaderboard, daily bonus |
| `Tests/` | ClubPoker.Tests | EditMode unit tests |
| `Assets/Editor/` | Editor only | Tools menu helpers (themes, audio setup) |

**Rules everyone follows**
- **Async = UniTask.** Use `async UniTask` / `UniTaskVoid`, never `Task` or coroutines for new code.
- **Managers are singletons** reached through `.Instance` (`AuthManager.Instance`, `SocketManager.Instance`).
- **Views never call the API or socket directly.** A screen calls a Manager; the Manager calls `ApiClient` / `SocketManager`.
- **`TokenStore` is private to `AuthManager`.** Never read tokens anywhere else.
- **Low-level code doesn't reference high-level code.** Core can't use Game or Audio. If Core-level code needs to trigger something higher up, use a static event in Core (that's how `ToastEvents` and `GameAudio` work).
- **Guests** can't use leaderboard, hand history or profile editing — check before showing those.

---

## 5. Key systems

### REST API
`ApiClient.Instance.Get<T>() / Post<T>() / Put<T>() / Delete<T>()`. It adds the
auth token, refreshes it on a 401, retries with backoff (1s → 2s → 4s), times out
after 10s and can cache GET responses.

Most endpoints are wrapped in `AuthManager` (e.g. `GetClubTablesAsync`). Screens
call those wrappers.

**Errors** are thrown as typed exceptions picked by the error code's first letter:
`A…` AuthException, `G…` GameException, `V…` ValidationException,
`E…` EconomyException, `L…` LobbyException, `S…` SystemApiException,
plus NetworkException for connection problems.

### Socket.io (live game)
`SocketManager` holds one connection, sends the login token on connect and
reconnects for up to 60 seconds. Its handlers already run on the Unity main
thread, so you can touch UI inside them.

All table events are handled in **`Game/TableJoinHandler.cs`** — that's the first
place to look for anything that happens at the table.

### Showing text to the player
- Toast: `ToastEvents.Show(message);`
- Fixed wording goes in **`Core/GameMessages.cs`**, not inline in a screen — one
  list keeps wording consistent and will become the localisation keys.
- Text the server sends (error messages) is shown as-is.

### Themes (table skins, card decks)
`Core/Theme/`, catalog at `Assets/Resources/Theme/ThemeCatalog.asset`, tools under
**Tools ▸ ClubPoker ▸ Theme**. Theme ids are saved on players' devices — **never
rename an id after a build ships.**

### Sound and music
Code asks for a sound by name; the Audio module does the rest.

```csharp
GameAudio.Play(SoundId.Fold);        // sound effect
GameAudio.PlayMusic(SoundId.MusicTable); // only to override scene music
```

- `Assets/Audio/SoundLibrary.asset` — one row per sound: file, volume, pitch,
  spam limits, preload. Music rows also have the **Scene Music** list (scene → track).
- Button clicks play automatically for every Button/Toggle. Add the
  `NoClickSound` component to a button (or its parent) to silence it.
- Mute: `SoundSettings` (everything) and `MusicSettings` (music only).
- The app's only **AudioListener** lives on `AudioManager`. Listeners on scene
  cameras are switched off automatically when the scene loads — don't rely on them.

See **How to: add a sound** below.

### In-app purchases
Unity IAP 5.x. Store product ids **must equal** the package ids the server returns
from `/api/shop/packages` (e.g. `diamonds_60`). Android testers must be added in
Play Console → License testing, otherwise purchases fail with "item could not be found".

---

## 6. How to…

### …call a new API endpoint
1. Add request/response models in `Networking/Models/`.
2. Add a wrapper method in `AuthManager` (or the relevant manager) that calls `ApiClient`.
3. Call the wrapper from your screen; catch the typed exception you expect.

### …handle a new socket event
1. Add a `const string EVENT_…` in `TableJoinHandler`.
2. Register it with `SocketManager.Instance.On(EVENT_…, OnYourEventReceived)` next to the others, and `Off` it in the matching cleanup block.
3. Parse JSON with `JsonConvert.DeserializeObject<YourPayload>(json)` inside a `try/catch`.

### …add a sound
1. Add a name to `Core/Audio/SoundId.cs` with an **unused number**, e.g. `ChipStack = 32`.
   Never change or reuse an existing number — the library stores numbers.
2. Put the file in `Assets/Audio/Sfx/` (short, mono `.wav`/`.ogg`).
3. Run **Tools ▸ ClubPoker ▸ Audio ▸ Setup Sound Library** — adds the new row.
4. Drag the file onto that row's **clip** in `SoundLibrary.asset`.
5. Run **Setup Sound Library** again — makes the file addressable. *Skip this and it works in the Editor but is silent in builds.*
6. In code: `GameAudio.Play(SoundId.ChipStack);`

Replacing an existing sound's file needs only steps 4–5.

### …add music to a scene
Put the file in `Assets/Audio/Music/`, drag it onto the music row, run Setup, then
add the **scene file name** (e.g. `ClubScene`) to the library's Scene Music list.

### …add a scene
Create it, mark it Addressable with a `Scene_Xxx` address, and load it through
`GameSceneManager`. Don't add it to Build Settings.

---

## 7. Testing

- **Unit tests:** Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All.
- **Playing a hand alone:** `UnityBotRunner` adds bot players to your table so you can test a full hand without a second device.
- Test on a **real device** for anything touching login, purchases, sound, safe area or backgrounding the app.

---

## 8. Building

1. **Build Addressables first:** Window ▸ Asset Management ▸ Addressables ▸ Groups ▸ Build ▸ New Build ▸ Default Build Script.
   Needed whenever configs, scenes, sounds or other addressable assets changed.
2. Check `configAddress` in `Bootstrap.unity` points at the right server.
3. File ▸ Build Settings → Android (signed with `Poker.keystore`) or iOS (opens in Xcode).

Bundle ids: Android `com.game.clubpoker`, iOS `game.club.poker`.

---

## 9. Git workflow

- `main` — release branch.
- `dev_integration` — where developer branches are combined.
- `dev_<name>` — each developer's own branch.

Commit `.meta` files together with their assets — a missing `.meta` breaks references for everyone else.
Avoid two people editing the same scene or prefab at the same time; Unity scene merges are painful.

---

## 10. Gotchas

- Pressing Play from a scene other than Bootstrap → null managers everywhere.
- Changed a config / sound / scene but the build didn't change → you didn't rebuild Addressables.
- Scene names: `LoadScene` uses the **address** (`Scene_GameTable`); the Scene Music list uses the **file name** (`GameTableScene`).
- Renaming theme ids or renumbering `SoundId` values breaks saved data.
- Backend URLs sometimes change without notice — if every request fails, check the server responds first.
