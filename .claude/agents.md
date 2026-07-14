# Appium Setup Manager — Agent Registry

Four custom agents are configured for this project. Always run them in this order for each phase.

## Agents

### 1. product-architect
**File:** `agents/product-architect.md`
**Run:** At the start of every phase or feature.
Analyzes the PRD and PROJECT_DOCUMENTS.md, reviews current codebase state, and produces a structured implementation plan with ordered task breakdown per agent. Never writes production code.

### 2. avalonia-ui-engineer
**File:** `agents/avalonia-ui-engineer.md`
**Run:** After product-architect produces a plan.
Implements AXAML views, compiled bindings, styles, animations, value converters, and ViewModel observable properties/commands (stubs only). Never touches Core business logic.

### 3. dotnet-core-engineer
**File:** `agents/dotnet-core-engineer.md`
**Run:** After avalonia-ui-engineer creates the ViewModel stubs.
Implements all business logic in AppiumSetupManager.Core — services, CommandRunner, platform adapters, CLI detection probes, install sequences, health checks, storage scanning, and ViewModel command wiring. Writes unit tests.

### 4. qa-review-engineer
**File:** `agents/qa-review-engineer.md`
**Run:** After dotnet-core-engineer completes a phase.
Validates implementation against PROJECT_DOCUMENTS.md specs, reviews C# and AXAML code quality, fills missing tests, and issues phase APPROVED or NEEDS FIXES.

## Phase Flow

```
product-architect  →  avalonia-ui-engineer  →  dotnet-core-engineer  →  qa-review-engineer
     (plan)               (UI stubs)               (logic + wiring)          (validate)
```
