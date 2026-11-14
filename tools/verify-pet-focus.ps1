#Requires -Version 5.1
<#
.SYNOPSIS
Verifies that the desktop cat never takes the foreground from the user (#156).

.DESCRIPTION
Drives real mouse input against a live JiuMi.Desktop process and checks GetForegroundWindow()
before and after click, drag and double-click. The UI project has no unit tests, so this script is
the archived focus evidence requested by #156, following the #141 manual-verification scope.

The desktop must be interactive and unlocked; the script assumes 100% scaling and one screen.

.PARAMETER LeaveToDesktop
Navigate to My JiuMi and drag the page cat onto the desktop before the focus checks.

.PARAMETER DragHome
After the focus checks, drag the desktop cat back into the room and verify the desktop window closes.

.PARAMETER ProcessName
Process name to attach to. Defaults to JiuMi.Desktop.
#>
param(
    [string]$ProcessName = 'JiuMi.Desktop',
    [switch]$LeaveToDesktop,
    [switch]$DragHome
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class PetFocusProbe
{
    public delegate bool EnumProc(IntPtr handle, IntPtr state);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumProc callback, IntPtr state);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr handle, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr handle);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(
        IntPtr handle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, IntPtr extra);

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowLongPtr(IntPtr handle, int index);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr handle, int command);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public const uint LeftDown = 0x0002;
    public const uint LeftUp = 0x0004;
}
'@

$script:failures = 0

function Assert-True([bool]$condition, [string]$message) {
    if ($condition) {
        Write-Host "PASS  $message" -ForegroundColor Green
    }
    else {
        Write-Host "FAIL  $message" -ForegroundColor Red
        $script:failures++
    }
}

function Get-Rect([IntPtr]$handle) {
    $rect = [PetFocusProbe+RECT]::new()
    [void][PetFocusProbe]::GetWindowRect($handle, [ref]$rect)
    return $rect
}

function Get-ProcessWindows([int]$processId) {
    $windows = New-Object System.Collections.ArrayList
    $callback = [PetFocusProbe+EnumProc] {
        param([IntPtr]$handle, [IntPtr]$state)
        [uint32]$owner = 0
        [void][PetFocusProbe]::GetWindowThreadProcessId($handle, [ref]$owner)
        if ($owner -eq $processId) {
            [void]$windows.Add($handle)
        }

        return $true
    }

    [void][PetFocusProbe]::EnumWindows($callback, [IntPtr]::Zero)
    return $windows
}

function Get-MainWindow([int]$processId) {
    $process = Get-Process -Id $processId
    if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
        return $process.MainWindowHandle
    }

    $largest = [IntPtr]::Zero
    $largestArea = 0
    foreach ($handle in Get-ProcessWindows $processId) {
        $rect = Get-Rect $handle
        $area = ($rect.Right - $rect.Left) * ($rect.Bottom - $rect.Top)
        if ($area -gt $largestArea) {
            $largest = $handle
            $largestArea = $area
        }
    }

    return $largest
}

function Get-DesktopCat([int]$processId, [IntPtr]$mainHandle) {
    foreach ($handle in Get-ProcessWindows $processId) {
        if ($handle -eq $mainHandle -or -not [PetFocusProbe]::IsWindowVisible($handle)) {
            continue
        }

        $rect = Get-Rect $handle
        if ((($rect.Right - $rect.Left) -eq 160) -and (($rect.Bottom - $rect.Top) -eq 160)) {
            return $handle
        }
    }

    return [IntPtr]::Zero
}

function Get-ForegroundInfo {
    $handle = [PetFocusProbe]::GetForegroundWindow()
    [uint32]$owner = 0
    [void][PetFocusProbe]::GetWindowThreadProcessId($handle, [ref]$owner)
    $name = (Get-Process -Id $owner -ErrorAction SilentlyContinue).ProcessName
    return [pscustomobject]@{
        Handle = $handle
        ProcessId = [int]$owner
        ProcessName = $name
    }
}

function Assert-Foreground([IntPtr]$expected, [string]$message) {
    $info = Get-ForegroundInfo
    Assert-True ($info.Handle -eq $expected) ("$message (fg=$($info.ProcessName) $($info.Handle))")
}

function Invoke-Click([int]$x, [int]$y) {
    [void][PetFocusProbe]::SetCursorPos($x, $y)
    Start-Sleep -Milliseconds 120
    [PetFocusProbe]::mouse_event([PetFocusProbe]::LeftDown, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [PetFocusProbe]::mouse_event([PetFocusProbe]::LeftUp, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 400
}

function Invoke-DoubleClick([int]$x, [int]$y) {
    [void][PetFocusProbe]::SetCursorPos($x, $y)
    Start-Sleep -Milliseconds 150
    [PetFocusProbe]::mouse_event([PetFocusProbe]::LeftDown, 0, 0, 0, [IntPtr]::Zero)
    [PetFocusProbe]::mouse_event([PetFocusProbe]::LeftUp, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    [PetFocusProbe]::mouse_event([PetFocusProbe]::LeftDown, 0, 0, 0, [IntPtr]::Zero)
    [PetFocusProbe]::mouse_event([PetFocusProbe]::LeftUp, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 900
}

function Get-DragDelta($catRect, $mainRect, $workArea) {
    $mainCenterX = ($mainRect.Left + $mainRect.Right) / 2
    $mainCenterY = ($mainRect.Top + $mainRect.Bottom) / 2
    $catCenterX = ($catRect.Left + $catRect.Right) / 2
    $catCenterY = ($catRect.Top + $catRect.Bottom) / 2
    $deltaX = if ($catCenterX -lt $mainCenterX) { -48 } else { 48 }
    $deltaY = if ($catCenterY -lt $mainCenterY) { -32 } else { 32 }
    if ((($catRect.Left + $deltaX) -lt $workArea.Left) -or (($catRect.Right + $deltaX) -gt $workArea.Right)) {
        $deltaX = -$deltaX
    }

    if ((($catRect.Top + $deltaY) -lt $workArea.Top) -or (($catRect.Bottom + $deltaY) -gt $workArea.Bottom)) {
        $deltaY = -$deltaY
    }

    return @($deltaX, $deltaY)
}

function Invoke-Drag([int]$fromX, [int]$fromY, [int]$toX, [int]$toY, [int]$steps = 20) {
    [void][PetFocusProbe]::SetCursorPos($fromX, $fromY)
    Start-Sleep -Milliseconds 150
    [PetFocusProbe]::mouse_event([PetFocusProbe]::LeftDown, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    for ($index = 1; $index -le $steps; $index++) {
        $x = [int]($fromX + (($toX - $fromX) * $index / $steps))
        $y = [int]($fromY + (($toY - $fromY) * $index / $steps))
        [void][PetFocusProbe]::SetCursorPos($x, $y)
        Start-Sleep -Milliseconds 16
    }

    Start-Sleep -Milliseconds 120
    [PetFocusProbe]::mouse_event([PetFocusProbe]::LeftUp, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 400
}

function Select-MyJiuMiPage([IntPtr]$mainHandle) {
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($mainHandle)
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        '我的JiuMi')
    $items = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
    foreach ($item in $items) {
        $pattern = $null
        if ($item.TryGetCurrentPattern(
                [System.Windows.Automation.SelectionItemPattern]::Pattern,
                [ref]$pattern)) {
            $pattern.Select()
            Start-Sleep -Milliseconds 900
            return $true
        }
    }

    return $false
}

function Find-Element([IntPtr]$rootHandle, [string]$name) {
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($rootHandle)
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        $name)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Move-CatToDesktop([IntPtr]$mainHandle) {
    [void][PetFocusProbe]::ShowWindow($mainHandle, 9)
    [void][PetFocusProbe]::SetForegroundWindow($mainHandle)
    Start-Sleep -Milliseconds 500
    if (-not (Select-MyJiuMiPage $mainHandle)) {
        throw 'The My JiuMi navigation item was not found.'
    }

    $cat = Find-Element $mainHandle 'JiuMi 小猫'
    if ($null -eq $cat) {
        throw 'The page cat was not found; bring the cat home before running with -LeaveToDesktop.'
    }

    $catRect = $cat.Current.BoundingRectangle
    $mainRect = Get-Rect $mainHandle
    $workArea = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    $fromX = [int]($catRect.Left + ($catRect.Width / 2))
    $fromY = [int]($catRect.Top + ($catRect.Height / 2))
    if (($mainRect.Right + 160) -lt $workArea.Right) {
        $toX = $mainRect.Right + 120
    }
    else {
        $toX = $mainRect.Left - 120
    }

    $toY = [int](($mainRect.Top + $mainRect.Bottom) / 2)
    Write-Host "dragging the page cat out from $fromX,$fromY to $toX,$toY"
    Invoke-Drag $fromX $fromY $toX $toY 45
    Start-Sleep -Milliseconds 800
}

$process = Get-Process -Name $ProcessName -ErrorAction Stop
$processId = [int]$process.Id
$mainHandle = Get-MainWindow $processId
if ($mainHandle -eq [IntPtr]::Zero) {
    throw "The $ProcessName main window was not found."
}

$desktopCat = Get-DesktopCat $processId $mainHandle
if ($LeaveToDesktop -or $desktopCat -eq [IntPtr]::Zero) {
    if (-not $LeaveToDesktop) {
        throw 'The desktop cat was not found; rerun with -LeaveToDesktop or walk the cat out first.'
    }

    Move-CatToDesktop $mainHandle
    $desktopCat = Get-DesktopCat $processId $mainHandle
}

if ($desktopCat -eq [IntPtr]::Zero) {
    throw 'The desktop cat window was not found after leaving the page.'
}

$notepad = $null
try {
    $style = [PetFocusProbe]::GetWindowLongPtr($desktopCat, -20).ToInt64()
    Write-Host ("desktop cat hwnd={0} ex=0x{1:X}" -f $desktopCat, $style)
    Assert-True (($style -band 0x08000000) -ne 0) 'desktop host has WS_EX_NOACTIVATE'
    Assert-True (($style -band 0x00000080) -ne 0) 'desktop host has WS_EX_TOOLWINDOW'
    Assert-True (($style -band 0x00000008) -ne 0) 'desktop host stays topmost'

    $notepad = Start-Process notepad.exe -PassThru
    for ($index = 0; $index -lt 50 -and $notepad.MainWindowHandle -eq [IntPtr]::Zero; $index++) {
        Start-Sleep -Milliseconds 100
        $notepad.Refresh()
    }

    $notepadHandle = $notepad.MainWindowHandle
    if ($notepadHandle -eq [IntPtr]::Zero) {
        throw 'Notepad did not create a window.'
    }

    [void][PetFocusProbe]::SetWindowPos($notepadHandle, [IntPtr]::Zero, 10, 10, 420, 260, 0x0040)
    [void][PetFocusProbe]::SetForegroundWindow($notepadHandle)
    Start-Sleep -Milliseconds 500
    Assert-Foreground $notepadHandle 'Notepad owns the foreground before the click'

    $catRect = Get-Rect $desktopCat
    $catX = [int](($catRect.Left + $catRect.Right) / 2)
    $catY = [int](($catRect.Top + $catRect.Bottom) / 2)
    Invoke-Click $catX $catY
    Assert-Foreground $notepadHandle 'click keeps another application in the foreground'

    $beforeDrag = Get-Rect $desktopCat
    $delta = Get-DragDelta $beforeDrag (Get-Rect $mainHandle) ([System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea)
    Invoke-Drag $catX $catY ($catX + $delta[0]) ($catY + $delta[1])
    Assert-Foreground $notepadHandle 'drag keeps another application in the foreground'
    $afterDrag = Get-Rect $desktopCat
    Assert-True (($afterDrag.Left -ne $beforeDrag.Left) -or ($afterDrag.Top -ne $beforeDrag.Top)) 'drag still moves the desktop cat'
    Assert-True ((Get-DesktopCat $processId $mainHandle) -ne [IntPtr]::Zero) 'drag keeps the cat on the desktop'

    $desktopCat = Get-DesktopCat $processId $mainHandle
    $catRect = Get-Rect $desktopCat
    $catX = [int](($catRect.Left + $catRect.Right) / 2)
    $catY = [int](($catRect.Top + $catRect.Bottom) / 2)
    Invoke-DoubleClick $catX $catY
    Assert-Foreground $mainHandle 'double-click opens and activates the My JiuMi window'
    Assert-True ((Get-DesktopCat $processId $mainHandle) -ne [IntPtr]::Zero) 'double-click leaves the cat on the desktop'

    [void][PetFocusProbe]::ShowWindow($mainHandle, 9)
    [void][PetFocusProbe]::SetForegroundWindow($mainHandle)
    Start-Sleep -Milliseconds 500
    Assert-Foreground $mainHandle 'the main window owns the foreground before the same-process checks'

    $desktopCat = Get-DesktopCat $processId $mainHandle
    $catRect = Get-Rect $desktopCat
    $catX = [int](($catRect.Left + $catRect.Right) / 2)
    $catY = [int](($catRect.Top + $catRect.Bottom) / 2)
    Invoke-Click $catX $catY
    Assert-Foreground $mainHandle 'same-process click keeps the main window in the foreground'

    $delta = Get-DragDelta (Get-Rect $desktopCat) (Get-Rect $mainHandle) ([System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea)
    Invoke-Drag $catX $catY ($catX + $delta[0]) ($catY + $delta[1])
    Assert-Foreground $mainHandle 'same-process drag keeps the main window in the foreground'
    Assert-True ((Get-DesktopCat $processId $mainHandle) -ne [IntPtr]::Zero) 'same-process drag keeps the cat on the desktop'

    if ($DragHome) {
        $desktopCat = Get-DesktopCat $processId $mainHandle
        $catRect = Get-Rect $desktopCat
        $catX = [int](($catRect.Left + $catRect.Right) / 2)
        $catY = [int](($catRect.Top + $catRect.Bottom) / 2)
        if (-not (Select-MyJiuMiPage $mainHandle)) {
            throw 'The My JiuMi navigation item was not found before dragging home.'
        }

        $hint = Find-Element $mainHandle '小猫在桌面上，双击地面叫它回来'
        if ($null -eq $hint) {
            throw 'The desktop hint was not found; the room target is unknown.'
        }

        $target = $hint.Current.BoundingRectangle
        Invoke-Drag $catX $catY ([int]($target.Left + ($target.Width / 2))) ([int]($target.Top + ($target.Height / 2))) 30
        Start-Sleep -Milliseconds 800
        Assert-True ((Get-DesktopCat $processId $mainHandle) -eq [IntPtr]::Zero) 'dragging into the room closes the desktop window'
        Assert-True ($null -ne (Find-Element $mainHandle 'JiuMi 小猫')) 'the page cat is back in the room'
    }
}
finally {
    [PetFocusProbe]::mouse_event([PetFocusProbe]::LeftUp, 0, 0, 0, [IntPtr]::Zero)
    if ($null -ne $notepad) {
        $notepad.CloseMainWindow() | Out-Null
        Start-Sleep -Milliseconds 300
        if (-not $notepad.HasExited) {
            $notepad.Kill()
        }
    }
}

Write-Host ''
if ($script:failures -eq 0) {
    Write-Host 'pet focus verification passed' -ForegroundColor Green
    exit 0
}

Write-Host "$($script:failures) pet focus check(s) failed" -ForegroundColor Red
exit 1
