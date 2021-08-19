$ngrok= Start-Job -ScriptBlock { & ngrok http 88 }

try
{
    Write-Host "Started ngrok http 88" -ForegroundColor DarkGreen

    Start-Sleep -Seconds 1

    $url = Invoke-RestMethod http://127.0.0.1:4040/api/tunnels | Select-Object -Expand tunnels -First 1 | Select-Object -Expand public_url | Where-Object { $_.StartsWith('https') }

    . dotnet user-secrets set "Deployment:PublicUrl" $url --project .\src\KindlerBot\KindlerBot.csproj
    Write-Host "Configured public URL to $url" -ForegroundColor DarkGreen

    while($true)
    {
        Start-Sleep -Seconds 1
    }
}
finally
{
    Write-Host "Stopping..." -ForegroundColor DarkYellow -NoNewline
    Stop-Job -Id $ngrok.Id
    # $ngrok.StopJob();
    Write-Host " Stopped" -ForegroundColor DarkRed
}

