param(
    [string]$LogDir = (Join-Path $PSScriptRoot '..\WeChatSpike\logs')
)

$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class WinInetRefresh {
    [DllImport("wininet.dll", SetLastError = true)]
    public static extern bool InternetSetOption(IntPtr hInternet, int option, IntPtr buffer, int bufferLength);

    public static void Refresh() {
        InternetSetOption(IntPtr.Zero, 39, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, 37, IntPtr.Zero, 0);
    }
}
"@

$keyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings'
$snapshotPath = Join-Path $LogDir 'proxy-snapshot.json'

if (Test-Path $snapshotPath) {
    $snapshot = Get-Content $snapshotPath -Raw | ConvertFrom-Json
    $values = @{
        ProxyEnable   = $snapshot.ProxyEnable
        ProxyServer   = $snapshot.ProxyServer
        ProxyOverride = $snapshot.ProxyOverride
        AutoConfigURL = $snapshot.AutoConfigURL
        AutoDetect    = $snapshot.AutoDetect
    }
    foreach ($name in $values.Keys) {
        $value = $values[$name]
        if ($null -eq $value -or ($value -is [string] -and $value -eq '')) {
            Remove-ItemProperty -Path $keyPath -Name $name -ErrorAction SilentlyContinue
        } elseif ($value -is [int]) {
            New-ItemProperty -Path $keyPath -Name $name -Value $value -PropertyType DWord -Force | Out-Null
        } else {
            New-ItemProperty -Path $keyPath -Name $name -Value ([string]$value) -PropertyType String -Force | Out-Null
        }
    }
    [WinInetRefresh]::Refresh()
    Write-Output "Proxy restored from $snapshotPath"
} else {
    Write-Warning "No proxy snapshot at $snapshotPath; proxy was not changed."
}

foreach ($storeName in @('Root', 'My')) {
    $store = New-Object System.Security.Cryptography.X509Certificates.X509Store($storeName, 'CurrentUser')
    $store.Open('ReadWrite')
    $matches = $store.Certificates | Where-Object { $_.Subject -eq 'CN=JiumiSpike' }
    foreach ($cert in $matches) {
        $store.Remove($cert)
        Write-Output ("Removed CN=JiumiSpike from CurrentUser\{0} ({1})" -f $storeName, $cert.Thumbprint)
    }
    $store.Close()
}

Write-Output 'Current proxy settings:'
Get-ItemProperty -Path $keyPath | Select-Object ProxyEnable, ProxyServer, ProxyOverride, AutoConfigURL, AutoDetect | Format-List
Write-Output 'Remaining JiumiSpike certificates:'
$remaining = Get-ChildItem Cert:\CurrentUser\Root, Cert:\CurrentUser\My | Where-Object { $_.Subject -eq 'CN=JiumiSpike' }
if ($remaining) {
    $remaining | Select-Object Subject, Thumbprint
} else {
    Write-Output 'none'
}
Write-Output 'Note: stop WeChatSpike before running this script if it is still running.'