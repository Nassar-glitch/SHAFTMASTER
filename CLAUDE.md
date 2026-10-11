# Project guidance for Claude Code

## How to work with the user

- The user usually prefers Egyptian Arabic for conversation and explanations.
- The user wants two distinct chats in this project: a **Planner** chat for discussion, review, and writing implementation prompts, and an **Executor** chat for carrying out those prompts.
- The long-term calculation goal is to implement the remaining applicable calculations in the supplied calculation note, in stages. FF1/FF2 is an intermediate milestone, not the endpoint. When the user says “continue,” consult `CALCULATION_ROADMAP.md` and inspect the source note/current implementation to identify the next unfinished calculation.
- In the Planner role, do not edit implementation files unless the user explicitly changes the role. Read `SESSION_HANDOFF.md` first, verify its status against the workspace, then prepare a prompt that carries forward the current state and constraints.
- In the Executor role, read `SESSION_HANDOFF.md` before acting. After completing or pausing a task, update it with the actual status, decisions, changed files, checks run/not run, blockers, and next step. Do not claim the Planner chat can see Executor chat history; share progress through this file.
- Both chats must use this same project folder for the shared handoff file to work.
- Keep project facts grounded in the current files. Older chat summaries and generated outputs can be stale.
- **Product principle:**
  - `DESIGN-FLOW.md` is the agreed design flow and is binding for all work.
  - Every input except the site survey starts from an editable preset. Edited values are locked, and unlocked values follow upstream changes.
  - The UI, drawings and calculation sheet contain no disclaimers or remarks such as "preset", "assumed", "not from catalog", "review required" or "preliminary".
  - Code remarks and NOT OK items go only to the separate Design Notes file.

## Cloud and local split (agreed 2026-10-12)

- **Source of truth:** GitHub `main`. Local `main` is pushed before any cloud task starts, and a cloud task always starts from the latest `origin/main`.
- **Cloud sessions** (no AutoCAD, SOLIDWORKS, Word or WPF) take the portable work:
  - Elevator.Core and Elevator.Reporting: calculations, presets and rules, calculation sheet and Design Notes text, figure SVGs, layout and title-block data models, file-name builder;
  - xUnit tests and docs.
  
  They install the .NET 8 SDK if it is missing, and run `ElevatorAutomation/tools/test.ps1 -Tier 1` (net8.0 suites) before pushing a branch `cloud/<topic>`. They never push to `main`, and never re-approve golden fixtures without listing every changed fixture and why.
- **Local sessions** take only what needs the Windows tools:
  - AutoCAD drawings and layouts, PDF plotting, STEP, the WPF UI, Word rendering;
  - merging cloud branches, the net48 / WPF golden checks, releases.
  
  Local prompts are small and exact. They run tier 1 plus the suites the change touches. The full tiers 2–3 run only before a merge into `main` that touches those parts, and before a release.
- **Never in parallel on the same files:** one branch per topic, and a cloud branch is merged locally (`git fetch` + `git merge`, no rebase) before local work on the same files continues.

## Start here

- Read `DESIGN-FLOW.md` for the agreed design flow, presets and build stages.
- Read `ARCHITECTURE-PLAN.md` for the target code structure, refactor phases, test tiers and refactor rules. It is binding for structural work.
- Read `SESSION_HANDOFF.md` for the latest Executor progress and next step.
- Read `CALCULATION_ROADMAP.md` for the user's broader calculation goal and the note-section inventory.
- For the main application, read `ElevatorAutomation/README.md` and the relevant current documents under `ElevatorAutomation/docs/` before changing code.
- `ElevatorAutomation/docs/calculations/CAR-GUIDE-FORCES.md` documents the FF1/FF2 candidate, adopted equations, validation, and known interpretation limits.
- `ElevatorAutomation/docs/releases/SHAFTMASTER-UI-RESULTS.md` and `ElevatorAutomation/docs/releases/STEP-EXPORT-RESULTS.md` describe packaged application and export evidence.
- `legacy/ElevatorPlanGenerator/` is an earlier AutoCAD plugin and regression/reference tree. Do not assume it is the active ShaftMaster desktop application.

## Project constraints

- Preserve the original DWG files and backups. Generate into separate output paths and do not overwrite source drawings.
- Preserve the existing native AutoCAD drawing and STEP workflows when editing the desktop solution.
- Treat engineering equations as adopted source-note rules unless independently established. Do not imply standards compliance, manufacturer approval, or certification without evidence.
- Do not guess engineering interpretation: equations, load paths, axis meanings, force directions or physical mappings. Request the missing engineering decision when it affects results. User-approved presets and ladders in `DESIGN-FLOW.md` are design defaults, not guesses.
- Existing outputs and candidate binaries are evidence or build artifacts, not automatically the release target. Inspect the current source, docs, and requested delivery target before promoting or replacing anything.
- This directory may not be a Git repository. Check the actual repository/worktree state before relying on Git commands or assumptions.
