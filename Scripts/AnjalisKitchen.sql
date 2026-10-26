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
        Details nvarchar(1000) NOT NULL CONSTRAINT DF_Category_Details DEFAULT (N''),
        Img nvarchar(200) NULL,
        ParentId int NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Category_IsActive DEFAULT (1)
    );
END;
GO

IF COL_LENGTH(N'dbo.Category', N'Details') IS NULL
BEGIN
    ALTER TABLE dbo.Category
        ADD Details nvarchar(1000) NOT NULL CONSTRAINT DF_Category_Details DEFAULT (N'');
END;
GO

IF OBJECT_ID(N'dbo.Dish', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Dish
    (
        Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Dish PRIMARY KEY,
        Name nvarchar(200) NOT NULL,
        Price decimal(10, 2) NOT NULL,
        OldPrice decimal(10, 2) NULL,
        Description nvarchar(2000) NOT NULL CONSTRAINT DF_Dish_Description DEFAULT (N''),
        CategoryId int NOT NULL,
        Img nvarchar(500) NULL,
        IsNew bit NOT NULL CONSTRAINT DF_Dish_IsNew DEFAULT (0),
        IsActive bit NOT NULL CONSTRAINT DF_Dish_IsActive DEFAULT (1),
        CONSTRAINT FK_Dish_Category FOREIGN KEY (CategoryId) REFERENCES dbo.Category (Id)
    );
END;
GO

IF COL_LENGTH(N'dbo.Dish', N'Img') IS NULL
BEGIN
    ALTER TABLE dbo.Dish ADD Img nvarchar(500) NULL;
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
        FirstName nvarchar(100) NOT NULL CONSTRAINT DF_AdminUsers_FirstName DEFAULT (N''),
        LastName nvarchar(100) NOT NULL CONSTRAINT DF_AdminUsers_LastName DEFAULT (N''),
        Email nvarchar(320) NOT NULL CONSTRAINT DF_AdminUsers_Email DEFAULT (N''),
        UserName nvarchar(200) NOT NULL,
        [Password] nvarchar(200) NOT NULL,
        Phone nvarchar(30) NULL,
        DOB date NULL,
        City nvarchar(100) NULL,
        State nvarchar(100) NULL,
        Country nvarchar(100) NULL,
        IsSystemUser bit NOT NULL CONSTRAINT DF_AdminUsers_IsSystemUser DEFAULT (0),
        IsActive bit NOT NULL CONSTRAINT DF_AdminUsers_IsActive DEFAULT (1),
        RoleId int NOT NULL,
        CONSTRAINT FK_AdminUsers_Role FOREIGN KEY (RoleId) REFERENCES dbo.[Role] (Id),
        CONSTRAINT UQ_AdminUsers_UserName UNIQUE (UserName),
        CONSTRAINT UQ_AdminUsers_Email UNIQUE (Email)
    );
END;
GO

IF COL_LENGTH(N'dbo.AdminUsers', N'FirstName') IS NULL
    ALTER TABLE dbo.AdminUsers ADD FirstName nvarchar(100) NOT NULL CONSTRAINT DF_AdminUsers_FirstName DEFAULT (N'');
IF COL_LENGTH(N'dbo.AdminUsers', N'LastName') IS NULL
    ALTER TABLE dbo.AdminUsers ADD LastName nvarchar(100) NOT NULL CONSTRAINT DF_AdminUsers_LastName DEFAULT (N'');
IF COL_LENGTH(N'dbo.AdminUsers', N'Email') IS NULL
    ALTER TABLE dbo.AdminUsers ADD Email nvarchar(320) NOT NULL CONSTRAINT DF_AdminUsers_Email DEFAULT (N'');
IF COL_LENGTH(N'dbo.AdminUsers', N'Phone') IS NULL
    ALTER TABLE dbo.AdminUsers ADD Phone nvarchar(30) NULL;
IF COL_LENGTH(N'dbo.AdminUsers', N'DOB') IS NULL
    ALTER TABLE dbo.AdminUsers ADD DOB date NULL;
IF COL_LENGTH(N'dbo.AdminUsers', N'City') IS NULL
    ALTER TABLE dbo.AdminUsers ADD City nvarchar(100) NULL;
IF COL_LENGTH(N'dbo.AdminUsers', N'State') IS NULL
    ALTER TABLE dbo.AdminUsers ADD State nvarchar(100) NULL;
IF COL_LENGTH(N'dbo.AdminUsers', N'Country') IS NULL
    ALTER TABLE dbo.AdminUsers ADD Country nvarchar(100) NULL;
IF COL_LENGTH(N'dbo.AdminUsers', N'IsSystemUser') IS NULL
    ALTER TABLE dbo.AdminUsers ADD IsSystemUser bit NOT NULL CONSTRAINT DF_AdminUsers_IsSystemUser DEFAULT (0);
GO

IF OBJECT_ID(N'dbo.RolePrivilege', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RolePrivilege
    (
        Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_RolePrivilege PRIMARY KEY,
        MenuId int NOT NULL,
        RoleId int NOT NULL,
        [View] bit NOT NULL CONSTRAINT DF_RolePrivilege_View DEFAULT (0),
        [Add] bit NOT NULL CONSTRAINT DF_RolePrivilege_Add DEFAULT (0),
        [Edit] bit NOT NULL CONSTRAINT DF_RolePrivilege_Edit DEFAULT (0),
        [Insert] bit NOT NULL CONSTRAINT DF_RolePrivilege_Insert DEFAULT (0),
        [Update] bit NOT NULL CONSTRAINT DF_RolePrivilege_Update DEFAULT (0),
        [Delete] bit NOT NULL CONSTRAINT DF_RolePrivilege_Delete DEFAULT (0),
        CONSTRAINT FK_RolePrivilege_ItemMenus FOREIGN KEY (MenuId) REFERENCES dbo.ItemMenus (Id),
        CONSTRAINT FK_RolePrivilege_Role FOREIGN KEY (RoleId) REFERENCES dbo.[Role] (Id),
        CONSTRAINT UQ_RolePrivilege_Menu_Role UNIQUE (MenuId, RoleId)
    );
END;
GO

IF COL_LENGTH(N'dbo.RolePrivilege', N'Add') IS NULL
    ALTER TABLE dbo.RolePrivilege ADD [Add] bit NOT NULL CONSTRAINT DF_RolePrivilege_Add DEFAULT (0);
IF COL_LENGTH(N'dbo.RolePrivilege', N'Edit') IS NULL
    ALTER TABLE dbo.RolePrivilege ADD [Edit] bit NOT NULL CONSTRAINT DF_RolePrivilege_Edit DEFAULT (0);
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

IF NOT EXISTS (SELECT 1 FROM dbo.ItemMenus WHERE Name = N'Customers' AND ParentId IS NULL)
BEGIN
    INSERT INTO dbo.ItemMenus (Name, ParentId, ControllerName, ActionName, IsActive)
    VALUES (N'Customers', NULL, NULL, NULL, 1);
END;

DECLARE @CustomersId int =
(
    SELECT TOP (1) Id
    FROM dbo.ItemMenus
    WHERE Name = N'Customers' AND ParentId IS NULL
    ORDER BY Id
);

IF NOT EXISTS (SELECT 1 FROM dbo.ItemMenus WHERE Name = N'Users' AND ParentId = @CustomersId)
BEGIN
    INSERT INTO dbo.ItemMenus (Name, ParentId, ControllerName, ActionName, IsActive)
    VALUES (N'Users', @CustomersId, N'Customers', N'Users', 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.ItemMenus WHERE Name = N'Roles' AND ParentId = @CustomersId)
BEGIN
    INSERT INTO dbo.ItemMenus (Name, ParentId, ControllerName, ActionName, IsActive)
    VALUES (N'Roles', @CustomersId, N'Customers', N'Roles', 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.ItemMenus WHERE Name = N'Role Privileges' AND ParentId = @CustomersId)
BEGIN
    INSERT INTO dbo.ItemMenus (Name, ParentId, ControllerName, ActionName, IsActive)
    VALUES (N'Role Privileges', @CustomersId, N'Customers', N'RolePrivileges', 1);
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.[Role] WHERE RoleName = N'Admin')
BEGIN
    INSERT INTO dbo.[Role] (RoleName, IsActive)
    VALUES (N'Admin', 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.AdminUsers WHERE UserName = N'admin')
BEGIN
    INSERT INTO dbo.AdminUsers (Name, FirstName, LastName, Email, UserName, [Password], IsActive, IsSystemUser, RoleId)
    SELECT N'Admin', N'Admin', N'', N'admin@anjaliskitchen.local', N'admin',
           N'AQAAAAEAAYagAAAAEGdTUzJA5AjfYaRhJ3/6Vy2mnkkjISh+eM3uU76Fnyu7yPjhGgMdy8CUeghoXUU9/w==',
           1, 1, Id
    FROM dbo.[Role]
    WHERE RoleName = N'Admin';
END;
GO

IF COL_LENGTH(N'dbo.RolePrivilege', N'UserId') IS NOT NULL
BEGIN
    SELECT MenuId, RoleId,
           MAX(CONVERT(int, [View])) AS [View],
           MAX(CONVERT(int, [Insert])) AS [Insert],
           MAX(CONVERT(int, [Update])) AS [Update],
           MAX(CONVERT(int, [Delete])) AS [Delete]
    INTO #RolePrivilegeByRole
    FROM dbo.RolePrivilege
    GROUP BY MenuId, RoleId;

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_RolePrivilege_AdminUsers' AND parent_object_id = OBJECT_ID(N'dbo.RolePrivilege'))
        ALTER TABLE dbo.RolePrivilege DROP CONSTRAINT FK_RolePrivilege_AdminUsers;
    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_RolePrivilege_Menu_Role_User' AND parent_object_id = OBJECT_ID(N'dbo.RolePrivilege'))
        ALTER TABLE dbo.RolePrivilege DROP CONSTRAINT UQ_RolePrivilege_Menu_Role_User;
    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_RolePrivilege_Menu_Role' AND parent_object_id = OBJECT_ID(N'dbo.RolePrivilege'))
        ALTER TABLE dbo.RolePrivilege DROP CONSTRAINT UQ_RolePrivilege_Menu_Role;

    ALTER TABLE dbo.RolePrivilege DROP COLUMN UserId;
    DELETE FROM dbo.RolePrivilege;

    INSERT INTO dbo.RolePrivilege (MenuId, RoleId, [View], [Add], [Edit], [Insert], [Update], [Delete])
    SELECT MenuId, RoleId, [View], [Insert], [Update], [Insert], [Update], [Delete]
    FROM #RolePrivilegeByRole;

    DROP TABLE #RolePrivilegeByRole;
    ALTER TABLE dbo.RolePrivilege
        ADD CONSTRAINT UQ_RolePrivilege_Menu_Role UNIQUE (MenuId, RoleId);
END;
GO

UPDATE dbo.AdminUsers
SET FirstName = CASE WHEN FirstName = N'' THEN Name ELSE FirstName END,
    Email = CASE WHEN Email = N'' THEN CONCAT(UserName, N'@local.invalid') ELSE Email END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_AdminUsers_Email' AND parent_object_id = OBJECT_ID(N'dbo.AdminUsers'))
    ALTER TABLE dbo.AdminUsers ADD CONSTRAINT UQ_AdminUsers_Email UNIQUE (Email);
GO

IF EXISTS (SELECT 1 FROM dbo.AdminUsers WHERE UserName = N'admin')
BEGIN
    INSERT INTO dbo.RolePrivilege (MenuId, RoleId, [View], [Add], [Edit], [Insert], [Update], [Delete])
    SELECT menu.Id, adminUser.RoleId, 1, 1, 1, 1, 1, 1
    FROM dbo.ItemMenus AS menu
    INNER JOIN dbo.AdminUsers AS adminUser ON adminUser.UserName = N'admin'
    WHERE menu.Name IN
        (N'Dashboard', N'Sales', N'Orders', N'Catalog', N'Category', N'Dishes',
         N'Customers', N'Users', N'Roles', N'Role Privileges')
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.RolePrivilege AS privilege
          WHERE privilege.MenuId = menu.Id AND privilege.RoleId = adminUser.RoleId
      );
END;
GO
