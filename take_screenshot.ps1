Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# Wait longer for the 3.6M triangles to upload to GPU
Start-Sleep -Seconds 30

$Screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$Bitmap = New-Object System.Drawing.Bitmap $Screen.Width, $Screen.Height
$Graphics = [System.Drawing.Graphics]::FromImage($Bitmap)
$Graphics.CopyFromScreen($Screen.X, $Screen.Y, 0, 0, $Bitmap.Size)
$Bitmap.Save("C:\Dev\OpenRender\final_verification.png", [System.Drawing.Imaging.ImageFormat]::Png)

$Graphics.Dispose()
$Bitmap.Dispose()

Write-Host "Verification screenshot saved."
