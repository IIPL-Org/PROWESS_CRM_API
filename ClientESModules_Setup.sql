-- =====================================================================================
-- Script Name: ClientESModules_Setup.sql
-- Description: CRM Database Schema & Seed Data for ESPortal (Employee Portal) Modules
--              1. Creates [dbo].[ES_Module_Master]
--              2. Creates [dbo].[Client_ES_Modules] mapping table
--              3. Seeds the 11 ESPortal Modules
-- NOTE: Execute this script in your CRM Database via SSMS.
-- =====================================================================================

-- 1. Create ES_Module_Master Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ES_Module_Master]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ES_Module_Master](
        [Module_Id]    [int] NOT NULL,
        [Module_Name]  [nvarchar](100) NOT NULL,
        [Short_Name]   [nvarchar](20) NOT NULL,
        [Description]  [nvarchar](500) NULL,
        [Is_Active]    [char](1) NOT NULL DEFAULT ('Y'),
        [Created_Date] [datetime] NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_ES_Module_Master] PRIMARY KEY CLUSTERED 
        (
            [Module_Id] ASC
        )
    ) ON [PRIMARY];
    PRINT 'Table [dbo].[ES_Module_Master] created successfully.';
END
GO

-- 2. Create Client_ES_Modules Mapping Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Client_ES_Modules]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Client_ES_Modules](
        [Client_Id]    [int] NOT NULL,
        [Module_Id]    [int] NOT NULL,
        [Created_Date] [datetime] NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_Client_ES_Modules] PRIMARY KEY CLUSTERED 
        (
            [Client_Id] ASC,
            [Module_Id] ASC
        )
    ) ON [PRIMARY];
    PRINT 'Table [dbo].[Client_ES_Modules] created successfully.';
END
GO

-- Add Foreign Key for Client_ES_Modules -> Client_Master
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_Client_ES_Modules_Client_Master]'))
BEGIN
    ALTER TABLE [dbo].[Client_ES_Modules] WITH CHECK ADD CONSTRAINT [FK_Client_ES_Modules_Client_Master] FOREIGN KEY([Client_Id])
    REFERENCES [dbo].[Client_Master] ([Client_Id])
    ON DELETE CASCADE;

    ALTER TABLE [dbo].[Client_ES_Modules] CHECK CONSTRAINT [FK_Client_ES_Modules_Client_Master];
    PRINT 'Foreign key [FK_Client_ES_Modules_Client_Master] created successfully.';
END
GO

-- Add Foreign Key for Client_ES_Modules -> ES_Module_Master
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_Client_ES_Modules_ES_Module_Master]'))
BEGIN
    ALTER TABLE [dbo].[Client_ES_Modules] WITH CHECK ADD CONSTRAINT [FK_Client_ES_Modules_ES_Module_Master] FOREIGN KEY([Module_Id])
    REFERENCES [dbo].[ES_Module_Master] ([Module_Id])
    ON DELETE CASCADE;

    ALTER TABLE [dbo].[Client_ES_Modules] CHECK CONSTRAINT [FK_Client_ES_Modules_ES_Module_Master];
    PRINT 'Foreign key [FK_Client_ES_Modules_ES_Module_Master] created successfully.';
END
GO

-- 3. Upsert / Seed the 11 ESPortal Modules
MERGE INTO [dbo].[ES_Module_Master] AS target
USING (VALUES
    (1,  'Core Navigation',      'NAV',  'Home, Dashboard, My Work, Services, and Core Navigation',     'Y'),
    (2,  'Auth & Session',       'AUTH', 'Authentication, Biometric/PIN Lock, Sessions & Passwords',   'Y'),
    (3,  'Time & Attendance',    'ATT',  'Clock In/Out, Shifts, Regularization, and Timesheets',       'Y'),
    (4,  'Leave',                'LV',   'Leave Balances, Applications, Comp-off, WFH, & Sanctions',   'Y'),
    (5,  'Field Movement',       'FLD',  'Geo-Tracking, On-Duty (OD), Customer Visits, & Mileage',     'Y'),
    (6,  'Payroll & Money',      'PAY',  'Payslips, Salary Info, Expense Claims, Advances, & Loans',   'Y'),
    (7,  'Performance',          'PERF', 'Review Cycle, Self & Manager Reviews, Rating, & Goals',       'Y'),
    (8,  'Workplace',            'WRK',  'Announcements, Memos, Tasks, Documents, Org Chart, & Assets', 'Y'),
    (9,  'Manager',              'MGR',  'Manager Approvals, Team Attendance, Reports, & Escalations', 'Y'),
    (10, 'Lifecycle & Services', 'LCS',  'Probation, Lifecycle Milestones, Requests, & Feedback',      'Y'),
    (11, 'Platform',             'PLT',  'Offline Sync, Push Notifications, Feature Flags, & AI HR',   'Y')
) AS source ([Module_Id], [Module_Name], [Short_Name], [Description], [Is_Active])
ON target.[Module_Id] = source.[Module_Id]
WHEN MATCHED THEN
    UPDATE SET 
        target.[Module_Name] = source.[Module_Name],
        target.[Short_Name]  = source.[Short_Name],
        target.[Description] = source.[Description],
        target.[Is_Active]   = source.[Is_Active]
WHEN NOT MATCHED THEN
    INSERT ([Module_Id], [Module_Name], [Short_Name], [Description], [Is_Active], [Created_Date])
    VALUES (source.[Module_Id], source.[Module_Name], source.[Short_Name], source.[Description], source.[Is_Active], GETDATE());
GO

PRINT '[dbo].[ES_Module_Master] seeded with 11 ESPortal modules successfully.';
GO
