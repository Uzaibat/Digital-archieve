# ─────── Build stage ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /repo

COPY IDADRS.sln .
COPY src/IDADRS.Core/IDADRS.Core.csproj                          src/IDADRS.Core/
COPY src/IDADRS.Application/IDADRS.Application.csproj             src/IDADRS.Application/
COPY src/IDADRS.Infrastructure/IDADRS.Infrastructure.csproj       src/IDADRS.Infrastructure/
COPY src/IDADRS.NativeSearch/IDADRS.NativeSearch.csproj           src/IDADRS.NativeSearch/
COPY src/IDADRS.API/IDADRS.API.csproj                             src/IDADRS.API/
COPY tests/IDADRS.Tests/IDADRS.Tests.csproj                       tests/IDADRS.Tests/
RUN dotnet restore

COPY . .

# Build native C library
RUN apt-get update && apt-get install -y cmake build-essential
WORKDIR /repo/src/IDADRS.NativeSearch
RUN cmake -S . -B build -DCMAKE_BUILD_TYPE=Release && cmake --build build
RUN cp build/libsearch_engine.so .

WORKDIR /repo
RUN dotnet publish src/IDADRS.API/IDADRS.API.csproj -c Release -o /app/publish 

# ─────── Runtime stage ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN addgroup --system idadrs && adduser --system --ingroup idadrs idadrs

COPY --from=build /app/publish .
COPY --from=build /repo/src/IDADRS.NativeSearch/libsearch_engine.so .

RUN mkdir -p /uploads && chown idadrs:idadrs /uploads

USER idadrs
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "IDADRS.API.dll"]
