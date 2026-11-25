[![GitHub](https://img.shields.io/badge/GitHub-fan--world--me-181717?style=flat-square&logo=github)](https://github.com/fan-world-me) [![Telegram](https://img.shields.io/badge/Telegram-@fan__world__me-2CA5E0?style=flat-square&logo=telegram)](https://t.me/fan_world_me) [![Discord](https://img.shields.io/badge/Discord-fan__world__me-5865F2?style=flat-square&logo=discord)](https://discord.com/users/1040301536036786248) [![Portfolio](https://img.shields.io/badge/Portfolio-fan--world--me.github.io-00e5ff?style=flat-square&logo=githubpages&logoColor=white)](https://fan-world-me.github.io/)

```
 ███████╗██╗      ██████╗ ██╗    ██╗███╗   ███╗ ██████╗
 ██╔════╝██║     ██╔═══██╗██║    ██║████╗ ████║██╔═══██╗
 ███████╗██║     ██║   ██║██║ █╗ ██║██╔████╔██║██║   ██║
 ╚════██║██║     ██║   ██║██║███╗██║██║╚██╔╝██║██║   ██║
 ███████║███████╗╚██████╔╝╚███╔███╔╝██║ ╚═╝ ██║╚██████╔╝
 ╚══════╝╚══════╝ ╚═════╝  ╚══╝╚══╝ ╚═╝     ╚═╝ ╚═════╝
   SlowMo v0.2.0 — BepInEx mod for Hollow Knight: Silksong
```

BepInEx mod that slows down the world of Hollow Knight: Silksong while keeping the hero at normal speed — or slows everything down, including the hero. Features a smooth transition, gravity compensation, movement correction and visible afterimages of the hero during slow motion.

### 💭 More about this mod

- 🐌 **Two slow-motion modes** — Left Shift slows the world while the hero stays at normal speed; Right Shift slows everything, including the hero
- 🔄 **Smooth transition** — speed ramps up/down instead of instant snapping
- 🧍 **Hero stays responsive** — timers, animations and movement are normalized via Harmony patches and a time transpiler
- 🌀 **Afterimages** — visible hero ghosts while slow motion is active
- ⚙️ **Configurable** — slow scale, transition speed, ghost interval/lifetime and toggle keys via BepInEx config
- 📦 **Thunderstore-ready** — builds a Thunderstore package automatically

---

### 🎮 Controls

| Key | Mode |
|---|---|
| Left Shift | Slow motion (world slows, hero normal) |
| Right Shift | Full slow motion (world + hero) |
| Same key again | Disable slow motion |

Keys are rebindable in `BepInEx/config/yarik.slowmo.cfg`.

---

### 🧰 Tech Stack

**Core** [![C#](https://img.shields.io/badge/C%23-netstandard2.1-239120?style=flat-square&logo=csharp&logoColor=white)](#) [![Unity](https://img.shields.io/badge/Unity-6000.0.50-000000?style=flat-square&logo=unity&logoColor=white)](#) [![BepInEx](https://img.shields.io/badge/BepInEx-5.4.21-8A2BE2?style=flat-square)](#) [![HarmonyX](https://img.shields.io/badge/HarmonyX-2.9.0-FF6B35?style=flat-square)](#)

**Game** [![Silksong](https://img.shields.io/badge/Hollow_Knight-Silksong-4A3F6B?style=flat-square)](#) [![Silksong.GameLibs](https://img.shields.io/badge/Silksong.GameLibs-*--*-00e5ff?style=flat-square)](#)

---

### 🗂️ Project Structure

```
SlowMoProj/
├── SlowMo.cs                — plugin source (time transpiler, patches, ghosts)
├── SlowMo.csproj            — project file (BepInEx + HarmonyX + Silksong.GameLibs)
├── Directory.Build.props    — assembly title & version
├── nuget.config             — nuget sources
├── packages.lock.json       — locked dependencies
├── SilksongPath.props       — local Silksong install path (git-ignored)
├── thunderstore/
│   ├── thunderstore.toml    — Thunderstore package config
│   └── icon.png             — package icon
├── .github/workflows/       — CI build & publish workflow
└── README.md
```

---

### 🚀 How it works

```
[Left/Right Shift pressed] ──► slowOn flag toggled
        │
        ▼
[LateUpdate] ──► Time.timeScale = cur (0.05..1.0)
        │            HeroTime.K = cur (hero-normal) or 1 (full slow)
        ▼
[Transpiler] ──► Time.deltaTime → HeroTime.Dt()  (hero sees normal time)
        │
        ▼
[Movement] ──► gravity & velocity compensated for K
        │
        ▼
[Ghosts] ──► afterimages spawned while slowed
```

---

### 📦 Build

Requires .NET SDK and the Silksong game libraries (provided by `Silksong.GameLibs`).

```bash
dotnet build -c Release
```

Output:
- `bin/Release/netstandard2.1/SlowMo.dll` — mod binary
- `thunderstore/dist/*.zip` — Thunderstore package

---

### 🛠️ Install Locally

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx) for Silksong.
2. Copy `SlowMo.dll` into `BepInEx/plugins/`.
3. Launch the game — config is generated at `BepInEx/config/yarik.slowmo.cfg`.

Or install via a mod manager (Thunderstore) using the packaged `.zip`.

---

### 🔒 Notes

> `SilksongPath.props` is git-ignored and contains your local game path.  
> The CI workflow (`allow-release`) is disabled by default — enable it when you are ready to publish releases.

---

### 📄 License

MIT © [fan-world-me](https://github.com/fan-world-me)

---

Made with 🩵 in Ukraine 🇺🇦

[![Telegram](https://img.shields.io/badge/Telegram-@fan__world__me-2CA5E0?style=flat-square&logo=telegram)](https://t.me/fan_world_me) [![Discord](https://img.shields.io/badge/Discord-fan__world__me-5865F2?style=flat-square&logo=discord)](https://discord.com/users/1040301536036786248) [![Portfolio](https://img.shields.io/badge/Portfolio-fan--world--me.github.io-00e5ff?style=flat-square&logo=githubpages&logoColor=white)](https://fan-world-me.github.io/)