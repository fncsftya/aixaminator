# syntax=docker/dockerfile:1
# check=skip=FromPlatformFlagConstDisallowed
#
# Builds the C# solution and runs the test suite on linux/amd64.
#
#   docker build --platform linux/amd64 -t aixaminator .
#   docker run --rm aixaminator                     # run all tests
#   docker run --rm aixaminator dotnet build ...    # or any other command
#
# The MAUI UI app only targets net10.0 here: the iOS / Mac Catalyst target
# frameworks need Apple tooling and cannot be built on Linux. The net10.0 target
# is the one the test project references.
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:10.0@sha256:28e7a5db4f5d40cc805acd939a065668ba2e17d697a09153054dce98db240d0e

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# UseMaui needs the MAUI SDK workload manifest even for the plain net10.0 target.
# maui-android is the only MAUI workload installable on Linux.
RUN dotnet workload install maui-android

WORKDIR /src

# The app's multi-target list is rewritten to net10.0 only (see the note at the
# top). This is done with sed rather than -p:TargetFrameworks=..., because a
# global TargetFrameworks makes NuGet treat the test project as cross-targeting
# and the xunit adapter then never gets copied to the output folder.
ARG PLAIN_TFM_ONLY="s#<TargetFrameworks>[^<]*</TargetFrameworks>#<TargetFramework>net10.0</TargetFramework>#"

# Restore first, using only project files, so the package layer is cached
# until a dependency changes.
COPY Aixaminator.sln ./
COPY Aixaminator/Aixaminator.csproj Aixaminator/
RUN sed -i "$PLAIN_TFM_ONLY" Aixaminator/Aixaminator.csproj
COPY Aixaminator.Tests/Aixaminator.Tests.csproj Aixaminator.Tests/
COPY Importers/Importers.csproj Importers/
COPY SemanticSlicer/SemanticSlicer.csproj SemanticSlicer/
COPY Shared.AI/Shared.AI.csproj Shared.AI/
RUN dotnet restore Aixaminator.Tests/Aixaminator.Tests.csproj

COPY . .
RUN sed -i "$PLAIN_TFM_ONLY" Aixaminator/Aixaminator.csproj \
    && dotnet build Aixaminator.Tests/Aixaminator.Tests.csproj --no-restore -c Release

# Runtime must work with no network access: everything (SDK, workload, NuGet
# packages, compiled output) is fetched or built above. The command below uses
# --no-build, which also skips restore, so nothing is downloaded when it runs.
CMD ["sh", "-c", "dotnet test Aixaminator.Tests/Aixaminator.Tests.csproj --no-build -c Release"]
