# Nexa

> **Nexa — Intelligent Engineering Assistant**

Nexa is an **AI-powered Engineering Assistant** designed to help software engineers understand, analyze, develop, and interact with software systems.

Nexa is more than a traditional chatbot. It combines **AI Agents, Knowledge, Skills, Rules, Tools, and MCP** to provide context-aware assistance and enable intelligent interaction with software systems.

---

## Phase 1 — Foundation

Identity, PostgreSQL, Agent CRUD, model profiles, and the host. Secrets stay in configuration (`ModelConnections`), not in the database.

## Phase 2 — Agent Runtime

Microsoft Agent Framework (`ChatClientAgent`) runs conversations: **real streaming**, `AgentSession` persistence on the conversation, built-in tools (`utc_now`, `add_numbers`), and `AgentRun` / tool-execution tracking.

Out of this phase: MCP, RAG, AG-UI, workflows, and approval UI.

```bash
docker compose up -d
dotnet tool restore
dotnet restore
dotnet run --project src/Nexa.Web
```

- App: `https://localhost:7181` / `http://localhost:5136` (see launchSettings)
- Health: `/health`
- OpenAPI: `/openapi/v1.json`
- Stream: `POST /api/conversations/{id}/messages/stream`
- Tools: `GET /api/tools`
- Dev login: `admin@nexa.local` / `ChangeMe!Nexa1`
- Default chat backend: Ollama at `http://localhost:11434/v1` (OpenAI-compatible). Set `ModelConnections:Connections:OpenAI:ApiKey` via user-secrets for OpenAI.

```bash
dotnet test
```

---

## 🎯 Vision

The vision of Nexa is to create an intelligent engineering assistant that can:

* Understand and analyze codebases.
* Understand system architecture and domain concepts.
* Access project documentation and knowledge.
* Interact with external systems through MCP.
* Select and use appropriate tools to complete tasks.
* Follow project-specific rules and engineering standards.
* Assist with development, debugging, analysis, and documentation.
* Provide transparency into how decisions are made.

---

## 🧠 Core Concepts

Nexa is built around several core concepts.

### Agent

The Agent is the core intelligence of Nexa.

It is responsible for:

* Understanding user intent.
* Collecting relevant context.
* Planning tasks.
* Selecting appropriate skills and tools.
* Executing actions.
* Evaluating results.
* Producing the final response.

---

### Knowledge

Knowledge represents the information available to Nexa for understanding the system.

Examples include:

* Architecture documentation
* Domain knowledge
* Data dictionaries
* API documentation
* Architecture Decision Records (ADRs)
* Coding guidelines
* Business rules
* Project documentation
* Codebase information

---

### Skills

Skills represent reusable capabilities that the Agent can execute.

Examples:

* `AnalyzeCode`
* `GenerateCode`
* `AnalyzeDatabase`
* `CreateMigration`
* `ExplainArchitecture`
* `DebugIssue`
* `AnalyzePerformance`
* `GenerateDocumentation`

Skills should be modular, reusable, and independently maintainable.

---

### Rules

Rules define constraints and engineering policies that Nexa must follow.

Examples:

* Coding standards
* Architecture constraints
* Security requirements
* Database conventions
* API conventions
* Project-specific development rules

Rules allow Nexa to behave consistently with the engineering standards of the project.

---

### Tools

Tools allow Nexa to interact with real systems.

Examples include:

* File System
* Git
* Databases
* APIs
* Monitoring systems
* CI/CD systems
* Issue trackers
* External services

When a reliable tool is available, Nexa should prefer using the tool over guessing.

---

### MCP

Nexa uses **Model Context Protocol (MCP)** to provide a standardized way to connect the Agent to external tools and services.

This allows Nexa to integrate with different systems without tightly coupling the Agent to each individual implementation.

---

## 🔄 How Nexa Works

At a high level, Nexa follows this flow:

```text
                         User
                          │
                          ▼
                  ┌───────────────┐
                  │     Nexa      │
                  │     Agent     │
                  └───────┬───────┘
                          │
                 ┌────────┴────────┐
                 │                 │
                 ▼                 ▼
             Knowledge          Skills
                 │                 │
                 └────────┬────────┘
                          │
                          ▼
                        Tools
                          │
              ┌───────────┼───────────┐
              ▼           ▼           ▼
             MCP         APIs      Database
              │
              ▼
       External Systems
```

---

## 🔍 Decision Transparency

One of the key goals of Nexa is to make Agent decisions observable and explainable.

For each task, Nexa should be able to capture information such as:

```text
Task
 ├── User Intent
 ├── Context
 ├── Relevant Knowledge
 ├── Applicable Rules
 ├── Selected Skill
 ├── Selected Tools
 ├── Tool Results
 ├── Decision
 └── Final Response
```

Instead of simply saying:

> "I made this change."

Nexa should be able to provide an explanation such as:

> "This approach was selected based on the applicable architecture rule and the information documented in the relevant ADR."

The goal is not to expose the model's private chain-of-thought, but to provide a structured and useful **decision trace** containing the inputs, rules, tools, evidence, and outcomes that influenced the result.

---

## 🏗️ Architecture

Nexa is designed as a **modular and extensible AI engineering platform**.

```text
Nexa
│
├── Agent
│   ├── Planning
│   ├── Reasoning
│   └── Execution
│
├── Knowledge
│   ├── Documentation
│   ├── Codebase
│   ├── ADR
│   └── Data Dictionary
│
├── Rules
│   ├── Architecture Rules
│   ├── Coding Rules
│   └── Business Rules
│
├── Skills
│   ├── Development
│   ├── Debugging
│   ├── Architecture
│   └── Documentation
│
├── Tools
│   ├── File System
│   ├── Git
│   ├── Database
│   └── External APIs
│
├── MCP
│   └── MCP Servers
│
└── Observability
    ├── Logs
    ├── Traces
    └── Decision History
```

---

## 🚀 Roadmap

### Phase 1 — Agent Foundation

* [x] Create Agent Core
* [x] Add Chat Interface
* [ ] Integrate LLM
* [ ] Implement Tool Calling
* [ ] Add MCP Integration
* [ ] Implement basic Agent execution flow

### Phase 2 — Knowledge & Skills

* [ ] Knowledge Base
* [ ] Codebase Indexing
* [ ] ADR Integration
* [ ] Data Dictionary
* [ ] Skills System
* [ ] Rules System
* [ ] Context Retrieval

### Phase 3 — Observability & Evaluation

* [ ] Decision Trace
* [ ] Agent Observability
* [ ] Tool Execution History
* [ ] Agent Evaluation Framework
* [ ] Prompt/Response Tracking
* [ ] Execution Metrics

### Phase 4 — Advanced Agents

* [ ] Multi-Agent Architecture
* [ ] Advanced Planning
* [ ] Autonomous Tasks
* [ ] Agent Collaboration
* [ ] Continuous Knowledge Updates
* [ ] Human-in-the-Loop Workflows

---

## 🧩 Design Principles

### 1. Knowledge First

The Agent should retrieve relevant context before making important decisions.

### 2. Tool over Guessing

When a reliable tool is available, the Agent should use the tool instead of guessing.

### 3. Explicit Rules

Important system and engineering rules should be explicitly defined and accessible to the Agent.

### 4. Traceable Decisions

Agent decisions should be observable through structured decision traces.

### 5. Modular Skills

Skills should be independently developed, tested, and maintained.

### 6. Human in the Loop

Sensitive or potentially destructive operations should support human approval.

### 7. Fail Safely

When the Agent lacks sufficient context or confidence, it should avoid making unsupported assumptions.

### 8. Separation of Knowledge and Execution

Knowledge should describe the system, while Tools and Skills should perform actions against the system.

### 9. Observable by Design

Agent execution, tool calls, failures, and important decisions should be observable.

### 10. Extensible by Default

New models, tools, MCP servers, knowledge sources, and skills should be addable without redesigning the entire system.

---

## 🛠️ Technology

The initial technology stack is expected to include:

* **C#**
* **.NET**
* **Microsoft Agent Framework**
* **Model Context Protocol (MCP)**
* **LLM Providers**
* **Semantic Search**
* **Vector Stores**
* **OpenTelemetry**
* **Docker**

The technology stack may evolve as the architecture matures.

---

## 🔐 Safety & Governance

Nexa is designed with controlled Agent execution in mind.

Potentially sensitive operations should support:

* Permission checks
* Human approval
* Tool-level authorization
* Read-only modes
* Audit logs
* Execution history
* Structured decision traces

Nexa should distinguish between:

```text
Read Operation
      ↓
Analyze Operation
      ↓
Generate Operation
      ↓
Modify Operation
      ↓
Destructive Operation
```

The level of required authorization should increase with the potential impact of an operation.

---

## 📊 Observability

Nexa should provide visibility into Agent execution.

Important telemetry may include:

* Agent execution duration
* Model latency
* Token usage
* Tool execution duration
* Tool failures
* Knowledge retrieval results
* Skill execution
* Agent iterations
* Decision outcomes
* Human approvals

Example:

```text
Agent Execution
      │
      ├── Model Call
      │
      ├── Knowledge Retrieval
      │
      ├── Skill Selection
      │
      ├── Tool Call
      │
      ├── Tool Result
      │
      ├── Decision
      │
      └── Final Response
```

---

## 🎯 Long-Term Goal

Nexa is not intended to be just another **LLM-powered chatbot**.

The long-term goal is to build an **Engineering Intelligence Layer** that connects:

```text
Developer
    │
    ▼
  Nexa
    │
    ├── Knowledge
    ├── Rules
    ├── Skills
    ├── Codebase
    ├── Tools
    └── MCP
    │
    ▼
Software Systems
```

Nexa should become an intelligent layer between engineers and their software ecosystem.

---

## 💡 Philosophy

> **Nexa connects intelligence to engineering.**

The purpose of Nexa is not to replace engineers.

It is to help engineers:

* Understand systems faster.
* Make better technical decisions.
* Reduce repetitive work.
* Access project knowledge more efficiently.
* Safely automate engineering tasks.
* Understand why an Agent made a particular decision.
* Turn organizational knowledge into actionable engineering intelligence.

---

## 📌 Project Status

> 🚧 **Nexa is currently under active development.**

The architecture, technology choices, and capabilities may evolve during the early stages of the project.

---

## 📄 License

License information will be added as the project matures.
