IF DB_ID(N'AnjalisKitchen') IS NULL
BEGIN
    CREATE DATABASE [AnjalisKitchen];
END;
GO

USE [AnjalisKitchen];
GO

IF OBJECT_ID(N'dbo.ItemMenus', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ItemMenus
    (
        Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_ItemMenus PRIMARY KEY,
        Name nvarchar(200) NOT NULL,
        ParentId int NULL,
        ControllerName nvarchar(200) NULL,
        ActionName nvarchar(200) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_ItemMenus_IsActive DEFAULT (1),
        CONSTRAINT FK_ItemMenus_Parent FOREIGN KEY (ParentId) REFERENCES dbo.ItemMenus (Id)
    );
END;
GO

IF OBJECT_ID(N'dbo.Category', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Category
    (
        Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Category PRIMARY KEY,
        Name nvarchar(200) NOT NULL,
        Img nvarchar(200) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Category_IsActive DEFAULT (1)
    );
END;
GO

IF OBJECT_ID(N'dbo.[Role]', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[Role]
    (
        Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Role PRIMARY KEY,
        RoleName nvarchar(200) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Role_IsActive DEFAULT (1)
    );
END;
GO

IF OBJECT_ID(N'dbo.AdminUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdminUsers
    (
        Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_AdminUsers PRIMARY KEY,
        Name nvarchar(200) NOT NULL,
        UserName nvarchar(200) NOT NULL,
        [Password] nvarchar(200) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_AdminUsers_IsActive DEFAULT (1),
        RoleId int NOT NULL,
        CONSTRAINT FK_AdminUsers_Role FOREIGN KEY (RoleId) REFERENCES dbo.[Role] (Id),
        CONSTRAINT UQ_AdminUsers_UserName UNIQUE (UserName)
    );
END;
GO

IF OBJECT_ID(N'dbo.RolePrivilege', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RolePrivilege
    (
        Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_RolePrivilege PRIMARY KEY,
        MenuId int NOT NULL,
        RoleId int NOT NULL,
        UserId int NOT NULL,
        [View] bit NOT NULL CONSTRAINT DF_RolePrivilege_View DEFAULT (0),
        [Insert] bit NOT NULL CONSTRAINT DF_RolePrivilege_Insert DEFAULT (0),
        [Update] bit NOT NULL CONSTRAINT DF_RolePrivilege_Update DEFAULT (0),
        [Delete] bit NOT NULL CONSTRAINT DF_RolePrivilege_Delete DEFAULT (0),
        CONSTRAINT FK_RolePrivilege_ItemMenus FOREIGN KEY (MenuId) REFERENCES dbo.ItemMenus (Id),
        CONSTRAINT FK_RolePrivilege_Role FOREIGN KEY (RoleId) REFERENCES dbo.[Role] (Id),
        CONSTRAINT FK_RolePrivilege_AdminUsers FOREIGN KEY (UserId) REFERENCES dbo.AdminUsers (Id),
        CONSTRAINT UQ_RolePrivilege_Menu_Role_User UNIQUE (MenuId, RoleId, UserId)
    );
END;
GO

IF EXISTS
(
    SELECT 1
    FROM sys.key_constraints
    WHERE name = N'UQ_RolePrivilege_Menu_Role'
      AND parent_object_id = OBJECT_ID(N'dbo.RolePrivilege')
)
BEGIN
    ALTER TABLE dbo.RolePrivilege DROP CONSTRAINT UQ_RolePrivilege_Menu_Role;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.key_constraints
    WHERE name = N'UQ_RolePrivilege_Menu_Role_User'
      AND parent_object_id = OBJECT_ID(N'dbo.RolePrivilege')
)
BEGIN
    ALTER TABLE dbo.RolePrivilege
        ADD CONSTRAINT UQ_RolePrivilege_Menu_Role_User UNIQUE (MenuId, RoleId, UserId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ItemMenus WHERE Name = N'Dashboard' AND ParentId IS NULL)
BEGIN
    INSERT INTO dbo.ItemMenus (Name, ParentId, ControllerName, ActionName, IsActive)
    VALUES (N'Dashboard', NULL, N'Home', N'Dashboard', 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.ItemMenus WHERE Name = N'Sales' AND ParentId IS NULL)
BEGIN
    INSERT INTO dbo.ItemMenus (Name, ParentId, ControllerName, ActionName, IsActive)
    VALUES (N'Sales', NULL, NULL, NULL, 1);
END;

DECLARE @SalesId int =
(
    SELECT TOP (1) Id
    FROM dbo.ItemMenus
    WHERE Name = N'Sales' AND ParentId IS NULL
    ORDER BY Id
);

UPDATE dbo.ItemMenus
SET ParentId = @SalesId
WHERE Name = N'Orders' AND ParentId IS NULL;

IF NOT EXISTS (SELECT 1 FROM dbo.ItemMenus WHERE Name = N'Orders' AND ParentId = @SalesId)
BEGIN
    INSERT INTO dbo.ItemMenus (Name, ParentId, ControllerName, ActionName, IsActive)
    VALUES (N'Orders', @SalesId, N'Order', N'Index', 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.ItemMenus WHERE Name = N'Catalog' AND ParentId IS NULL)
BEGIN
    INSERT INTO dbo.ItemMenus (Name, ParentId, ControllerName, ActionName, IsActive)
    VALUES (N'Catalog', NULL, NULL, NULL, 1);
END;

DECLARE @CatalogId int =
(
    SELECT TOP (1) Id
    FROM dbo.ItemMenus
    WHERE Name = N'Catalog' AND ParentId IS NULL
    ORDER BY Id
);

IF NOT EXISTS (SELECT 1 FROM dbo.ItemMenus WHERE Name = N'Category' AND ParentId = @CatalogId)
BEGIN
    INSERT INTO dbo.ItemMenus (Name, ParentId, ControllerName, ActionName, IsActive)
    VALUES (N'Category', @CatalogId, N'Catalog', N'categories', 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.ItemMenus WHERE Name = N'Dishes' AND ParentId = @CatalogId)
BEGIN
    INSERT INTO dbo.ItemMenus (Name, ParentId, ControllerName, ActionName, IsActive)
    VALUES (N'Dishes', @CatalogId, N'Catalog', N'dishes', 1);
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.[Role] WHERE RoleName = N'Admin')
BEGIN
    INSERT INTO dbo.[Role] (RoleName, IsActive)
    VALUES (N'Admin', 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.AdminUsers WHERE UserName = N'admin')
BEGIN
    INSERT INTO dbo.AdminUsers (Name, UserName, [Password], IsActive, RoleId)
    SELECT N'Admin', N'admin',
           N'AQAAAAEAAYagAAAAEGdTUzJA5AjfYaRhJ3/6Vy2mnkkjISh+eM3uU76Fnyu7yPjhGgMdy8CUeghoXUU9/w==',
           1, Id
    FROM dbo.[Role]
    WHERE RoleName = N'Admin';
END;

IF COL_LENGTH(N'dbo.RolePrivilege', N'UserId') IS NULL
BEGIN
    ALTER TABLE dbo.RolePrivilege ADD UserId int NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.AdminUsers WHERE Id = 1)
    THROW 50002, 'AdminUsers must contain user Id 1 before assigning RolePrivilege.UserId.', 1;
GO

UPDATE dbo.RolePrivilege
SET UserId = 1
WHERE UserId IS NULL;
GO

ALTER TABLE dbo.RolePrivilege ALTER COLUMN UserId int NOT NULL;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_RolePrivilege_AdminUsers'
      AND parent_object_id = OBJECT_ID(N'dbo.RolePrivilege')
)
BEGIN
    ALTER TABLE dbo.RolePrivilege
        ADD CONSTRAINT FK_RolePrivilege_AdminUsers
        FOREIGN KEY (UserId) REFERENCES dbo.AdminUsers (Id);
END;
GO

INSERT INTO dbo.RolePrivilege (MenuId, RoleId, UserId, [View], [Insert], [Update], [Delete])
SELECT menu.Id, role.Id, adminUser.Id, 1, 1, 1, 1
FROM dbo.ItemMenus AS menu
CROSS JOIN dbo.[Role] AS role
INNER JOIN dbo.AdminUsers AS adminUser ON adminUser.RoleId = role.Id
WHERE role.RoleName = N'Admin'
  AND adminUser.UserName = N'admin'
  AND menu.Name IN (N'Dashboard', N'Sales', N'Orders', N'Catalog', N'Category', N'Dishes')
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.RolePrivilege AS privilege
      WHERE privilege.MenuId = menu.Id
        AND privilege.RoleId = role.Id
        AND privilege.UserId = adminUser.Id
  );
GO
