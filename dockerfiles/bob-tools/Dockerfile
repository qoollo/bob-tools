FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine as build

# Build
COPY ./src /src
COPY ./buildscripts /buildscripts
COPY ./*.props /
COPY ./publish /publish
RUN /buildscripts/publish.sh linux-musl-x64

FROM alpine

COPY --from=build /publish/* /
RUN apk add --no-cache krb5-libs libstdc++ libgcc openssh bash

ENTRYPOINT ["/BobTools.sh"]