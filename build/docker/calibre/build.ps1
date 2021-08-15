[CmdletBinding()]
param (
    [Parameter()]
    [Switch] $Push = $false
)

. docker build --tag vps.zvirja.linkpc.net/aspnet-calibre:latest .

if ($Push) {
    . docker push vps.zvirja.linkpc.net/aspnet-calibre:latest
}
