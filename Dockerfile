# syntax=docker/dockerfile:1
# check=skip=FromPlatformFlagConstDisallowed
#
# Builds the C# solution and runs the test suite on linux/amd64.
#
#   docker build --platform linux/amd64 -t aixaminator .
#   docker run --rm aixaminator                     # run all tests
#   docker run --rm aixaminator dotnet build ...    # or any other command
#
# It is also a self-contained Avalonia toolchain for the MAUI -> Avalonia port:
# the Avalonia packages (+ templates, native Skia libs and an X virtual
# framebuffer) are fetched at build time, so Avalonia projects can be created,
# restored, built, tested (headless) and run inside the container with
# --network none.
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

# Native libraries Avalonia/SkiaSharp load at runtime on Linux, plus xvfb so a
# real (non-headless) Avalonia window can be run and screenshotted.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        libfontconfig1 libfreetype6 libx11-6 libxext6 libxrender1 libxcursor1 \
        libxi6 libxrandr2 libice6 libsm6 libgl1 libegl1 libglu1-mesa \
        fonts-dejavu-core xvfb xauth \
    && rm -rf /var/lib/apt/lists/*

# Avalonia project/item templates (`dotnet new avalonia.app`, etc.).
ARG AVALONIA_VERSION=12.1.3
RUN dotnet new install Avalonia.Templates::${AVALONIA_VERSION}

# Pre-populate the NuGet cache with everything an Avalonia port needs. A
# throwaway project references the packages; restoring it downloads them and
# their transitive dependencies (Skia/HarfBuzz native assets for linux, win and
# osx included) into ~/.nuget/packages, which later offline restores resolve
# from. Add packages here when the port starts using new ones. Avalonia's
# headless test package (Avalonia.Headless.XUnit) needs xunit v3, hence xunit.v3
# below; the existing xunit v2 test project restores its own packages further down.
RUN mkdir /tmp/avalonia-warmup && cd /tmp/avalonia-warmup \
    && printf '%s\n' \
        '<Project Sdk="Microsoft.NET.Sdk">' \
        '  <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup>' \
        '  <ItemGroup>' \
        "    <PackageReference Include=\"Avalonia\" Version=\"${AVALONIA_VERSION}\" />" \
        "    <PackageReference Include=\"Avalonia.Desktop\" Version=\"${AVALONIA_VERSION}\" />" \
        "    <PackageReference Include=\"Avalonia.Themes.Fluent\" Version=\"${AVALONIA_VERSION}\" />" \
        "    <PackageReference Include=\"Avalonia.Fonts.Inter\" Version=\"${AVALONIA_VERSION}\" />" \
        "    <PackageReference Include=\"Avalonia.Headless\" Version=\"${AVALONIA_VERSION}\" />" \
        "    <PackageReference Include=\"Avalonia.Headless.XUnit\" Version=\"${AVALONIA_VERSION}\" />" \
        '    <PackageReference Include="Avalonia.Controls.DataGrid" Version="12.1.2" />' \
        '    <PackageReference Include="Avalonia.Controls.WebView" Version="12.1.0" />' \
        '    <PackageReference Include="AvaloniaUI.DiagnosticsSupport" Version="2.2.3" />' \
        '    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />' \
        '    <PackageReference Include="xunit.v3" Version="3.2.2" />' \
        '    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />' \
        '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />' \
        '  </ItemGroup>' \
        '</Project>' > warmup.csproj \
    && dotnet restore -r linux-x64 \
    && cd / && rm -rf /tmp/avalonia-warmup

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
RUN dotnet restore Aixaminator.Tests/Aixaminator.Tests.csproj

COPY . .
RUN sed -i "$PLAIN_TFM_ONLY" Aixaminator/Aixaminator.csproj \
    && dotnet build Aixaminator.Tests/Aixaminator.Tests.csproj --no-restore -c Release

# Runtime must work with no network access: everything (SDK, workload, NuGet
# packages incl. Avalonia, templates, native libs, compiled output) is fetched or built above. The command below uses
# --no-build, which also skips restore, so nothing is downloaded when it runs.
CMD ["sh", "-c", "dotnet test Aixaminator.Tests/Aixaminator.Tests.csproj --no-build -c Release"]
