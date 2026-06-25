# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**HistoricScience** is a Unity 6 (6000.1.17f1) game project using the Universal Render Pipeline (URP 17.1.0). The project is in early development — only `Assets/Scenes/SampleScene.unity` exists; no C# scripts have been written yet.

## Unity Workflow

Unity projects are edited primarily through the Unity Editor GUI, not the command line. There is no build/lint/test CLI to run from this repo directly. To work with this project:

- **Open the project**: Launch Unity Hub and open the `C:\GitHub\HistoricScience` folder with Unity 6000.1.17f1.
- **Run tests**: Use the Unity Test Runner (Window → General → Test Runner) with the `com.unity.test-framework` 1.5.1 package.
- **Build**: File → Build Settings in the Unity Editor.

IDEs configured: JetBrains Rider and Visual Studio (both have Unity integration packages installed).

## Key Packages

| Package | Version | Purpose |
|---|---|---|
| `com.unity.render-pipelines.universal` | 17.1.0 | URP — all shaders/materials must be URP-compatible |
| `com.unity.inputsystem` | 1.14.0 | New Input System (not the legacy Input class) |
| `com.unity.ai.navigation` | 2.0.8 | NavMesh for AI pathfinding |
| `com.unity.timeline` | 1.8.7 | Cutscene/animation sequencing |
| `com.unity.visualscripting` | 1.9.7 | Visual scripting graphs |
| `com.unity.ugui` | 2.0.0 | UI Toolkit / uGUI |

## Asset Organization

- `Assets/Scenes/` — Unity scenes
- `Assets/ExternalAssets/` — third-party assets (gitignored; must be imported locally). Currently contains `Cartoon_Texture_Pack` with URP materials for grass, rocks, dirt, sand, wood, walls, and roofs.
- `Assets/ExternalAssets/Test` — All test elements (including test code, test scenes, and test prefabs) must be created here to keep them out of the main project structure and source control.

**Important**: `Assets/ExternalAssets/` is in `.gitignore`. Do not commit assets placed there — they are imported from outside the repo.

## Code Conventions

When writing C# scripts for Unity:
- Use the **New Input System** (`UnityEngine.InputSystem`) — the legacy `Input` class is not the intended pattern for this project.
- All materials and shaders must be **URP-compatible** (use URP Lit/Unlit shaders, not built-in Standard shader).
- Assembly definitions (`.asmdef`) should be created for any new script folder to manage compilation.
- Always explicitly declare `private` for all members of classes and structs (do not omit it).
- If you find a better design or architecture than what was requested, do not implement it immediately; propose the design change first.
