# nuget-docs

A .NET global tool to inspect public API documentation from any NuGet package. Lists types, decompiles with XML docs, and searches members — optimized for AI agents and CLI workflows.

## Installation

```bash
dotnet tool install -g nuget-docs
```

## Usage

```bash
# List all public types in a package
nuget-docs list Microsoft.Extensions.AI.Abstractions

# Show full decompiled source for a specific type (short names work)
nuget-docs show Microsoft.Extensions.AI.Abstractions IChatClient

# Search for types/members matching a pattern
nuget-docs search Microsoft.Extensions.AI.Abstractions "Chat*"

# Show package metadata
nuget-docs info Newtonsoft.Json

# Specify version and framework
nuget-docs list Microsoft.Extensions.AI.Abstractions --version 10.4.0 --framework net9.0
```

## Features

- Inspects any public NuGet package — auto-downloads if not cached
- Full C# decompilation with `///` XML documentation comments
- Short type name resolution (e.g., `IChatClient` resolves automatically)
- Framework-aware: picks best matching TFM
- AI-optimized plain text output
- Zero configuration — works out of the box

## License

MIT
