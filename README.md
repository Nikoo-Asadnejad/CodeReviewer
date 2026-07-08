# Reviewer Agent

## Overview

Reviewer Agent is an AI-powered Pull Request (PR) review system designed to automate code reviews across different repositories, issue trackers, and Large Language Models (LLMs).

Instead of being tied to a specific AI provider or source control platform, the agent uses a modular architecture that allows different connectors to work together. It gathers context from the repository, linked work items, and the Pull Request itself before generating intelligent review comments.

The goal is to provide reviewers with meaningful feedback that considers not only the code changes, but also the business context behind them.

---

# Features

- 🤖 Multi-LLM Support
  - Claude
  - Llama
  - Easily extensible to additional models

- 📦 Repository Connectors
  - GitHub
  - Azure DevOps
  - Extensible connector architecture

- 📋 Task Management Connectors
  - Azure DevOps Work Items
  - ClickUp
  - Extensible for Jira and others

- 🔍 Pull Request Analysis
  - PR metadata
  - Changed files
  - Git diff
  - Commit history

- 🧠 Context Builder
  - Collects relevant source code
  - Reads linked tasks
  - Understands affected modules
  - Provides dependency context

- 💬 AI Code Review
  - Code quality
  - Bug detection
  - Security concerns
  - Performance improvements
  - Best practices
  - Architecture validation
  - Business logic validation

- 📝 Automated PR Comments
  - Inline comments
  - General review summary
  - Suggested improvements
  - Risk assessment

---

# High-Level Architecture

```
                    +----------------+
                    | Pull Request   |
                    +-------+--------+
                            |
                            |
                 Repository Connector
                            |
          +-----------------+------------------+
          |                                    |
      GitHub                          Azure DevOps
          |                                    |
          +-----------------+------------------+
                            |
                            |
                     Context Builder
                            |
        +-------------------+----------------------+
        |                   |                      |
     PR Diff            Repository           Linked Task
        |                 Files                Details
        |                   |                      |
        +-------------------+----------------------+
                            |
                     Context Aggregator
                            |
                   Prompt Construction
                            |
                    LLM Abstraction Layer
                            |
     +------------+-----------+-------+
     |            |           |            
  Claude       Llama        Others
                            |
                     Review Response
                            |
                   Comment Publisher
                            |
        +-------------------+------------------+
        |                                      |
    GitHub Review                     Azure DevOps Review
```

---

# Workflow

1. Pull Request event is received.
2. Repository connector loads:
   - PR metadata
   - changed files
   - git diff
   - commits
3. Task connector loads linked work items.
4. Context Builder gathers:
   - affected files
   - related code
   - business requirements
   - repository context
5. Prompt Builder creates an optimized prompt.
6. Selected LLM reviews the changes.
7. Review output is parsed.
8. Comments are published back to the Pull Request.

---

# Components

## Repository Connectors

Responsible for communicating with source control providers.

### Supported

- GitHub
- Azure DevOps

Responsibilities:

- Read Pull Requests
- Read file changes
- Read commits
- Publish review comments
- Read repository files

---

## Task Connectors

Retrieve business context associated with a Pull Request.

### Supported

- Azure DevOps Work Items
- ClickUp

Responsibilities:

- Retrieve linked tasks
- Read acceptance criteria
- Read descriptions
- Read implementation notes
- Provide business requirements

---

## Context Builder

The Context Builder is responsible for assembling all information required by the LLM.

It combines:

- PR description
- Git diff
- Changed files
- Related source code
- Linked tasks
- Business requirements
- Repository structure
- Existing comments (optional)

The result is a structured context object that is independent of the LLM.

---

## Prompt Builder

Transforms the collected context into an optimized prompt for the selected model.

The prompt includes:

- System instructions
- Review guidelines
- Coding standards
- Code diff
- Related code snippets

---

## LLM Provider

Provides a unified interface for different AI models.

Example providers:

- Claude
- Llama
- GPT (future)
- Gemini (future)

Each provider implements the same interface.

```text
Review(Context) -> ReviewResult
```

---

## Review Engine

Responsible for:

- invoking the selected LLM
- parsing responses
- validating output
- generating structured review comments

Example review categories:

- Bug
- Warning
- Suggestion
- Performance
- Security
- Maintainability
- Business Logic
- Documentation

---

## Comment Publisher

Publishes review results back to the repository platform.

Supports:

- Inline comments
- Review summaries
- Approval suggestions
- Change requests

---

# Project Structure

```text
ReviewerAgent
│
├── Core
│   ├── Context
│   ├── Prompt
│   ├── Review
│   ├── Models
│   
│
├── Connectors
│   ├── GitHub
│   ├── AzureDevOps
│   ├── ClickUp
│   └── Common
│
├── Providers
│   ├── Claude
│   ├── Llama
│   ├── OpenAI
│   └── Common
│
├── ReviewEngine
│
├── CommentPublisher
│
├── Configuration
│
└── API
```

---

# Supported Providers

| Category | Providers |
|-----------|-----------|
| Repository | GitHub, Azure DevOps |
| Tasks | ClickUp, Azure DevOps |
| AI Models | Claude, Llama, OpenAI GPT |

---

# Configuration Example

```json
{
  "repositoryProvider": "GitHub",
  "taskProvider": "ClickUp",
  "llmProvider": "Claude",
  "maxContextTokens": 100000,
  "enableBusinessContext": true,
  "publishInlineComments": true
}
```

---

# Example Flow

```
Developer opens Pull Request
            │
            ▼
Repository Connector
            │
            ▼
Load PR Diff
            │
            ▼
Find Linked Task
            │
            ▼
Task Connector
            │
            ▼
Read Business Requirements
            │
            ▼
Build Context
            │
            ▼
Generate Prompt
            │
            ▼
Claude / Llama / GPT
            │
            ▼
Generate Review
            │
            ▼
Publish Comments
```

---

# Design Principles

- Provider-agnostic architecture
- Connector-based integrations
- Modular and extensible components
- Separation of concerns
- LLM-independent prompt generation
- Scalable context-building pipeline
- Easy addition of new repository, task, or AI providers

---

# Future Enhancements

- Jira connector
- GitLab connector
- Bitbucket connector
- Gemini integration
- Deep semantic repository search
- RAG-based repository knowledge
- Organization-specific coding standards
- Custom review policies
- Learning from developer feedback
- Multi-agent review pipeline
- Security-focused review agent
- Performance optimization agent
- Architecture validation agent