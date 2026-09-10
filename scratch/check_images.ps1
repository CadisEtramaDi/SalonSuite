$files = Get-ChildItem "c:\Users\Francis John\SalonSuite\wwwroot\screenshots"
$allOk = $true
foreach ($f in $files) {
    $url = "http://localhost:5261/screenshots/" + $f.Name
    try {
        $res = Invoke-WebRequest -Uri $url -Method Head -UseBasicParsing
        if ($res.StatusCode -ne 200) {
            Write-Host "FAIL: $($f.Name) -> $($res.StatusCode)"
            $allOk = $false
        }
    } catch {
        Write-Host "ERROR: $($f.Name) -> $($_.Exception.Message)"
        $allOk = $false
    }
}
if ($allOk) {
    Write-Host "ALL $($files.Count) SCREENSHOTS ARE ACCESSIBLE VIA HTTP 200 OK!"
}
