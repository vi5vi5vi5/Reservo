# Reservo

Resource booking system for a university: meeting rooms, classrooms, labs and equipment — with guaranteed protection against double bookings.

> 🚧 Work in progress. Study project, built with ASP.NET Core 10, EF Core and PostgreSQL.

## Tech stack

- .NET 10 / C# 14, ASP.NET Core Web API
- Entity Framework Core + PostgreSQL
- JWT authentication, FluentValidation, Serilog
- SignalR for real-time updates
- xUnit + Testcontainers
- Docker Compose

## Project structure

```
src/
  Reservo.Domain/          entities, business rules
  Reservo.Application/     use cases, DTOs, validators
  Reservo.Infrastructure/  database, auth, background jobs
  Reservo.Api/             HTTP API
tests/
  Reservo.UnitTests/
  Reservo.IntegrationTests/
```

## Getting started

_Coming soon._

## License

[MIT](LICENSE)
