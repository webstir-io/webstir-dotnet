FROM node:current-alpine3.21

RUN npm install -g npm@10.9.2

WORKDIR /app

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
