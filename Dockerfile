FROM alpine

RUN apk add krb5-libs libstdc++ libgcc

COPY ./publish/RecordsCalculator /root/RecordsCalculator

ENTRYPOINT ["/root/RecordsCalculator"]