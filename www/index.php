<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Local Lite Server - Local Environment</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
            background: #0f172a;
            color: #f8fafc;
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            margin: 0;
        }
        .card {
            background: #1e293b;
            padding: 2.5rem;
            border-radius: 12px;
            box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.3);
            max-width: 540px;
            width: 100%;
            border: 1px solid #334155;
        }
        h1 {
            margin-top: 0;
            color: #38bdf8;
            font-size: 1.75rem;
        }
        p {
            color: #94a3b8;
            line-height: 1.6;
        }
        .badge {
            display: inline-block;
            background: #0369a1;
            color: #e0f2fe;
            padding: 0.25rem 0.6rem;
            border-radius: 9999px;
            font-size: 0.85rem;
            font-weight: 600;
        }
        .grid {
            margin-top: 1.5rem;
            border-top: 1px solid #334155;
            padding-top: 1.25rem;
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 1rem;
        }
        .stat {
            background: #0f172a;
            padding: 0.75rem 1rem;
            border-radius: 8px;
            border: 1px solid #1e293b;
        }
        .stat-label {
            font-size: 0.75rem;
            text-transform: uppercase;
            color: #64748b;
            font-weight: 700;
        }
        .stat-val {
            margin-top: 0.25rem;
            font-size: 1.1rem;
            color: #f1f5f9;
            font-weight: 600;
        }
        .links {
            margin-top: 1.5rem;
            display: flex;
            gap: 0.75rem;
        }
        .btn {
            display: inline-block;
            background: #2563eb;
            color: white;
            text-decoration: none;
            padding: 0.5rem 1rem;
            border-radius: 6px;
            font-weight: 500;
            font-size: 0.9rem;
            transition: background 0.2s;
        }
        .btn:hover {
            background: #1d4ed8;
        }
        .btn-secondary {
            background: #334155;
        }
        .btn-secondary:hover {
            background: #475569;
        }
    </style>
</head>
<body>
    <div class="card">
        <span class="badge">Local Lite Server Running</span>
        <h1>Welcome to Local Lite Server</h1>
        <p>Your portable local web development environment is running smoothly.</p>

        <div class="grid">
            <div class="stat">
                <div class="stat-label">PHP Version</div>
                <div class="stat-val"><?php echo PHP_VERSION; ?></div>
            </div>
            <div class="stat">
                <div class="stat-label">Web Server</div>
                <div class="stat-val"><?php echo $_SERVER['SERVER_SOFTWARE'] ?? 'Nginx'; ?></div>
            </div>
            <div class="stat">
                <div class="stat-label">Server Port</div>
                <div class="stat-val"><?php echo $_SERVER['SERVER_PORT'] ?? '80'; ?></div>
            </div>
            <div class="stat">
                <div class="stat-label">Document Root</div>
                <div class="stat-val" style="font-size: 0.85rem; word-break: break-all;"><?php echo __DIR__; ?></div>
            </div>
        </div>

        <div class="links">
            <a href="/adminer" class="btn">Database Manager (Adminer)</a>
            <a href="http://localhost:8025" target="_blank" class="btn btn-secondary">Mailpit Web (:8025)</a>
        </div>
    </div>
</body>
</html>
