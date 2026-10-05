using System;
using System.Linq;
using System.Threading.Tasks;
using SalonSuite.Services;
using SalonSuite.Models;

EnvLoader.Load();
var service = new SalonDataService();
service.EnsureSeedData();

Console.WriteLine($"DB Connected: {service.IsDatabaseConnected}");
Console.WriteLine($"Customers count: {service.Customers.Count}");
foreach(var c in service.Customers)
{
    Console.WriteLine($"Cust #{c.Id} | Name: '{c.FullName}' | Phone: '{c.Phone}' | Email: '{c.Email}' | Tier: '{c.Tier}'");
}

Console.WriteLine($"\nAppointments count: {service.Appointments.Count}");
foreach(var a in service.Appointments)
{
    Console.WriteLine($"Appt #{a.Id} | CustID: {a.CustomerId} | Name: '{a.ClientName}' | Phone: '{a.ClientPhone}' | Email: '{a.ClientEmail}' | Svc: '{a.ServiceName}' | Date: {a.Date:yyyy-MM-dd} | Slot: '{a.TimeSlot}' | Status: '{a.Status}'");
}
