$cs = "Server=.\SQLEXPRESS;Database=SalonSuiteDb;Trusted_Connection=True;TrustServerCertificate=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($cs)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Id, FullName, Phone, Email, Tier FROM dbo.Customers ORDER BY Id"
$r = $cmd.ExecuteReader()
while($r.Read()) {
    Write-Host ("Cust #" + $r["Id"] + " | " + $r["FullName"] + " | " + $r["Phone"] + " | " + $r["Email"] + " | " + $r["Tier"])
}
$conn.Close()
