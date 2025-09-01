# Technical Documentation

## Architecture Overview
SimpleMQ follows a layered architecture with clear separation of concerns:

### Core Components
- **SimpleMq.Common**: Contains interfaces and common abstractions
- **SimpleMq.SqlServer**: Concrete implementation using SQL Server as the backing store

### Design Patterns
- Repository pattern for data access
- Factory pattern for queue creation
- Strategy pattern for different message processing strategies

## Database Schema
TBD - Will contain tables for:
- Queue definitions
- Messages
- Processing status
- Dead letter queues

## Configuration
TBD - Configuration options for:
- Connection strings
- Queue settings
- Retry policies
- Timeouts