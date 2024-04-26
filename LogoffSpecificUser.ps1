# 指定要注销的用户名
$username = "Admin"

# 使用 cmd.exe 执行 'query session' 命令，并通过 PowerShell 捕获输出
$sessionInfo = cmd /c query session 2>&1 | Where-Object { $_ -match "\s+$username\s+" }

if ($sessionInfo) {
    # 从输出中提取会话ID
    $sessionId = $sessionInfo -split '\s+' | Where-Object { $_ -match '^\d+$' }

    if ($sessionId) {
        # 如果找到会话ID，使用 'logoff' 命令注销该会话
        cmd /c logoff $sessionId
        Write-Host "User $username logged off."
    }
} else {
    Write-Host "User $username is not logged in or session could not be found."
}
