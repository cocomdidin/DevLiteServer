# Dev Lite Server - Portable PostgreSQL Configuration
listen_addresses = '127.0.0.1'
port = {{POSTGRESQL_PORT}}
max_connections = 100
shared_buffers = 128MB
dynamic_shared_memory_type = windows

# Logging
log_destination = 'stderr'
logging_collector = on
log_directory = 'log'
log_filename = 'postgresql-%Y-%m-%d_%H%M%S.log'
log_min_messages = warning

# Locale and Formatting
timezone = 'UTC'
log_timezone = 'UTC'
datestyle = 'iso, ymd'
default_text_search_config = 'pg_catalog.english'
