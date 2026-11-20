-- Run once against SQL Server / LocalDB (e.g. sqlcmd -S "(localdb)\MSSQLLocalDB" -i schema.sql)
IF DB_ID(N'StoreDb') IS NULL CREATE DATABASE StoreDb;
GO
USE StoreDb;
GO

IF OBJECT_ID(N'dbo.Products') IS NULL
CREATE TABLE dbo.Products (
    Id       INT IDENTITY(1,1) CONSTRAINT PK_Products PRIMARY KEY,
    Name     NVARCHAR(100) NOT NULL,
    Category NVARCHAR(50)  NOT NULL,
    Price    DECIMAL(10,2) NOT NULL CONSTRAINT CK_Products_Price CHECK (Price > 0),
    Stock    INT           NOT NULL CONSTRAINT CK_Products_Stock CHECK (Stock >= 0)
);
GO

IF OBJECT_ID(N'dbo.Orders') IS NULL
CREATE TABLE dbo.Orders (
    Id           INT IDENTITY(1,1) CONSTRAINT PK_Orders PRIMARY KEY,
    CustomerName NVARCHAR(100) NOT NULL,
    CreatedUtc   DATETIME2(7)  NOT NULL,
    Total        DECIMAL(12,2) NOT NULL
);
GO

IF OBJECT_ID(N'dbo.OrderItems') IS NULL
CREATE TABLE dbo.OrderItems (
    Id        INT IDENTITY(1,1) CONSTRAINT PK_OrderItems PRIMARY KEY,
    OrderId   INT NOT NULL CONSTRAINT FK_OrderItems_Orders   FOREIGN KEY REFERENCES dbo.Orders(Id),
    ProductId INT NOT NULL CONSTRAINT FK_OrderItems_Products FOREIGN KEY REFERENCES dbo.Products(Id),
    Quantity  INT NOT NULL CONSTRAINT CK_OrderItems_Qty CHECK (Quantity > 0),
    UnitPrice DECIMAL(10,2) NOT NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Products_Category')
    CREATE INDEX IX_Products_Category ON dbo.Products(Category);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OrderItems_OrderId')
    CREATE INDEX IX_OrderItems_OrderId ON dbo.OrderItems(OrderId);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Products)
INSERT INTO dbo.Products (Name, Category, Price, Stock) VALUES
    (N'Mechanical Keyboard', N'Electronics', 79.99, 25),
    (N'Wireless Mouse',      N'Electronics', 24.50, 60),
    (N'USB-C Hub',           N'Electronics', 39.00, 40),
    (N'Standing Desk Mat',   N'Office',      45.00, 15),
    (N'Notebook (A5)',       N'Office',       4.25, 200),
    (N'Desk Lamp',           N'Home',        32.75, 30);
GO
