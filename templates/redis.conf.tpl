# Dev Lite Server - Portable Redis Configuration
bind 127.0.0.1
protected-mode yes
port {{REDIS_PORT}}
tcp-backlog 511
timeout 0
tcp-keepalive 300
loglevel notice
databases 16
always-show-logo yes

save 900 1
save 300 10
save 60 10000

stop-writes-on-bgsave-error yes
rdbcompression yes
rdbchecksum yes
dbfilename dump.rdb
dir ./

maxmemory 256mb
maxmemory-policy allkeys-lru
