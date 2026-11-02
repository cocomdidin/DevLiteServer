worker_processes  1;

events {
    worker_connections  1024;
}

http {
    include       mime.types;
    default_type  application/octet-stream;
    sendfile        on;
    keepalive_timeout  65;

    # Upstream PHP FastCGI pools
    include upstreams.conf;

    # FastCGI performance and buffer tuning
    fastcgi_connect_timeout 5s;
    fastcgi_send_timeout 60s;
    fastcgi_read_timeout 60s;
    fastcgi_buffer_size 128k;
    fastcgi_buffers 4 256k;
    fastcgi_busy_buffers_size 256k;
    fastcgi_temp_file_write_size 256k;
    fastcgi_keep_conn on;

    server {
        listen       {{HTTP_PORT}};
        server_name  localhost;
        root         "{{WWW_DIR_FORWARD}}";
        index        index.php index.html index.htm;

        # Main www project routing
        location / {
            try_files $uri $uri/ /index.php?$query_string;
        }

        # Dedicated Adminer route for database management
        location = /adminer {
            return 301 /adminer/;
        }
        location ^~ /adminer/ {
            alias "{{ROOT_DIR_FORWARD}}/tools/adminer/";
            index adminer.php;

            location ~ \.php$ {
                fastcgi_pass   php_default;
                fastcgi_index  adminer.php;
                fastcgi_param  SCRIPT_FILENAME $request_filename;
                include        fastcgi_params;
            }
        }

        # Dedicated phpMyAdmin route for MySQL management
        location = /phpmyadmin {
            return 301 /phpmyadmin/;
        }
        location ^~ /phpmyadmin/ {
            alias "{{ROOT_DIR_FORWARD}}/tools/phpmyadmin/";
            index index.php;

            location ~ \.php$ {
                fastcgi_pass   php_default;
                fastcgi_index  index.php;
                fastcgi_param  SCRIPT_FILENAME $request_filename;
                fastcgi_param  PHP_VALUE "error_reporting=22527\ndisplay_errors=0";
                include        fastcgi_params;
            }
        }

        # Dedicated phpRedisAdmin route for Redis key-value management
        location = /redisadmin {
            return 301 /redisadmin/;
        }
        location ^~ /redisadmin/ {
            alias "{{ROOT_DIR_FORWARD}}/tools/phpredisadmin/";
            index index.php;

            location ~ \.php$ {
                fastcgi_pass   php_default;
                fastcgi_index  index.php;
                fastcgi_param  SCRIPT_FILENAME $request_filename;
                include        fastcgi_params;
            }
        }

        # Pass PHP scripts to FastCGI server
        location ~ \.php$ {
            try_files $uri =404;
            fastcgi_pass   php_default;
            fastcgi_index  index.php;
            fastcgi_param  SCRIPT_FILENAME $document_root$fastcgi_script_name;
            include        fastcgi_params;
        }

        error_page   500 502 503 504  /50x.html;
        location = /50x.html {
            root   html;
        }
    }

    # Dynamic virtual hosts (*.test)
    include vhosts.conf;
}
