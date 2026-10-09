<#
.SYNOPSIS
    Installs git commit-msg validation hook into .git/hooks/
.DESCRIPTION
    Ensures every local commit strictly follows Conventional Commits
    to prevent unintended Semantic Version bumps.
#>

$ScriptRoot = Split-Path -Parent $PSScriptRoot
if (-not $ScriptRoot) { $ScriptRoot = Get-Location }

$GitHooksDir = Join-Path $ScriptRoot ".git\hooks"

if (-not (Test-Path $GitHooksDir)) {
    Write-Error ".git directory not found. Please run inside a git repository."
    exit 1
}

$HookFile = Join-Path $GitHooksDir "commit-msg"

$HookContent = @'
#!/bin/sh
# Local Lite Server - Commit Message Linter for SemVer integrity

commit_regex='^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)(\([a-zA-Z0-9_\-\.]+\))?!?: .{1,100}$'
merge_regex='^Merge .*'

first_line=$(head -n 1 "$1")

# Allow merge commits
if echo "$first_line" | grep -qE "$merge_regex"; then
    exit 0
fi

if ! echo "$first_line" | grep -qE "$commit_regex"; then
    echo ""
    echo "======================================================================"
    echo "❌ COMMIT MESSAGE DITOLAK: Format tidak sesuai Conventional Commits!"
    echo "======================================================================"
    echo "Pesan Anda: \"$first_line\""
    echo ""
    echo "Format yang diizinkan (mempengaruhi Semantic Version):"
    echo "  feat(<scope>): <pesan>       -> Bump MINOR (v1.1.0)"
    echo "  fix(<scope>): <pesan>        -> Bump PATCH (v1.0.1)"
    echo "  perf(<scope>): <pesan>       -> Bump PATCH (v1.0.1)"
    echo "  refactor(<scope>): <pesan>   -> Bump PATCH (v1.0.1)"
    echo "  chore(<scope>): <pesan>      -> Bump PATCH (v1.0.1)"
    echo "  docs(<scope>): <pesan>       -> Bump PATCH (v1.0.1)"
    echo "  ci(<scope>): <pesan>         -> Bump PATCH (v1.0.1)"
    echo "  feat!(<scope>): <pesan>      -> Bump MAJOR (v2.0.0)"
    echo "======================================================================"
    echo ""
    exit 1
fi
'@

[System.IO.File]::WriteAllText($HookFile, $HookContent, [System.Text.Encoding]::UTF8)

Write-Host "[OK] Git commit-msg hook installed at: $HookFile" -ForegroundColor Green
