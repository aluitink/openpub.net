# Contributing to ActivityPub.NET

Thank you for your interest in contributing to ActivityPub.NET! All contributions are welcome, from code fixes to documentation improvements.

## Code of Conduct

This project follows the .NET Foundation Code of Conduct. Please be respectful and constructive in all interactions.

## How to Contribute

### Reporting Issues

1. Check existing issues to avoid duplicates
2. Use appropriate issue templates
3. Provide detailed reproduction steps
4. Include environment information (OS, .NET version, etc.)

### Setting Up Development Environment

1. Clone the repository:
   ```bash
   git clone https://github.com/yourorg/activitypub-dotnet.git
   cd activitypub-dotnet
   ```

2. Build the solution:
   ```bash
   dotnet build
   ```

3. Run tests to verify setup:
   ```bash
   dotnet test
   ```

### Making Changes

1. Create a feature branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. Make your changes following the code style guidelines

3. Run tests:
   ```bash
   dotnet test
   ```

4. Commit your changes:
   ```bash
   git commit -m "Description of changes"
   ```

5. Push and create a pull request

## Code Style Guidelines

### C# Coding Standards

- Use PascalCase for public members
- Use camelCase for private fields
- Use async/await for all async operations
- Prefer expression-bodied members
- Use `var` when type is obvious
- Use `readonly struct` where appropriate

### Project Structure

```
src/
├── ActivityPub.Core/
│   ├── Core/           # Domain models
│   ├── Services/       # Business logic
│   ├── Repositories/   # Data access
│   └── Infrastructure/ # External concerns
tests/
├── UnitTests/          # Unit tests
├── IntegrationTests/   # Integration tests
└── ScaleTests/         # Performance tests
```

### Testing Requirements

- All new code must have unit tests
- Integration tests for external dependencies
- Aim for 100% coverage (pragmatic approach)
- Tests must run in under 30 seconds

## Pull Request Process

1. Update documentation if needed
2. Add tests for new functionality
3. Ensure all tests pass
4. Request review from maintainers
5. Address review comments
6. Merge when approved

## Questions?

Join our Discord community or open an issue with your question.
