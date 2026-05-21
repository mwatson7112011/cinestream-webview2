$sh = New-Object -ComObject WScript.Shell
$target = $sh.CreateShortcut("C:\Users\mwats\OneDrive\Desktop\CineStream.lnk")
Write-Host "Target: $($target.TargetPath)"
Write-Host "WorkingDir: $($target.WorkingDirectory)"
Write-Host "IconLocation: $($target.IconLocation)"
