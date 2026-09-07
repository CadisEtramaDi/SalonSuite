$cs = "Server=.\SQLEXPRESS;Database=SalonSuiteDb;Trusted_Connection=True;TrustServerCertificate=True;"

try {
    $conn = New-Object System.Data.SqlClient.SqlConnection($cs)
    $conn.Open()
    Write-Host "-> Connected to SQL Server successfully!" -ForegroundColor Green

    # Check if Jhasser exists or insert
    $checkCmd = $conn.CreateCommand()
    $checkCmd.CommandText = "SELECT COUNT(*) FROM dbo.Customers WHERE FullName = 'Jhasser'"
    $exists = $checkCmd.ExecuteScalar()

    if ($exists -eq 0) {
        $insertCust = $conn.CreateCommand()
        $insertCust.CommandText = "INSERT INTO dbo.Customers (FullName, Email, Phone, LoyaltyPoints, Tier, TotalSpent, VisitsCount, LastVisit, Notes, CreatedAt) OUTPUT INSERTED.Id VALUES ('Jhasser', 'jhasser@example.com', '09979085864', 59, 'Bronze', 5990.00, 1, GETDATE(), 'Ultimate Package booking', GETDATE())"
        $newCustId = $insertCust.ExecuteScalar()
        Write-Host "-> Inserted Customer 'Jhasser' with ID: $newCustId into dbo.Customers" -ForegroundColor Yellow

        $insertAppt = $conn.CreateCommand()
        $insertAppt.CommandText = "INSERT INTO dbo.Appointments (CustomerId, ClientName, ClientPhone, ClientEmail, ServiceName, StylistName, Date, TimeSlot, Price, Status, IsPaid, Notes) OUTPUT INSERTED.Id VALUES ($newCustId, 'Jhasser', '09979085864', 'jhasser@example.com', 'Ultimate Package', 'Sofia Martinez', CAST(GETDATE() AS DATE), '10:30 AM', 5990.00, 'Completed', 1, 'Live Master Schedule Sync')"
        $newApptId = $insertAppt.ExecuteScalar()
        Write-Host "-> Inserted Appointment #$newApptId for 'Jhasser' into dbo.Appointments" -ForegroundColor Yellow
    } else {
        Write-Host "-> Jhasser already exists in dbo.Customers!" -ForegroundColor Cyan
    }

    Write-Host "`n=== CURRENT CUSTOMERS IN SSMS (dbo.Customers) ===" -ForegroundColor Cyan
    $listCmd = $conn.CreateCommand()
    $listCmd.CommandText = "SELECT Id, FullName, Phone, Tier, TotalSpent, VisitsCount FROM dbo.Customers ORDER BY Id ASC"
    $r = $listCmd.ExecuteReader()
    while ($r.Read()) {
        Write-Host ("   ID " + $r["Id"] + " | " + $r["FullName"] + " | " + $r["Phone"] + " | Tier: " + $r["Tier"] + " | Spend: ₱" + $r["TotalSpent"] + " | Visits: " + $r["VisitsCount"])
    }
    $r.Close()

    Write-Host "`n=== CURRENT APPOINTMENTS IN SSMS (dbo.Appointments) ===" -ForegroundColor Cyan
    $listApptCmd = $conn.CreateCommand()
    $listApptCmd.CommandText = "SELECT Id, CustomerId, ClientName, ServiceName, StylistName, Price, Status FROM dbo.Appointments ORDER BY Id ASC"
    $r2 = $listApptCmd.ExecuteReader()
    while ($r2.Read()) {
        Write-Host ("   Appt #" + $r2["Id"] + " | Client: " + $r2["ClientName"] + " (CustID: " + $r2["CustomerId"] + ") | " + $r2["ServiceName"] + " | " + $r2["StylistName"] + " | ₱" + $r2["Price"] + " | " + $r2["Status"])
    }
    $r2.Close()

    Write-Host "`n=== CURRENT SERVICES & HAIRSTYLES IN SSMS (dbo.Services) ===" -ForegroundColor Cyan
    $listSvcCmd = $conn.CreateCommand()
    $listSvcCmd.CommandText = "SELECT Id, Number, Name, Category, Price, DurationMinutes FROM dbo.Services ORDER BY Id ASC"
    $r3 = $listSvcCmd.ExecuteReader()
    while ($r3.Read()) {
        Write-Host ("   Svc #" + $r3["Id"] + " [" + $r3["Number"] + "] " + $r3["Name"] + " (" + $r3["Category"] + ") | ₱" + $r3["Price"] + " | " + $r3["DurationMinutes"] + " mins")
    }
    $r3.Close()

    Write-Host "`n=== CURRENT EMPLOYEES & SPECIALISTS IN SSMS (dbo.Employees) ===" -ForegroundColor Cyan
    $listEmpCmd = $conn.CreateCommand()
    $listEmpCmd.CommandText = "SELECT Id, Name, Role, Email, Phone, CommissionRate, IsActive FROM dbo.Employees ORDER BY Id ASC"
    $r4 = $listEmpCmd.ExecuteReader()
    while ($r4.Read()) {
        Write-Host ("   Emp #" + $r4["Id"] + " | " + $r4["Name"] + " (" + $r4["Role"] + ") | Email: " + $r4["Email"] + " | Phone: " + $r4["Phone"] + " | Active: " + $r4["IsActive"])
    }
    $r4.Close()

    $conn.Close()
} catch {
    Write-Host ("-> ERROR: " + $_.Exception.Message) -ForegroundColor Red
}
