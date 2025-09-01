# SimpleMQ - Database-Supported Queue Implementation

SimpleMQ is a simple, database-supported queue implementation for .NET applications using SQL Server as the backing store.

**ALWAYS** reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

## Working Effectively

### Prerequisites and Setup
- Install .NET 8.0 SDK - currently validated version: 8.0.119
- Verify installation: `dotnet --version`
- SQL Server instance (for integration testing and development)

### Bootstrap, Build, and Test the Repository
**Current State**: Repository is minimal with only README, LICENSE, and .gitignore. Once Issue #1 is implemented, the full project structure will be available.

**When project structure exists**:
```bash
# Restore dependencies - takes ~2 seconds. NEVER CANCEL.
dotnet restore

# Build solution - takes ~4 seconds. NEVER CANCEL. Set timeout to 30+ seconds.
dotnet build

# Run all tests - takes ~3 seconds. NEVER CANCEL. Set timeout to 30+ seconds.
dotnet test
```

**Full project setup workflow**:
```bash
# Clone and initial setup
git clone <repository-url>
cd SimpleMQ

# Restore NuGet packages for all projects
dotnet restore  # ~2 seconds

# Build entire solution  
dotnet build    # ~4 seconds, NEVER CANCEL

# Run all unit tests
dotnet test     # ~3 seconds, NEVER CANCEL
```

### Expected Project Structure (when implemented)
```
SimpleMQ/
├── SimpleMq.sln                    # Main solution file
├── src/
│   ├── SimpleMq.Common/            # Core interfaces and abstractions
│   │   ├── SimpleMq.Common.csproj
│   │   └── *.cs files
│   └── SimpleMq.SqlServer/         # SQL Server implementation
│       ├── SimpleMq.SqlServer.csproj
│       └── *.cs files
├── test/
│   ├── SimpleMq.Common.Test/       # Tests for common library (MSTest)
│   └── SimpleMq.SqlServer.Test/    # Tests for SQL Server implementation (MSTest)
├── docs/
│   ├── tech/                       # Technical documentation
│   └── user/                       # User documentation
└── copilot/
    ├── .copilot-instructions.md    # Development guidelines
    ├── logs/                       # Session logs
    └── plans/                      # Implementation plans
```

## Validation

### ALWAYS run complete validation after making changes
**CRITICAL**: For a queue implementation, always test actual queue operations, not just compilation.

**Required validation steps**:
1. **Build validation**: `dotnet build` - must complete without errors
2. **Unit test validation**: `dotnet test` - all tests must pass
3. **Queue functionality validation** (when implementation exists):
   - Test message enqueue operations
   - Test message dequeue operations  
   - Test queue persistence across application restarts
   - Test concurrent queue access scenarios
   - Test error handling for database connection issues

**Database validation scenarios** (when SQL Server integration exists):
- Test with local SQL Server instance
- Verify database schema creation/migration
- Test transaction handling for queue operations
- Test cleanup of processed messages
- Test dead letter queue functionality

### Never Cancel Operations
- **NEVER CANCEL** any `dotnet restore` operations - can take up to 2 seconds
- **NEVER CANCEL** any `dotnet build` operations - can take up to 4 seconds  
- **NEVER CANCEL** any `dotnet test` operations - can take up to 3 seconds
- **Set timeouts to 30+ seconds** for all build and test commands

## Common Tasks

### Building Specific Projects
```bash
# Build only the common library
dotnet build src/SimpleMq.Common/SimpleMq.Common.csproj

# Build only the SQL Server implementation  
dotnet build src/SimpleMq.SqlServer/SimpleMq.SqlServer.csproj

# Build only tests
dotnet build test/SimpleMq.Common.Test/SimpleMq.Common.Test.csproj
```

### Running Specific Tests
```bash
# Run tests for specific project
dotnet test test/SimpleMq.Common.Test/SimpleMq.Common.Test.csproj

# Run tests with detailed output
dotnet test --verbosity normal

# Run tests with coverage (if configured)
dotnet test --collect:"XPlat Code Coverage"
```

### Development Workflow
1. **Always start with**: `dotnet restore`
2. **Before making changes**: `dotnet build && dotnet test` to ensure clean baseline
3. **After making changes**: `dotnet build && dotnet test` to validate changes
4. **Before committing**: Ensure all validation steps pass

### Project Dependencies
- `SimpleMq.SqlServer` references `SimpleMq.Common` for shared interfaces
- Test projects reference their corresponding implementation projects
- All projects target .NET 8.0

## Key Components (when implemented)

### Core Interfaces (`SimpleMq.Common`)
- Queue management interfaces
- Message serialization/deserialization contracts
- Error handling abstractions
- Transaction handling interfaces

### SQL Server Implementation (`SimpleMq.SqlServer`)  
- Concrete queue implementation using SQL Server
- Database connection management
- SQL-based message persistence
- Transaction coordination with SQL Server

### Testing Strategy
- **Unit tests**: Core functionality without database dependencies
- **Integration tests**: Full database-backed queue operations
- **Performance tests**: Queue throughput and latency testing
- **Error scenarios**: Database connection failures, transaction rollbacks

## Database Configuration

### SQL Server Setup (when implementation exists)
- Connection string configuration via appsettings.json or environment variables
- Database schema will be created automatically or via migrations
- Requires SQL Server 2019+ or SQL Server Express for development

### Connection String Format
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=SimpleMQ;Trusted_Connection=true;TrustServerCertificate=true;"
  }
}
```

## Troubleshooting

### Common Build Issues
- **Restore failures**: Check internet connectivity, clear NuGet cache with `dotnet nuget locals all --clear`
- **Build failures**: Ensure .NET 8.0 SDK is installed, check for missing project references
- **Test failures**: Verify database connectivity for integration tests

### Database Issues (when applicable)
- **Connection failures**: Verify SQL Server is running and connection string is correct
- **Schema issues**: Check database migrations and permissions
- **Transaction issues**: Verify SQL Server supports required isolation levels

### Command Timing Expectations
Based on validation testing:
- `dotnet restore`: ~2 seconds for clean restore
- `dotnet build`: ~4 seconds for clean build  
- `dotnet test`: ~3 seconds for full test suite
- **ALWAYS set timeouts to 30+ seconds minimum** to prevent premature cancellation

## Current Limitations
- **Repository is currently minimal** - full .NET project structure not yet implemented
- **No database setup** - SQL Server integration pending implementation  
- **No CI/CD** - GitHub Actions workflows not yet configured

## Additional Context
- Project follows .NET conventions and SOLID principles
- Uses dependency injection patterns for testability
- Implements proper error handling and logging
- MSTest framework for all unit and integration testing
- Database operations use transactions for consistency