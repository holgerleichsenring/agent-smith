# Agent Smith

**Self-hosted AI coding agent that processes tickets and generates code changes**

Agent Smith is an intelligent coding assistant that takes ticket references as input, analyzes your codebase, generates implementation plans, and autonomously creates code changes via an agentic loop. It integrates with multiple platforms (GitHub, Azure DevOps, GitLab, Jira) and uses Claude/OpenAI to execute complex coding tasks.

## How It Works

Agent Smith follows a configurable pipeline to transform tickets into pull requests:

1. **Fetch Ticket** - Retrieves ticket details from your issue tracking system
2. **Checkout Source** - Clones or accesses the target repository
3. **Load Coding Principles** - Applies your team's coding standards and conventions
4. **Analyze Code** - Scans the codebase to understand structure and dependencies
5. **Generate Plan** - Creates a step-by-step implementation plan using AI
6. **Approval** - Shows the plan and waits for your confirmation
7. **Agentic Execute** - AI autonomously implements changes using tool calling (read/write files, run commands)
8. **Test** - Runs project tests to verify the changes
9. **Commit & PR** - Creates a branch, commits changes, and opens a pull request

## Prerequisites

- **.NET 8** - Required to run Agent Smith
- **API Keys** - For your chosen AI provider (Anthropic Claude or OpenAI)
- **Source Control Access** - GitHub, Azure DevOps, or GitLab credentials
- **Ticket System Access** - GitHub Issues, Azure DevOps Work Items, or Jira tickets

## Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/holgerleichsenring/agent-smith.git
cd agent-smith
```

### 2. Configure Environment Variables

```bash
export ANTHROPIC_API_KEY="sk-ant-api03-..."
export GITHUB_TOKEN="ghp_..."
export AZURE_DEVOPS_TOKEN="your-pat-token"
```

### 3. Configure Your Project

Edit `config/agentsmith.yml`:

```yaml
projects:
  your-project:
    source:
      type: GitHub
      url: https://github.com/your-org/your-repo
      auth: token
    tickets:
      type: GitHub
      url: https://github.com/your-org/your-repo
      auth: token
    agent:
      type: Claude
      model: claude-sonnet-4-20250514
    pipeline: fix-bug
    coding_principles_path: ./config/coding-principles.md
```

### 4. Run Agent Smith

```bash
# Dry run to verify configuration
dotnet run --project src/AgentSmith.Host -- --dry-run "fix #123 in your-project"

# Execute the pipeline
dotnet run --project src/AgentSmith.Host -- "fix #123 in your-project"
```

### 5. Using Docker

```bash
# Build the image
docker build -t agentsmith .

# Run with environment variables
docker run \
  -e ANTHROPIC_API_KEY=sk-ant-... \
  -e GITHUB_TOKEN=ghp_... \
  -v ~/.ssh:/root/.ssh:ro \
  -v $(pwd)/config:/app/config \
  agentsmith "fix #123 in your-project"

# Or use Docker Compose
docker-compose run agentsmith "fix #123 in your-project"
```

## Project Structure

Agent Smith follows Clean Architecture with Domain-Driven Design:

```
AgentSmith.sln
├── src/
│   ├── AgentSmith.Domain/          # Core business entities and value objects
│   │   ├── Entities/               # Ticket, Plan, Repository, CodeAnalysis
│   │   ├── ValueObjects/           # TicketId, BranchName, FilePath
│   │   └── Exceptions/             # Domain-specific exceptions
│   ├── AgentSmith.Contracts/       # Interfaces and contracts
│   │   ├── Commands/               # ICommandContext, ICommandHandler
│   │   ├── Providers/              # ITicketProvider, ISourceProvider, IAgentProvider
│   │   ├── Configuration/          # Config models
│   │   └── Services/               # Service contracts
│   ├── AgentSmith.Application/     # Business logic and use cases
│   │   ├── Commands/               # Pipeline command handlers
│   │   ├── Services/               # Command executor, pipeline executor
│   │   └── UseCases/               # ProcessTicketUseCase
│   ├── AgentSmith.Infrastructure/  # External integrations
│   │   ├── Providers/              # GitHub, Azure DevOps, Claude implementations
│   │   ├── Configuration/          # YAML config loading
│   │   └── Factories/              # Provider factories
│   └── AgentSmith.Host/            # CLI application entry point
├── config/
│   ├── agentsmith.yml              # Main configuration file
│   └── coding-principles.md        # Code quality standards
├── tests/
│   └── AgentSmith.Tests/           # Unit and integration tests
└── docs/                           # Documentation and run logs
```

## Configuration

Agent Smith uses YAML configuration with support for multiple projects and pipelines:

### Projects Configuration

```yaml
projects:
  my-app:
    source:
      type: GitHub                  # GitHub | AzureRepos | GitLab | Local
      url: https://github.com/user/repo
      auth: token                   # token | ssh
    tickets:
      type: GitHub                  # GitHub | AzureDevOps | Jira
      url: https://github.com/user/repo
      auth: token
    agent:
      type: Claude                  # Claude | OpenAI
      model: claude-sonnet-4-20250514
      retry:
        max_retries: 5
        initial_delay_ms: 2000
        backoff_multiplier: 2.0
        max_delay_ms: 60000
      cache:
        enabled: true
        strategy: automatic
      compaction:
        enabled: true
        threshold_iterations: 8
        max_context_tokens: 80000
    pipeline: fix-bug
    coding_principles_path: ./config/coding-principles.md
```

### Pipeline Configuration

```yaml
pipelines:
  fix-bug:
    commands:
      - FetchTicketCommand
      - CheckoutSourceCommand
      - LoadCodingPrinciplesCommand
      - AnalyzeCodeCommand
      - GeneratePlanCommand
      - ApprovalCommand
      - AgenticExecuteCommand
      - TestCommand
      - CommitAndPRCommand
      
  add-feature:
    commands:
      - FetchTicketCommand
      - CheckoutSourceCommand
      - LoadCodingPrinciplesCommand
      - GeneratePlanCommand
      - ApprovalCommand
      - AgenticExecuteCommand
      - GenerateTestsCommand
      - TestCommand
      - GenerateDocsCommand
      - CommitAndPRCommand
```

### Secrets Configuration

```yaml
secrets:
  azure_devops_token: ${AZURE_DEVOPS_TOKEN}
  github_token: ${GITHUB_TOKEN}
  anthropic_api_key: ${ANTHROPIC_API_KEY}
  openai_api_key: ${OPENAI_API_KEY}
  jira_token: ${JIRA_TOKEN}
  jira_email: ${JIRA_EMAIL}
```

## Architecture

### Command Pattern (MediatR-Style)

Agent Smith uses a command-handler pattern for pipeline execution:

- Each pipeline step is a **Command** with its own context record
- Each command has a dedicated **Handler** that implements the logic
- The **CommandExecutor** resolves handlers via dependency injection
- The **PipelineExecutor** orchestrates sequential command execution

### Provider System

Multi-provider architecture supports various platforms:

- **Ticket Providers**: GitHub Issues, Azure DevOps Work Items, Jira
- **Source Providers**: GitHub, Azure Repos, GitLab, Local filesystem
- **Agent Providers**: Anthropic Claude, OpenAI GPT with agentic loops

### Agentic Loop

The core innovation is the agentic execution phase where AI autonomously:
- Reads and analyzes code files
- Makes targeted changes across multiple files
- Runs build and test commands
- Iterates until the implementation is complete

Available tools for the AI agent:
- `read_file` - Read repository files
- `write_file` - Create or modify files  
- `list_files` - Explore directory structure
- `run_command` - Execute shell commands (build, test, etc.)

## CLI Usage

```bash
# Basic usage
agentsmith "fix #123 in project-name"

# Configuration options
agentsmith --config path/to/config.yml "add feature #456 in project-name"

# Dry run (show pipeline without execution)
agentsmith --dry-run "fix #123 in project-name"

# Verbose logging
agentsmith --verbose "fix #123 in project-name"
```

## Docker Usage

### Build and Run

```bash
# Build the image
docker build -t agentsmith .

# Run with required environment variables
docker run \
  -e ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY} \
  -e GITHUB_TOKEN=${GITHUB_TOKEN} \
  -v ~/.ssh:/root/.ssh:ro \
  -v $(pwd)/config:/app/config \
  agentsmith "fix #123 in project-name"
```

### Docker Compose

```bash
# Set environment variables
export GITHUB_TOKEN=$(gh auth token)
export ANTHROPIC_API_KEY="sk-ant-..."

# Run via Docker Compose
docker-compose run agentsmith "fix #123 in project-name"
```

## Testing

Run the test suite to verify functionality:

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test category
dotnet test --filter Category=Integration
```

The test suite includes:
- **Unit Tests**: Command handlers, domain logic, value objects
- **Integration Tests**: End-to-end pipeline execution with mocked providers
- **Configuration Tests**: YAML config parsing and validation

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Follow the coding principles in `config/coding-principles.md`
4. Write tests for new functionality
5. Ensure all tests pass: `dotnet test`
6. Submit a pull request

### Code Quality Standards

- Maximum 20 lines per method
- Maximum 120 lines per class  
- One type per file
- SOLID principles
- English-only codebase (comments, documentation, variable names)

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Support

- **GitHub Issues**: [Report bugs and request features](https://github.com/holgerleichsenring/agent-smith/issues)
- **Documentation**: Comprehensive docs in the `docs/` directory
- **Examples**: See `config/agentsmith.yml` for configuration examples

---

**Made with ❤️ by the Agent Smith community**