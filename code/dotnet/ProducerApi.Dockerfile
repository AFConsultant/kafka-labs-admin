# Stage 1: Build the application
# We use the .NET SDK image which contains all the tools to build the app.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the solution file and all project files.
# This is done to leverage Docker layer caching. The layers will only be rebuilt
# if these files change.
COPY ["ProducerApi/ProducerApi.csproj", "ProducerApi/"]
COPY ["Shared.Contracts/Shared.Contracts.csproj", "Shared.Contracts/"]
COPY ["ConsumerApi/ConsumerApi.csproj", "ConsumerApi/"]
COPY ["SimpleConsumerApi/SimpleConsumerApi.csproj", "SimpleConsumerApi/"]
COPY ["SimpleProducerApi/SimpleProducerApi.csproj", "SimpleProducerApi/"]
COPY ["KafkaApp.sln", "."]
COPY [".config", "./.config"]

# Restore dependencies for all projects in the solution
RUN dotnet restore "KafkaApp.sln"
RUN dotnet tool restore

# Copy the rest of the source code
COPY . .

# Build and publish the ProducerApi project
# We specify the output directory for the published application.
WORKDIR "/src/ProducerApi"
RUN dotnet build "ProducerApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ProducerApi.csproj" -c Release -o /app/publish

# Stage 2: Create the final runtime image
# We use the smaller ASP.NET runtime image which is optimized for running the app.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set the entrypoint for the container. This is the command that will be executed
# when the container starts.
ENTRYPOINT ["dotnet", "ProducerApi.dll"]

