$cs = "Server=.\SQLEXPRESS;Database=SalonSuiteDb;Trusted_Connection=True;TrustServerCertificate=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($cs)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT * FROM dbo.Appointments WHERE TimeSlot LIKE '%Walk-in%' OR ServiceName LIKE '%Essential%' OR Date >= '2026-09-20'"
$r = $cmd.ExecuteReader()
while($r.Read()) {
    Write-Host ("Appt #" + $r["Id"] + " | CustId: " + $r["CustomerId"] + " | Client: " + $r["ClientName"] + " | Svc: " + $r["ServiceName"] + " | Date: " + $r["Date"] + " | Slot: " + $r["TimeSlot"] + " | Status: " + $r["Status"] + " | Price: " + $r["Price"])
}
$conn.Close()
