[CmdletBinding()]
param (
    [Parameter()]
    [Switch] $Push = $false
)

. docker build --tag docker.zvirja.com/aspnet-calibre:latest .

if ($Push) {
    . docker push docker.zvirja.com/aspnet-calibre:latest
}
