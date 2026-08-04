# Plan-call live eval

- ticket source: `synthetic-two-repo-migration`
- system prompt: 2423 chars, user prompt: 16327 chars
- generated: 2026-08-04T07:25:42.3626230+00:00

| model | max out tokens | finish reason | out tokens | response chars | parsed steps | salvaged steps | both repos | error |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| gpt-4.1 | 8192 | stop | 2218 | 10278 | 25 | - | yes | - |
| gpt-4.1 | 16384 | stop | 2137 | 9894 | 27 | - | yes | - |
| claude-sonnet-5 | 8192 | length | 8192 | 15370 | - | 0 | no | - |
| claude-sonnet-5 | 16384 | stop | 8696 | 19531 | 45 | - | yes | - |

## gpt-4.1 @ 8192 — sample step targets
- SampleServer/src/Sample.Server.*/*.csproj
- SampleServer/src/Sample.Server.Api/**/*.cs
- SampleServer/src/Sample.Server.Application/**/*Step.cs
- SampleServer/src/Sample.Server.Api/Program.cs
- SampleServer/tests/Sample.Server.Tests/**/*Dispatcher*.cs
- SampleServer/src/Sample.Server.Api/**/*Export*.cs
- SampleServer/src/Sample.Server.Application/AuditLogHandler.cs
- SampleServer/src/Sample.Server.*/*.csproj

## gpt-4.1 @ 16384 — sample step targets
- SampleServer/src/Sample.Server.Api/Sample.Server.Api.csproj
- SampleServer/src/Sample.Server.Application/Sample.Server.Application.csproj
- SampleServer/src/Sample.Server.Api/Program.cs
- SampleServer/src/Sample.Server.Api
- SampleServer/src/Sample.Server.Application
- SampleServer/src/Sample.Server.Application/Behaviors
- SampleServer/src/Sample.Server.Application/Behaviors/AuditLogBehavior.cs
- SampleServer/src/Sample.Server.Api/Controllers

## claude-sonnet-5 @ 16384 — sample step targets
- SampleServer/src/Sample.Server.Application/Sample.Server.Application.csproj
- SampleServer/src/Sample.Server.Api/Sample.Server.Api.csproj
- SampleServer/tests/Sample.Server.Tests/Sample.Server.Tests.csproj
- SampleServer/src/Sample.Server.Api/Program.cs
- SampleServer/src/Sample.Server.Application/Abstractions/ITransactionalRequest.cs
- SampleServer/src/Sample.Server.Application/Handlers/
- SampleServer/src/Sample.Server.Application/Handlers/ExportOrdersQueryHandler.cs
- SampleServer/src/Sample.Server.Api/Controllers/ExportsController.cs
