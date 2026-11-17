-- =====================================================================================
-- Script Name: MultiProduct_DB_Setup.sql
-- Description: Complete Database Script for Multi-Product Suite & Dynamic Menus
-- Products:    1. CRM Suit (Code: CRM)
--              2. Prowess HRMS Connect (Code: HRMS)
--              3. Prowess ERP Connect (Code: ERP)
--              4. Prowess Ticket (Code: TICKET)
-- Execution:   Run in SSMS against your database (TESTPayroll_CRM)
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- 1. Create [dbo].[Product_Master] Table
-- -------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Product_Master]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Product_Master](
        [Product_Id]   INT IDENTITY(1,1) NOT NULL,
        [Product_Code] NVARCHAR(50) NOT NULL,
        [Product_Name] NVARCHAR(100) NOT NULL,
        [Description]  NVARCHAR(255) NULL,
        [Icon_Name]    NVARCHAR(50) NOT NULL DEFAULT ('MdLayers'),
        [Theme_Color]  NVARCHAR(20) NOT NULL DEFAULT ('#2563eb'),
        [Sort_Order]   INT NOT NULL DEFAULT (0),
        [Is_Active]    BIT NOT NULL DEFAULT (1),
        [Created_Date] DATETIME NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_Product_Master] PRIMARY KEY CLUSTERED ([Product_Id] ASC),
        CONSTRAINT [UQ_Product_Master_Code] UNIQUE ([Product_Code])
    );
    PRINT 'Created table [dbo].[Product_Master].';
END
ELSE
BEGIN
    PRINT 'Table [dbo].[Product_Master] already exists.';
END
GO

-- -------------------------------------------------------------------------------------
-- 2. Seed / Upsert the 4 Official Product Suites
-- -------------------------------------------------------------------------------------
MERGE INTO [dbo].[Product_Master] AS target
USING (VALUES
    ('ADMIN',  'Admin Suit',            'Central Administration, Client Management & Licensing', 'MdAdminPanelSettings', '#2563eb', 1, 1),
    ('HRMS',   'Prowess HRMS Connect',  'Human Resource Management, Workforce Ops & Payroll Processing', 'MdPeople',             '#059669', 2, 1),
    ('ERP',    'Prowess ERP Connect',   'Enterprise Resource Planning, Accounts & Business Operations',  'MdPayments',           '#7c3aed', 3, 1),
    ('TICKET', 'Prowess Ticket',        'Helpdesk, Ticketing & Support Management',           'MdConfirmationNumber', '#d97706', 4, 1)
) AS source ([Product_Code], [Product_Name], [Description], [Icon_Name], [Theme_Color], [Sort_Order], [Is_Active])
ON target.[Product_Code] = source.[Product_Code]
WHEN MATCHED THEN
    UPDATE SET 
        target.[Product_Name] = source.[Product_Name],
        target.[Description]  = source.[Description],
        target.[Icon_Name]    = source.[Icon_Name],
        target.[Theme_Color]  = source.[Theme_Color],
        target.[Sort_Order]   = source.[Sort_Order],
        target.[Is_Active]    = source.[Is_Active]
WHEN NOT MATCHED THEN
    INSERT ([Product_Code], [Product_Name], [Description], [Icon_Name], [Theme_Color], [Sort_Order], [Is_Active])
    VALUES (source.[Product_Code], source.[Product_Name], source.[Description], source.[Icon_Name], source.[Theme_Color], source.[Sort_Order], source.[Is_Active]);

PRINT 'Seeded official product suites into [dbo].[Product_Master].';
GO

-- -------------------------------------------------------------------------------------
-- 3. Create [dbo].[User_Products] Mapping Table
-- -------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[User_Products]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[User_Products](
        [User_Id]       INT NOT NULL,
        [Product_Id]    INT NOT NULL,
        [Is_Default]    BIT NOT NULL DEFAULT (0),
        [Assigned_Date] DATETIME NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_User_Products] PRIMARY KEY CLUSTERED ([User_Id] ASC, [Product_Id] ASC)
    );

    ALTER TABLE [dbo].[User_Products] WITH CHECK ADD CONSTRAINT [FK_User_Products_Product_Master] 
    FOREIGN KEY([Product_Id]) REFERENCES [dbo].[Product_Master] ([Product_Id]) ON DELETE CASCADE;

    PRINT 'Created table [dbo].[User_Products].';
END
ELSE
BEGIN
    PRINT 'Table [dbo].[User_Products] already exists.';
END
GO

-- Sync default product access for existing users
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserMaster]') AND type in (N'U'))
BEGIN
    INSERT INTO [dbo].[User_Products] (User_Id, Product_Id, Is_Default)
    SELECT u.UserId, p.Product_Id, CASE WHEN p.Product_Code = 'CRM' THEN 1 ELSE 0 END
    FROM dbo.UserMaster u
    CROSS JOIN dbo.Product_Master p
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.User_Products up 
        WHERE up.User_Id = u.UserId AND up.Product_Id = p.Product_Id
    );
    PRINT 'Synced default product access for existing users across all suites.';
END
GO

-- -------------------------------------------------------------------------------------
-- 4. Add Columns [Product_Code] and [Is_Common] to [dbo].[MenuMaster]
-- -------------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MenuMaster]') AND type in (N'U'))
BEGIN
    IF COL_LENGTH('dbo.MenuMaster', 'Product_Code') IS NULL
    BEGIN
        ALTER TABLE dbo.MenuMaster ADD Product_Code NVARCHAR(50) NULL;
        PRINT 'Added column [Product_Code] to [dbo].[MenuMaster].';
    END

    IF COL_LENGTH('dbo.MenuMaster', 'Is_Common') IS NULL
    BEGIN
        ALTER TABLE dbo.MenuMaster ADD Is_Common BIT NOT NULL CONSTRAINT DF_MenuMaster_Is_Common DEFAULT (0);
        PRINT 'Added column [Is_Common] to [dbo].[MenuMaster].';
    END
END
GO

-- -------------------------------------------------------------------------------------
-- 5. Seed HRMS Connect Specific Menus If Not Yet Present
-- -------------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MenuMaster]') AND type in (N'U'))
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.MenuMaster WHERE MenuPath = '/client-modules')
    BEGIN
        INSERT INTO dbo.MenuMaster (MenuName, MenuLabel, MenuPath, MenuIcon, ParentMenuId, SortOrder, AllowedRoles, IsActive, CreatedAt, Product_Code, Is_Common)
        VALUES ('client-modules', 'HRMS Modules', '/client-modules', 'MdLayers', NULL, 8, 'Admin,Manager', 1, GETDATE(), 'HRMS', 0);
        PRINT 'Added /client-modules menu to [dbo].[MenuMaster].';
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.MenuMaster WHERE MenuPath = '/client-es-modules')
    BEGIN
        INSERT INTO dbo.MenuMaster (MenuName, MenuLabel, MenuPath, MenuIcon, ParentMenuId, SortOrder, AllowedRoles, IsActive, CreatedAt, Product_Code, Is_Common)
        VALUES ('client-es-modules', 'ESPortal Modules', '/client-es-modules', 'MdPhoneAndroid', NULL, 9, 'Admin,Manager', 1, GETDATE(), 'HRMS', 0);
        PRINT 'Added /client-es-modules menu to [dbo].[MenuMaster].';
    END
END
GO

-- -------------------------------------------------------------------------------------
-- 6. Tag Common vs Product-Specific Routes (Using Actual Schema Column MenuPath)
-- -------------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MenuMaster]') AND type in (N'U'))
BEGIN
    -- Common modules (accessible across ALL product suites)
    UPDATE dbo.MenuMaster 
    SET Is_Common = 1, Product_Code = NULL 
    WHERE MenuPath IN ('/dashboard', '/clients', '/users', '/settings');

    -- Prowess HRMS Connect specific
    UPDATE dbo.MenuMaster 
    SET Is_Common = 0, Product_Code = 'HRMS' 
    WHERE MenuPath IN ('/employees', '/client-modules', '/client-es-modules', '/attendance', '/shifts', '/leaves');

    -- Prowess ERP Connect specific
    UPDATE dbo.MenuMaster 
    SET Is_Common = 0, Product_Code = 'ERP' 
    WHERE MenuPath IN ('/payroll', '/reports', '/salary-slips');

    -- Prowess Ticket specific
    UPDATE dbo.MenuMaster 
    SET Is_Common = 0, Product_Code = 'TICKET' 
    WHERE MenuPath IN ('/tickets', '/support');

    PRINT 'Tagged Common and Product-specific routes in [dbo].[MenuMaster].';
END
GO

-- -------------------------------------------------------------------------------------
-- 7. Create / Alter Stored Procedure: [dbo].[sp_GetMenusByRoleAndProduct]
--    Maps database schema (MenuLabel, MenuPath, MenuIcon, AllowedRoles) to application DTO
-- -------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_GetMenusByRoleAndProduct]
    @RoleName NVARCHAR(50),
    @ProductCode NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        m.MenuId,
        m.MenuLabel AS Title,
        m.MenuPath AS Route,
        m.MenuIcon AS IconName,
        m.ParentMenuId AS ParentId,
        m.SortOrder,
        m.AllowedRoles AS Roles,
        m.IsActive,
        'Main' AS Category,
        m.Product_Code AS ProductCode,
        ISNULL(m.Is_Common, 0) AS IsCommon
    FROM dbo.MenuMaster m
    WHERE m.IsActive = 1
      AND (
          @RoleName IS NULL 
          OR @RoleName = 'Admin' 
          OR @RoleName = 'Administrator'
          OR EXISTS (
              SELECT 1 FROM STRING_SPLIT(ISNULL(m.AllowedRoles, ''), ',') r
              WHERE LTRIM(RTRIM(r.value)) = @RoleName
          )
      )
      AND (
          -- Common cross-product menus are always included
          ISNULL(m.Is_Common, 0) = 1 
          OR m.Product_Code IS NULL
          -- Suite-specific menus matching active product
          OR (@ProductCode IS NOT NULL AND (
                UPPER(LTRIM(RTRIM(m.Product_Code))) = UPPER(LTRIM(RTRIM(@ProductCode)))
                OR (UPPER(@ProductCode) = 'ADMIN' AND UPPER(m.Product_Code) = 'CRM')
                OR (UPPER(@ProductCode) = 'CRM' AND UPPER(m.Product_Code) = 'ADMIN')
                OR (UPPER(@ProductCode) = 'ERP' AND UPPER(m.Product_Code) = 'PAYROLL')
                OR (UPPER(@ProductCode) = 'PAYROLL' AND UPPER(m.Product_Code) = 'ERP')
                OR (UPPER(@ProductCode) = 'TICKET' AND UPPER(m.Product_Code) = 'ESPORTAL')
             ))
      )
    ORDER BY m.SortOrder ASC;
END;
GO

PRINT 'Created/Updated procedure [dbo].[sp_GetMenusByRoleAndProduct] successfully.';
GO
