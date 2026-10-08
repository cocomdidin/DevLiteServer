worker_processes  1;

events {
    worker_connections  1024;
}

http {
    include       mime.types;
    default_type  application/octet-stream;
    sendfile        on;
    keepalive_timeout  65;

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
        location /adminer {
            alias "{{ROOT_DIR_FORWARD}}/tools/adminer";
            index adminer.php;

            location ~ \.php$ {
                fastcgi_pass   127.0.0.1:{{PHP_PORT}};
                fastcgi_index  adminer.php;
                fastcgi_param  SCRIPT_FILENAME $request_filename;
                include        fastcgi_params;
            }
        }

        # Pass PHP scripts to FastCGI server
        location ~ \.php$ {
            try_files $uri =404;
            fastcgi_pass   127.0.0.1:{{PHP_PORT}};
            fastcgi_index  index.php;
            fastcgi_param  SCRIPT_FILENAME $document_root$fastcgi_script_name;
            include        fastcgi_params;
        }

        error_page   500 502 503 504  /50x.html;
        location = /50x.html {
            root   html;
        }
    }
}
