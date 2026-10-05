$cs = "Server=.\SQLEXPRESS;Database=SalonSuiteDb;Trusted_Connection=True;TrustServerCertificate=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($cs)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT * FROM dbo.Appointments ORDER BY Id DESC"
$r = $cmd.ExecuteReader()
while($r.Read()) {
    Write-Host ("Appt #" + $r["Id"] + " | CustID: " + $r["CustomerId"] + " | Client: " + $r["ClientName"] + " | Phone: " + $r["ClientPhone"] + " | Email: " + $r["ClientEmail"] + " | Svc: " + $r["ServiceName"] + " | Date: " + $r["Date"] + " | Slot: " + $r["TimeSlot"] + " | Status: " + $r["Status"])
}
$r.Close()
$conn.Close()
