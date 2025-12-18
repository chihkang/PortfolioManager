# Contributing to PortfolioManager

Thank you for your interest in contributing to PortfolioManager! This document provides guidelines and instructions for contributing.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Coding Standards](#coding-standards)
- [Testing Guidelines](#testing-guidelines)
- [Documentation](#documentation)
- [Pull Request Process](#pull-request-process)
- [Issue Reporting](#issue-reporting)

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](https://www.contributor-covenant.org/). By participating, you are expected to uphold this code.

## Getting Started

### Prerequisites

- .NET SDK 10 (see `global.json`)
- MongoDB (local or Atlas)
- Git
- Your favorite IDE (Visual Studio, Rider, VS Code)

### Setting Up Development Environment

1. **Fork and Clone**

```bash
git clone https://github.com/YOUR_USERNAME/PortfolioManager.git
cd PortfolioManager
```

2. **Configure MongoDB**

```bash
# Using user secrets (recommended)
dotnet user-secrets set "MongoDbSettings:ConnectionString" "mongodb://localhost:27017"
dotnet user-secrets set "MongoDbSettings:DatabaseName" "portfolio_db_dev"
```

3. **Restore Dependencies**

```bash
dotnet restore
```

4. **Run Tests**

```bash
dotnet test
```

5. **Run Application**

```bash
dotnet run --project PortfolioManager.csproj
```

Access Swagger UI at `http://localhost:3000/swagger`.

## Development Workflow

### Branch Strategy

- `main` - Production-ready code
- `develop` - Integration branch for features (if used)
- `feature/*` - New features
- `bugfix/*` - Bug fixes
- `docs/*` - Documentation updates

### Creating a Feature Branch

```bash
git checkout -b feature/your-feature-name main
```

### Commit Messages

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting)
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

**Examples:**

```
feat(portfolio): add support for cryptocurrency stocks

- Add CryptoStock entity
- Update portfolio valuation logic
- Add integration tests

Closes #123
```

```
fix(job): handle exchange rate service timeout

Add retry logic with exponential backoff when fetching exchange rates.

Fixes #456
```

## Coding Standards

### General Principles

1. **KISS (Keep It Simple, Stupid)**: Prefer simplicity over cleverness
2. **DRY (Don't Repeat Yourself)**: Avoid code duplication
3. **YAGNI (You Aren't Gonna Need It)**: Don't add functionality until necessary
4. **SOLID Principles**: Follow object-oriented design principles

### C# Coding Style

#### Use Primary Constructors

```csharp
// ✅ Good
public class UserController(
    MongoDbService mongoDbService,
    ILogger<UserController> logger) : ControllerBase
{
    // ...
}

// ❌ Avoid (unless primary constructor is not suitable)
public class UserController : ControllerBase
{
    private readonly MongoDbService _mongoDbService;
    
    public UserController(MongoDbService mongoDbService)
    {
        _mongoDbService = mongoDbService;
    }
}
```

#### Use Switch Expressions

```csharp
// ✅ Good
return result switch
{
    null => NotFound(),
    _ => Ok(result)
};

// ❌ Avoid
if (result == null)
    return NotFound();
else
    return Ok(result);
```

#### Use ReadOnlySpan<char> for Hot Paths

```csharp
// ✅ Good (in hot paths)
ReadOnlySpan<char> idSpan = id.AsSpan();
if (idSpan.IsEmpty) return BadRequest();

// ✅ Also acceptable (in non-critical paths)
if (string.IsNullOrEmpty(id)) return BadRequest();
```

#### Structured Logging

```csharp
// ✅ Good
logger.LogInformation("""
    Portfolio created:
    Portfolio ID: {PortfolioId}
    User ID: {UserId}
    """,
    portfolio.Id,
    portfolio.UserId);

// ❌ Avoid
logger.LogInformation($"Portfolio {portfolio.Id} created for user {portfolio.UserId}");
```

### No Repository Pattern

Access MongoDB collections directly via `MongoDbService`:

```csharp
// ✅ Good
var user = await mongoDbService.Users
    .Find(u => u.Username == username)
    .FirstOrDefaultAsync();

// ❌ Avoid adding unnecessary repository abstractions
```

### Naming Conventions

- **Classes/Interfaces**: PascalCase (`UserController`, `IExchangeRateService`)
- **Methods**: PascalCase (`GetUserById`, `CreatePortfolio`)
- **Variables/Parameters**: camelCase (`userId`, `portfolioId`)
- **Private Fields**: `_camelCase` (`_logger`, `_mongoDbService`)
- **Constants**: PascalCase (`MaxRetries`, `BaseDelayMs`)

## Testing Guidelines

### Test Structure

Tests are located in `PortfolioManager.Tests` and use xUnit.

### Writing Tests

```csharp
public class UserControllerTests
{
    [Fact]
    public async Task GetUser_WithValidId_ReturnsUser()
    {
        // Arrange
        var mockService = new Mock<MongoDbService>();
        var controller = new UserController(mockService.Object, logger);
        
        // Act
        var result = await controller.GetUser("valid-id");
        
        // Assert
        Assert.IsType<OkObjectResult>(result);
    }
}
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test
dotnet test --filter "FullyQualifiedName~UserControllerTests"
```

### Code Coverage Requirements

- Aim for **75%+ code coverage**
- All new features should include tests
- Bug fixes should include regression tests

## Documentation

### When to Update Documentation

- **Always**: When adding/changing public APIs
- **Usually**: When adding new features
- **Sometimes**: When fixing bugs (if behavior changes)

### Documentation Locations

- **API Documentation**: `docs/api/README.md`
- **Guides**: `docs/guides/`
- **Tutorials**: `docs/tutorials/`
- **Code Comments**: For complex logic only

### Documentation Style

- Use clear, concise language
- Provide code examples
- Include both high-level concepts and detailed instructions
- Follow Diátaxis framework (tutorials, how-to guides, reference, explanation)

## Pull Request Process

### Before Submitting

1. ✅ Tests pass: `dotnet test`
2. ✅ Code builds: `dotnet build`
3. ✅ Linting passes (if configured)
4. ✅ Documentation updated (if applicable)
5. ✅ Commit messages follow Conventional Commits
6. ✅ Branch is up-to-date with `main`

### Submitting a Pull Request

1. **Push your branch**

```bash
git push origin feature/your-feature-name
```

2. **Create Pull Request** on GitHub with:
   - Clear title describing the change
   - Description explaining **what** and **why**
   - References to related issues (`Fixes #123`, `Closes #456`)
   - Screenshots/videos if UI changes

3. **Template**

```markdown
## Description
Brief description of changes

## Motivation and Context
Why is this change needed? What problem does it solve?

## Related Issues
Fixes #123

## Changes Made
- Change 1
- Change 2
- Change 3

## Testing
How has this been tested?

## Screenshots (if applicable)

## Checklist
- [ ] Tests pass
- [ ] Documentation updated
- [ ] Conventional commits used
- [ ] Ready for review
```

### Review Process

- At least one maintainer approval required
- Address review feedback promptly
- Keep discussions respectful and constructive
- Squash commits before merging (if requested)

## Issue Reporting

### Bug Reports

Use the bug report template and include:

- **Description**: Clear description of the bug
- **Steps to Reproduce**: Minimal steps to reproduce
- **Expected Behavior**: What should happen
- **Actual Behavior**: What actually happens
- **Environment**: OS, .NET version, MongoDB version
- **Logs**: Relevant error messages/stack traces

### Feature Requests

Use the feature request template and include:

- **Problem**: What problem does this solve?
- **Proposed Solution**: How should it work?
- **Alternatives**: Other solutions considered
- **Additional Context**: Screenshots, mockups, etc.

## Questions?

- **Discussions**: Use GitHub Discussions for questions
- **Issues**: For bug reports and feature requests
- **Email**: Contact maintainers directly for sensitive matters

## License

By contributing, you agree that your contributions will be licensed under the same license as the project.

---

Thank you for contributing to PortfolioManager! 🎉
