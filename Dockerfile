# Stage 1: build. The full .NET SDK is big, and it never ships.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy ONLY the project files first, then restore. This layer is rebuilt
# only when a package reference changes, not when a source file changes.
COPY src/TaskFlow.Api/TaskFlow.Api.csproj                       src/TaskFlow.Api/
COPY src/TaskFlow.Application/TaskFlow.Application.csproj       src/TaskFlow.Application/
COPY src/TaskFlow.Domain/TaskFlow.Domain.csproj                 src/TaskFlow.Domain/
COPY src/TaskFlow.Infrastructure/TaskFlow.Infrastructure.csproj src/TaskFlow.Infrastructure/
RUN dotnet restore src/TaskFlow.Api/TaskFlow.Api.csproj

# Now the source, and publish without restoring again.
COPY src/ src/
RUN dotnet publish src/TaskFlow.Api/TaskFlow.Api.csproj -c Release --no-restore -o /app

# Stage 2: runtime. Only the ASP.NET Core runtime, plus what publish produced.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# The folder for the SQLite file, owned by the unprivileged user the base image
# already defines. A volume mounted here keeps the data when the container goes.
RUN mkdir -p /var/lib/taskflow && chown $APP_UID /var/lib/taskflow
USER $APP_UID

# Documentation only: the app listens on 8080 inside the container.
EXPOSE 8080
ENTRYPOINT ["dotnet", "TaskFlow.Api.dll"]
