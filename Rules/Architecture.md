# Architecture

- Respect module boundaries: implementation projects must depend only on other modules' `*.Contracts`, never on another module's implementation.
- Follow SOLID; prefer composition over inheritance; avoid static classes except for pure helpers.
- Each cross-module dependency must flow through an interface registered in DI.
- Watch for layer/boundary violations, leaking implementation types across modules, and bidirectional coupling.
- Flag high coupling and low cohesion: a type doing unrelated jobs, or a change that forces edits across many modules.
- New external integrations (repo/task/LLM providers) must be added behind their factory, not by editing existing call sites.
