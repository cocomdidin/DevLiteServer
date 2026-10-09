# Virtual Host Configuration for {{DOMAIN}}
server {
    listen       {{HTTP_PORT}};
    server_name  {{DOMAIN}};
    root         "{{PROJECT_ROOT}}";
    index        index.php index.html index.htm;

    location / {
        try_files $uri $uri/ /index.php?$query_string;
    }

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
