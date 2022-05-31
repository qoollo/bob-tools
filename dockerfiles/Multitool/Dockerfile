FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine as build

# Build
COPY ./src /src
COPY ./buildscripts /buildscripts
RUN /buildscripts/publish.sh linux-musl-x64

FROM alpine

COPY --from=build /publish/* /root/
RUN apk add --no-cache krb5-libs libstdc++ libgcc openssh
RUN echo -e\
    "#!/bin/sh\n\
    /root/\$1 \${@:2}" >> /root/start.sh\
    && chmod +x /root/start.sh

ENTRYPOINT ["/root/start.sh"]