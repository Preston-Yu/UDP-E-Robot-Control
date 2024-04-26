# 指定要注销的用户名
$username = "Admin"

# 创建Web客户端对象
$webClient = New-Object System.Net.WebClient

# 开始循环检测
while ($true) {
    # 获取外部IP地址
    $externalIp = $webClient.DownloadString("http://ipinfo.io/ip").Trim()

    # 检查IP是否以"163.143"开头
    if ($externalIp.StartsWith("163.143")) {
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
        break # 结束循环
    } else {
        Write-Host "Current external IP ($externalIp) does not start with '163.143'. Checking again in 5 minutes."
        Start-Sleep -Seconds 1 # 等待1s后再次检查
    }
}
