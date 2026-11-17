-- 1. Create Module_Master Table
CREATE TABLE [dbo].[Module_Master](
	[Module_Id] [int] NOT NULL, -- Note: NOT identity, because we want fixed standard IDs across systems
	[Module_Name] [nvarchar](100) NOT NULL,
	[Short_Name] [nvarchar](20) NOT NULL,
	[Description] [nvarchar](500) NULL,
	[Is_Active] [char](1) NOT NULL DEFAULT ('Y'),
	CONSTRAINT [PK_Module_Master] PRIMARY KEY CLUSTERED 
	(
		[Module_Id] ASC
	)
) ON [PRIMARY]
GO

-- 2. Create Client_Modules Mapping Table
CREATE TABLE [dbo].[Client_Modules](
	[Client_Id] [int] NOT NULL,
	[Module_Id] [int] NOT NULL,
	CONSTRAINT [PK_Client_Modules] PRIMARY KEY CLUSTERED 
	(
		[Client_Id] ASC,
		[Module_Id] ASC
	)
) ON [PRIMARY]
GO

-- Add Foreign Key for Client_Modules -> Client_Master
ALTER TABLE [dbo].[Client_Modules]  WITH CHECK ADD  CONSTRAINT [FK_Client_Modules_Client_Master] FOREIGN KEY([Client_Id])
REFERENCES [dbo].[Client_Master] ([Client_Id])
ON DELETE CASCADE
GO

ALTER TABLE [dbo].[Client_Modules] CHECK CONSTRAINT [FK_Client_Modules_Client_Master]
GO

-- Add Foreign Key for Client_Modules -> Module_Master
ALTER TABLE [dbo].[Client_Modules]  WITH CHECK ADD  CONSTRAINT [FK_Client_Modules_Module_Master] FOREIGN KEY([Module_Id])
REFERENCES [dbo].[Module_Master] ([Module_Id])
ON DELETE CASCADE
GO

ALTER TABLE [dbo].[Client_Modules] CHECK CONSTRAINT [FK_Client_Modules_Module_Master]
GO

-- 3. Insert Seed Data for Available Modules
-- These IDs will become the universal standard.
INSERT INTO [dbo].[Module_Master] ([Module_Id], [Module_Name], [Short_Name], [Description], [Is_Active])
VALUES 
	(1, 'HRMS', 'HRMS', 'Human Resource Management System', 'Y'),
	(2, 'Payroll', 'PAY', 'Payroll Management', 'Y'),
	(3, 'Recruitment', 'REC', 'Recruitment Management', 'Y'),
	(4, 'Asset Management', 'AST', 'Asset Management System', 'Y'),
	(5, 'Task Management', 'TSK', 'Task Management System', 'Y'),
	(6, 'Admin', 'ADM', 'Administration Module', 'Y'),
	(7, 'Support Admin', 'SUP', 'Support Administration Module', 'Y')
GO
