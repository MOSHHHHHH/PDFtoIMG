param([string]$Executable = "publish/PdfToImages.exe")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$process = Start-Process -FilePath (Resolve-Path $Executable).Path -PassThru
try {
    if (-not $process.WaitForInputIdle(30000)) { throw 'WPF did not become idle.' }
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        $process.Refresh()
        if ($process.HasExited) { throw "WPF exited during startup: $($process.ExitCode)" }
        if ($process.MainWindowHandle -ne 0) { break }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($process.MainWindowHandle -eq 0) { throw 'WPF did not create a main window.' }
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    function Find-Control([string]$Id) {
        $condition = [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $Id)
        $control = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($null -eq $control) { throw "Missing UI control: $Id" }
        return $control
    }
    function Selected-Name($Control) {
        $pattern = $Control.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern)
        return $pattern.Current.GetSelection()[0].Current.Name
    }
    $output = Find-Control 'OutputTypeCombo'
    $dpi = Find-Control 'DpiCombo'
    if ((Selected-Name $dpi) -notmatch '^300') { throw 'Images must default to 300 DPI.' }
    $expand = $output.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand()
    $items = $output.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::ListItem))
    if ($items.Count -ne 2) { throw "Expected two output modes; found $($items.Count)." }
    $items[1].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $expand.Collapse()
    if ((Selected-Name $dpi) -notmatch '^200') { throw 'DOCX must default to 200 DPI.' }
    if ((Find-Control 'FormatCombo').Current.IsEnabled) { throw 'Image format must be disabled for DOCX.' }
    $expand.Expand()
    $items[0].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $expand.Collapse()
    if ((Selected-Name $dpi) -notmatch '^300') { throw 'Switching back must restore image DPI.' }
    if (-not (Find-Control 'FormatCombo').Current.IsEnabled) { throw 'Image format must be enabled for images.' }
    Write-Host 'PASS: self-contained EXE starts; both output modes and DPI defaults work.'
}
finally {
    if (-not $process.HasExited) {
        [void]$process.CloseMainWindow()
        if (-not $process.WaitForExit(5000)) { $process.Kill() }
    }
}
