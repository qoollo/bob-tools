FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine as build

ARG Version="1.0.0.0"
ARG InformationalVersion="1.0.0.0"
# Build
COPY ./src /src
COPY ./buildscripts /buildscripts
RUN /buildscripts/publish.sh ${Version} ${InformationalVersion} linux-musl-x64

FROM alpine

COPY --from=build /publish/* /root/
RUN apk add krb5-libs libstdc++ libgcc openssh
RUN echo -e\
    "#!/bin/sh\n\
    /root/\$1 \${@:2}" >> /root/start.sh\
    && chmod +x /root/start.sh

ENTRYPOINT ["/root/start.sh"]