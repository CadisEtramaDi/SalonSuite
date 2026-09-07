-- ==============================================================================
-- SALON SUITE ERP & CRM - SQL SERVER DATABASE SCHEMA SCRIPT
-- Compatible with SQL Server 2019 / 2022 & SQL Server Management Studio (SSMS)
-- ==============================================================================

USE master;
GO

-- 1. CREATE DATABASE IF NOT EXISTS
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'SalonSuiteDb')
BEGIN
    CREATE DATABASE SalonSuiteDb;
END
GO

USE SalonSuiteDb;
GO

-- 2. CREATE TABLE: Customers (Module 1 & 8 - CRM & Loyalty)
IF OBJECT_ID('dbo.Customers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customers (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FullName NVARCHAR(150) NOT NULL,
        Email NVARCHAR(150) NULL,
        Phone NVARCHAR(50) NOT NULL,
        LoyaltyPoints INT NOT NULL DEFAULT 0,
        Tier NVARCHAR(50) NOT NULL DEFAULT 'Bronze', -- Bronze, Silver, Gold, Platinum
        TotalSpent DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        VisitsCount INT NOT NULL DEFAULT 0,
        LastVisit DATETIME NULL,
        Notes NVARCHAR(MAX) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END
GO

-- 3. CREATE TABLE: Employees (Module 4 - Staff Management)
IF OBJECT_ID('dbo.Employees', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Employees (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(150) NOT NULL,
        Role NVARCHAR(100) NOT NULL DEFAULT 'Senior Stylist',
        Specialization NVARCHAR(200) NOT NULL DEFAULT 'Precision Haircut & Styling',
        CommissionRate DECIMAL(5,2) NOT NULL DEFAULT 0.25, -- 25%
        Phone NVARCHAR(50) NULL,
        Email NVARCHAR(150) NULL,
        ImageUrl NVARCHAR(500) NULL,
        IsActive BIT NOT NULL DEFAULT 1
    );
END
GO

-- 4. CREATE TABLE: Suppliers (Module 6 - Supplier Management)
IF OBJECT_ID('dbo.Suppliers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Suppliers (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CompanyName NVARCHAR(200) NOT NULL,
        ContactPerson NVARCHAR(150) NOT NULL,
        Email NVARCHAR(150) NULL,
        Phone NVARCHAR(50) NOT NULL,
        Address NVARCHAR(300) NULL,
        SuppliedCategory NVARCHAR(100) NOT NULL DEFAULT 'Hair Care & Cosmetics',
        PaymentTerms NVARCHAR(50) NOT NULL DEFAULT 'Net 30',
        IsActive BIT NOT NULL DEFAULT 1
    );
END
GO

-- 5. CREATE TABLE: Services (Module 3 - Service Management)
IF OBJECT_ID('dbo.Services', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Services (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Number NVARCHAR(20) NOT NULL,
        Name NVARCHAR(150) NOT NULL,
        Category NVARCHAR(100) NOT NULL DEFAULT 'Hair',
        Description NVARCHAR(500) NULL,
        Price DECIMAL(18,2) NOT NULL,
        DurationMinutes INT NOT NULL DEFAULT 45,
        IsActive BIT NOT NULL DEFAULT 1
    );
END
GO

-- 6. CREATE TABLE: ServicePackages (Module 3 - Package Offerings)
IF OBJECT_ID('dbo.ServicePackages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ServicePackages (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(150) NOT NULL,
        Price DECIMAL(18,2) NOT NULL,
        IsPopular BIT NOT NULL DEFAULT 0,
        FeaturesJson NVARCHAR(MAX) NULL
    );
END
GO

-- 7. CREATE TABLE: Products (Module 5 - Product Inventory)
IF OBJECT_ID('dbo.Products', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        SKU NVARCHAR(50) NOT NULL UNIQUE,
        Name NVARCHAR(200) NOT NULL,
        Category NVARCHAR(100) NOT NULL DEFAULT 'Hair Care',
        CostPrice DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        RetailPrice DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        StockQuantity INT NOT NULL DEFAULT 0,
        ReorderLevel INT NOT NULL DEFAULT 5,
        SupplierId INT NULL FOREIGN KEY REFERENCES dbo.Suppliers(Id),
        SupplierName NVARCHAR(200) NULL,
        Unit NVARCHAR(50) NOT NULL DEFAULT 'Bottle'
    );
END
GO

-- 8. CREATE TABLE: Appointments (Module 2 - Appointment Scheduling)
IF OBJECT_ID('dbo.Appointments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Appointments (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CustomerId INT NULL FOREIGN KEY REFERENCES dbo.Customers(Id),
        ClientName NVARCHAR(150) NOT NULL,
        ClientPhone NVARCHAR(50) NOT NULL,
        ClientEmail NVARCHAR(150) NULL,
        ServiceName NVARCHAR(200) NOT NULL,
        StylistName NVARCHAR(150) NOT NULL,
        Date DATE NOT NULL DEFAULT CAST(GETDATE() AS DATE),
        TimeSlot NVARCHAR(50) NOT NULL,
        Price DECIMAL(18,2) NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Confirmed', -- Confirmed, In Progress, Completed, Cancelled
        IsPaid BIT NOT NULL DEFAULT 0,
        Notes NVARCHAR(MAX) NULL
    );
END
GO

-- 9. CREATE TABLE: Promotions (Module 9 - Promotions & Vouchers)
IF OBJECT_ID('dbo.Promotions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Promotions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Code NVARCHAR(50) NOT NULL UNIQUE,
        Title NVARCHAR(200) NOT NULL,
        DiscountType NVARCHAR(50) NOT NULL DEFAULT 'Percentage', -- Percentage, Fixed
        DiscountValue DECIMAL(18,2) NOT NULL,
        MinSpend DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        StartDate DATE NOT NULL,
        EndDate DATE NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        UsageCount INT NOT NULL DEFAULT 0
    );
END
GO

-- 10. CREATE TABLE: LoyaltyRewards (Module 8 - Loyalty Program)
IF OBJECT_ID('dbo.LoyaltyRewards', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LoyaltyRewards (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Title NVARCHAR(200) NOT NULL,
        PointsRequired INT NOT NULL,
        DiscountValue DECIMAL(18,2) NOT NULL,
        Description NVARCHAR(500) NULL,
        IsActive BIT NOT NULL DEFAULT 1
    );
END
GO

-- 11. CREATE TABLE: Invoices (Module 7 & 10 - Billing & Reports)
IF OBJECT_ID('dbo.Invoices', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Invoices (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        AppointmentId INT NOT NULL,
        CustomerId INT NULL FOREIGN KEY REFERENCES dbo.Customers(Id),
        InvoiceNumber NVARCHAR(50) NOT NULL UNIQUE,
        ClientName NVARCHAR(150) NOT NULL,
        ServiceName NVARCHAR(200) NOT NULL,
        StylistName NVARCHAR(150) NOT NULL,
        Subtotal DECIMAL(18,2) NOT NULL,
        RetailAddonsTotal DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        Discount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        PromoCode NVARCHAR(50) NULL,
        PromoDiscount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        LoyaltyPointsRedeemed INT NOT NULL DEFAULT 0,
        LoyaltyDiscount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        Total DECIMAL(18,2) NOT NULL,
        AmountPaid DECIMAL(18,2) NOT NULL,
        Change DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        PaymentMethod NVARCHAR(50) NOT NULL DEFAULT 'Cash', -- Cash, Credit Card, GCash, Maya
        Timestamp DATETIME NOT NULL DEFAULT GETDATE(),
        CashierName NVARCHAR(100) NOT NULL DEFAULT 'Front Desk'
    );
END
GO

-- ==============================================================================
-- INITIAL SEED DATA
-- ==============================================================================

-- Seed Suppliers
IF NOT EXISTS (SELECT 1 FROM dbo.Suppliers)
BEGIN
    INSERT INTO dbo.Suppliers (CompanyName, ContactPerson, Email, Phone, Address, SuppliedCategory, PaymentTerms)
    VALUES 
    ('L''Oréal Professional PH', 'Marcus Vance', 'orders@loreal.ph', '+63 917 111 2233', 'Bonifacio Global City, Taguig', 'Hair Color & Treatments', 'Net 30'),
    ('Kerastase Luxury Supply', 'Elena Rostova', 'elena@kerastase.ph', '+63 918 222 3344', 'Makati City, Metro Manila', 'Premium Serums & Shampoos', 'Net 15'),
    ('Dyson Pro Tools Manila', 'David Ang', 'sales@dysonpro.ph', '+63 920 333 4455', 'Ortigas Center, Pasig City', 'Hair Dryers & Salon Tools', 'COD');
END
GO

-- Seed Products (Inventory)
IF NOT EXISTS (SELECT 1 FROM dbo.Products)
BEGIN
    INSERT INTO dbo.Products (SKU, Name, Category, CostPrice, RetailPrice, StockQuantity, ReorderLevel, SupplierName, Unit)
    VALUES
    ('SKU-MOR-01', 'Moroccan Argan Elixir Serum', 'Retail', 220.00, 450.00, 24, 5, 'Kerastase Luxury Supply', 'Bottle'),
    ('SKU-KER-02', 'Keratin Smoothing Daily Shampoo', 'Retail', 310.00, 650.00, 18, 5, 'L''Oréal Professional PH', 'Bottle'),
    ('SKU-TON-03', 'Botanical Scalp Detox Tonic', 'Retail', 180.00, 350.00, 12, 4, 'Kerastase Luxury Supply', 'Bottle'),
    ('SKU-COL-04', 'Majirel Ash Blonde 8.1 Tube', 'Color', 380.00, 750.00, 4, 6, 'L''Oréal Professional PH', 'Tube'),
    ('SKU-BLE-05', 'Blond Studio 9 Lightener (500g)', 'Color', 850.00, 1600.00, 3, 5, 'L''Oréal Professional PH', 'Jar');
END
GO

-- Seed Customers (CRM)
IF NOT EXISTS (SELECT 1 FROM dbo.Customers)
BEGIN
    INSERT INTO dbo.Customers (FullName, Email, Phone, LoyaltyPoints, Tier, TotalSpent, VisitsCount, LastVisit, Notes)
    VALUES
    ('Alexandra Rivera', 'alexandra.r@gmail.com', '+63 917 555 0192', 450, 'Gold', 14500.00, 6, GETDATE(), 'Prefers cool ash blonde balayage, sensitive scalp.'),
    ('Marcus Thompson', 'm.thompson@corp.ph', '+63 918 444 8812', 120, 'Silver', 4200.00, 3, GETDATE(), 'Executive cut with beard line sculpting.'),
    ('Natalia Kim', 'natalia.kim@fashion.ph', '+63 920 333 1290', 850, 'Platinum', 28900.00, 9, GETDATE(), 'VIP client. Always books Signature package with Sofia.'),
    ('David Chen', 'david.chen@studio.com', '+63 915 222 9011', 80, 'Bronze', 2450.00, 2, GETDATE(), 'Weekend styling and scalp massage ritual.');
END
GO

-- Seed Promotions
IF NOT EXISTS (SELECT 1 FROM dbo.Promotions)
BEGIN
    INSERT INTO dbo.Promotions (Code, Title, DiscountType, DiscountValue, MinSpend, StartDate, EndDate, IsActive, UsageCount)
    VALUES
    ('WELCOME10', 'New Client First Visit 10% OFF', 'Percentage', 10.00, 500.00, '2026-01-01', '2026-12-31', 1, 14),
    ('VIP200', 'VIP Flat Voucher ₱200 OFF', 'Fixed', 200.00, 1500.00, '2026-01-01', '2026-12-31', 1, 28),
    ('SUMMERGLOW', 'Summer Balayage Special 15% OFF', 'Percentage', 15.00, 2500.00, '2026-03-01', '2026-08-31', 1, 9);
END
GO

-- Seed Loyalty Rewards
IF NOT EXISTS (SELECT 1 FROM dbo.LoyaltyRewards)
BEGIN
    INSERT INTO dbo.LoyaltyRewards (Title, PointsRequired, DiscountValue, Description)
    VALUES
    ('Free Scalp Detox Treatment', 200, 350.00, 'Redeem 200 points for a complimentary scalp massage & tonic ritual.'),
    ('₱500 Beauty Voucher', 400, 500.00, 'Redeem 400 points for ₱500 off any treatment or service package.'),
    ('₱1,000 Luxury VIP Pass', 750, 1000.00, 'Redeem 750 points for ₱1,000 credit towards any luxury balayage or ritual.');
END
GO

PRINT 'SalonSuiteDb database schema and initial seed data created successfully!';
GO
