[client]
port = {{MYSQL_PORT}}

[mysql]
default-character-set = utf8mb4

[mysqld]
port = {{MYSQL_PORT}}
basedir = "{{MYSQL_BASEDIR_FORWARD}}"
datadir = "{{MYSQL_DATADIR_FORWARD}}"
character-set-server = utf8mb4
collation-server = utf8mb4_unicode_ci
default-storage-engine = INNODB
max_connections = 100
sql_mode = NO_ENGINE_SUBSTITUTION,STRICT_TRANS_TABLES
