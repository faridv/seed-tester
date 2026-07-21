<#
    reset-dongle-test.ps1

    We learned that when the Rockey dongle wedges, the PHYSICAL USB device
    (USB\VID_096E&PID_0006) drops off the bus entirely, while the software "ROCKEY4" node
    (ROOT\USB\0000) stays visible in Device Manager. So toggling the device does nothing -
    we have to power-cycle the HUB/PORT the dongle sits on.

    This script:
      1. Waits for the physical dongle to appear (plug / replug it now).
      2. Prints its parent-hub chain and SAVES the parent hub id to dongle-hub.txt
         (the app will use this to power-cycle the port even after the device drops off).
      3. Optionally power-cycles that hub to prove the device drops and re-appears.

    HOW TO USE:
      Right-click -> "Run with PowerShell" (self-elevates), or:
        powershell -ExecutionPolicy Bypass -File .\reset-dongle-test.ps1
#>

$ErrorActionPreference = 'Stop'
$hw = 'USB\VID_096E&PID_0006'
$hubFile = Join-Path $PSScriptRoot 'dongle-hub.txt'

# --- self-elevate ---
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Not elevated - relaunching as administrator..." -ForegroundColor Yellow
    Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -NoExit -File `"$PSCommandPath`""
    return
}

function Get-PhysDongle { Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.InstanceId -like "$hw*" } }

Write-Host "=== Rockey dongle hub capture + test (elevated) ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Make sure the dongle is plugged in and WORKING. If unsure, unplug and replug it now." -ForegroundColor White
Write-Host "Waiting up to 40s for the physical USB device (VID_096E) to appear..." -ForegroundColor White

$dev = $null
for ($i = 0; $i -lt 40; $i++) { $dev = Get-PhysDongle; if ($dev) { break }; Start-Sleep -Seconds 1 }

if (-not $dev) {
    Write-Host ""
    Write-Host "The physical VID_096E device never appeared." -ForegroundColor Red
    Write-Host "Either the dongle is not actually enumerating, or it uses a different VID/PID." -ForegroundColor Red
    Write-Host "Here are all present USB-attached HID devices - tell me which one is the dongle:" -ForegroundColor Yellow
    Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.InstanceId -like 'USB\*' -and $_.Class -eq 'HIDClass' } | Select-Object InstanceId, FriendlyName | Format-Table -AutoSize -Wrap
    Read-Host "Press Enter to exit"; return
}

$d = @($dev)[0]
Write-Host ""
Write-Host ("Found physical dongle: {0}  (Status={1})" -f $d.InstanceId, $d.Status) -ForegroundColor Green

# Parent-hub chain
Write-Host ""
Write-Host "Parent chain (device -> hub -> ... -> controller):" -ForegroundColor White
$immediateParent = $null
$cur = $d.InstanceId
for ($i = 0; $i -lt 6; $i++) {
    $parent = (Get-PnpDeviceProperty -InstanceId $cur -KeyName 'DEVPKEY_Device_Parent' -ErrorAction SilentlyContinue).Data
    if (-not $parent) { break }
    if ($i -eq 0) { $immediateParent = $parent }
    $pdev = Get-PnpDevice -InstanceId $parent -ErrorAction SilentlyContinue
    Write-Host ("  [{0}] {1}  ({2}, Class={3})" -f $i, $parent, $pdev.FriendlyName, $pdev.Class)
    $cur = $parent
    if ($parent -like 'PCI\*' -or $parent -like 'ROOT\*') { break }
}

if (-not $immediateParent) { Write-Host "Could not determine parent hub." -ForegroundColor Red; Read-Host "Press Enter"; return }

Set-Content -Path $hubFile -Value $immediateParent -Encoding ASCII
Write-Host ""
Write-Host ("Saved parent hub to: {0}" -f $hubFile) -ForegroundColor Green
Write-Host ("  -> {0}" -f $immediateParent)

# Optional: prove the hub cycle re-enumerates the dongle
Write-Host ""
$go = Read-Host "Power-cycle this hub now to prove the dongle drops and re-appears? Other devices on the hub blink off briefly. (y/n)"
if ($go -match '^(y|yes)') {
    $ph = Get-PnpDevice -InstanceId $immediateParent -ErrorAction SilentlyContinue
    Write-Host ("Disabling hub {0} ({1}) ..." -f $immediateParent, $ph.FriendlyName) -ForegroundColor Yellow
    Disable-PnpDevice -InstanceId $immediateParent -Confirm:$false -ErrorAction Stop
    Start-Sleep -Seconds 3
    Write-Host ("  dongle present while hub disabled: {0}" -f [bool](Get-PhysDongle))
    Write-Host "Enabling hub ..." -ForegroundColor Yellow
    Enable-PnpDevice -InstanceId $immediateParent -Confirm:$false -ErrorAction Stop
    Start-Sleep -Seconds 4
    Write-Host ("  dongle present after hub re-enable: {0}" -f [bool](Get-PhysDongle)) -ForegroundColor Green
    Write-Host ""
    Write-Host "NEXT: reproduce the wedge (run the app until the dongle stops), then run this script" -ForegroundColor Cyan
    Write-Host "again and cycle the hub - tell me whether the wedged dongle comes back to life." -ForegroundColor Cyan
}

Write-Host ""
Read-Host "Press Enter to exit"
