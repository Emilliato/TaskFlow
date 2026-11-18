#!/bin/bash
# Where does each glossary term live in THIS repository? For every term, the first real match
# (file:line) of a search pattern, or an honest "not in this repository".
#   usage: bash tools/capstone/glossary-tour.sh <group>      groups: lang web arch quality devops
# Run it from the repository root. It only reads files; nothing is built or started.
group=${1:?usage: glossary-tour.sh lang|web|arch|quality|devops}

# hit "Term" 'extended regex' [path ...]   (default path: src)
hit() {
  local term=$1 re=$2; shift 2
  local paths=("${@:-src}")
  local m
  m=$(grep -rnHE --exclude-dir={bin,obj,node_modules} -m1 -- "$re" "${paths[@]}" 2>/dev/null | head -1)
  if [ -n "$m" ]; then
    printf '%-24s %s\n' "$term" "$(echo "$m" | cut -d: -f1,2)"
  else
    printf '%-24s %s\n' "$term" "not in this repository"
  fi
}

case $group in
  lang)
    hit "SDK (build image)"   'dotnet/sdk'                    Dockerfile
    hit "Runtime (run image)" 'dotnet/aspnet'                 Dockerfile
    hit "NuGet"               'PackageReference'              src/TaskFlow.Api
    hit "Solution"            '<Project Path'                 TaskFlow.slnx
    hit "Nullable ref types"  '<Nullable>enable'              src/TaskFlow.Api
    hit "Record"              'public record '                src/TaskFlow.Application
    hit "Pattern matching"    ' is \{ \} '                    src/TaskFlow.Api
    hit "Extension method"    'static .*\(this '              src/TaskFlow.Api
    hit "LINQ"                '\.Select\('                    src/TaskFlow.Application
    hit "async in use cases" 'await '                        src/TaskFlow.Application src/TaskFlow.Infrastructure
    ;;
  web)
    hit "Minimal API"         'app\.MapGet'                   src/TaskFlow.Api
    hit "Endpoint, HTTP verb" 'app\.MapPost\("/tasks"'        src/TaskFlow.Api
    hit "Status code"         'Results\.NotFound'             src/TaskFlow.Api
    hit "DTO"                 'class CreateTaskRequest'       src/TaskFlow.Api
    hit "Model binding"       '\[FromBody\]'                  src/TaskFlow.Api
    hit "Middleware"          'class ApiKeyGateMiddleware'    src/TaskFlow.Api
    hit "CORS"                'AddCors'                       src/TaskFlow.Api
    hit "Swagger / OpenAPI"   'AddOpenApi'                    src/TaskFlow.Api
    hit "Controller"          ': ControllerBase'              src
    hit "JWT"                 'JwtBearer'                     src
    ;;
  arch)
    hit "DI registration"     'AddScoped<TaskService>'        src/TaskFlow.Api
    hit "Service lifetime"    'AddSingleton<IOwnerDirectory'  src/TaskFlow.Api
    hit "Repository pattern"  'interface ITaskRepository'     src/TaskFlow.Application
    hit "Domain layer"        'record TaskItem'               src/TaskFlow.Domain
    hit "Application layer"   'class TaskService'             src/TaskFlow.Application
    hit "Infrastructure layer" 'AddInfrastructure'            src/TaskFlow.Infrastructure
    hit "DbContext"           'class TaskFlowDbContext'       src/TaskFlow.Infrastructure/TaskFlowDbContext.cs
    hit "DbSet"               'DbSet<'                        src/TaskFlow.Infrastructure/TaskFlowDbContext.cs
    hit "Migration"           'class InitialCreate'           src/TaskFlow.Infrastructure
    hit "Connection string"   'ConnectionString'              src/TaskFlow.Api/appsettings.Production.json
    hit "N+1 fix"             'CountByTask'                   src/TaskFlow.Application
    ;;
  quality)
    hit "xUnit"               '\[Fact\]'                      tests
    hit "Fake"                'class FakeTaskRepository'      tests/TaskFlow.Application.Tests
    hit "Mock (Moq)"          'new Mock<'                     tests
    hit "Tech debt"           'Technical debt|technical debt' docs
    hit "ILogger"             'ILogger<TaskService>'          src
    hit "Structured logging"  'LogInformation\(.*\{TaskId\}'  src
    hit "Observability"       'AddOpenTelemetry'              src
    hit "appsettings.json"    '"TaskFlow"'                    src/TaskFlow.Api/appsettings.json
    hit "User secrets"        'UserSecretsId'                 src/TaskFlow.Api
    hit "Environment"         'IsDevelopment'                 src/TaskFlow.Api
    ;;
  devops)
    hit "Dockerfile"          'FROM '                         Dockerfile
    hit "Image (two stages)"  'AS runtime'                    Dockerfile
    hit "Container user"      'USER '                         Dockerfile
    hit "CI/CD"               'name: CI'                      .github/workflows
    hit "Pipeline jobs"       'build-test:'                   .github/workflows
    hit "Load balancing"      'upstream taskflow'             tools/microservices-demo/lb/nginx.conf
    hit "Message queue"       'QueueDeclareAsync'             tools/microservices-demo/queue
    hit "gRPC"                '^service '                     tools/microservices-demo/grpc
    hit "Serverless"          'Azure Functions|AzureFunctions' src .github
    hit "Cloud deploy"        'azure/webapps-deploy|az webapp' .github
    ;;
  *) echo "unknown group: $group"; exit 2 ;;
esac
