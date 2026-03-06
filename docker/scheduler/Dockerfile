FROM alpine:3.21

RUN apk add --no-cache bash curl tzdata

WORKDIR /scheduler

COPY docker/scheduler/run-motor.sh /scheduler/run-motor.sh
COPY docker/scheduler/crontab /etc/crontabs/root

RUN chmod +x /scheduler/run-motor.sh

ENV TZ=America/Sao_Paulo

CMD ["crond", "-f", "-l", "8"]
