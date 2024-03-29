FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine as build

ARG AppName=RecordsCalculator
# Tool to ease csproj copy for dotnet restore
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install -g dotnet-references
# Separate restore stage so docker can cache it
COPY ./src/${AppName}.sln /src/${AppName}.sln
COPY ./src/*/*.csproj /src/
COPY ./*.props /
RUN dotnet references fix -ep /src/${AppName}.sln -wd /src -rupf
RUN dotnet restore -r linux-musl-x64 /src/${AppName}
# Actual build
COPY ./src /src
RUN dotnet publish --no-restore -c Release -r linux-musl-x64 -o publish /src/${AppName}

FROM alpine

COPY --from=build /publish/${AppName} /root/${AppName}
RUN apk add --no-cache krb5-libs libstdc++ libgcc

ENTRYPOINT ["/root/RecordsCalculator"]