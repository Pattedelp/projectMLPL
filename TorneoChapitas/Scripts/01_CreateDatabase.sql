-- =============================================
-- TORNEO AMIGOS - Script de Creacion de Base de Datos
-- SQL Server
-- =============================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'TorneoAmigos')
BEGIN
    CREATE DATABASE TorneoAmigos;
END
GO

USE TorneoAmigos;
GO

-- =============================================
-- TABLA: Divisiones
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Divisiones]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Divisiones] (
        [Id]          INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Nombre]      NVARCHAR(100) NOT NULL,
        [Descripcion] NVARCHAR(500) NULL,
        [Orden]       INT NOT NULL DEFAULT 1,
        [Activa]      BIT NOT NULL DEFAULT 1,
        [FechaCreacion] DATETIME NOT NULL DEFAULT GETDATE()
    );
END
GO

-- =============================================
-- TABLA: Equipos
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Equipos]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Equipos] (
        [Id]          INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [DivisionId]  INT NOT NULL,
        [Nombre]      NVARCHAR(100) NOT NULL,
        [Escudo]      NVARCHAR(200) NULL,
        [ColorPrincipal] NVARCHAR(20) NULL DEFAULT '#003366',
        [ColorSecundario] NVARCHAR(20) NULL DEFAULT '#FFD700',
        [Activo]      BIT NOT NULL DEFAULT 1,
        [FechaCreacion] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [FK_Equipos_Divisiones] FOREIGN KEY ([DivisionId]) REFERENCES [dbo].[Divisiones]([Id])
    );
END
GO

-- =============================================
-- TABLA: Fechas (Jornadas)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Fechas]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Fechas] (
        [Id]          INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [DivisionId]  INT NOT NULL,
        [Numero]      INT NOT NULL,
        [Nombre]      NVARCHAR(50) NOT NULL,
        [FechaInicio] DATE NULL,
        [FechaFin]    DATE NULL,
        [Activa]      BIT NOT NULL DEFAULT 1,
        CONSTRAINT [FK_Fechas_Divisiones] FOREIGN KEY ([DivisionId]) REFERENCES [dbo].[Divisiones]([Id])
    );
END
GO

-- =============================================
-- TABLA: Partidos
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Partidos]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Partidos] (
        [Id]            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [FechaId]       INT NOT NULL,
        [DivisionId]    INT NOT NULL,
        [EquipoLocalId] INT NOT NULL,
        [EquipoVisitanteId] INT NOT NULL,
        [GolesLocal]    INT NULL,
        [GolesVisitante] INT NULL,
        [Jugado]        BIT NOT NULL DEFAULT 0,
        [FechaPartido]  DATETIME NULL,
        [Lugar]         NVARCHAR(200) NULL,
        [Observaciones] NVARCHAR(500) NULL,
        [FechaCreacion] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [FK_Partidos_Fechas] FOREIGN KEY ([FechaId]) REFERENCES [dbo].[Fechas]([Id]),
        CONSTRAINT [FK_Partidos_Divisiones] FOREIGN KEY ([DivisionId]) REFERENCES [dbo].[Divisiones]([Id]),
        CONSTRAINT [FK_Partidos_EquipoLocal] FOREIGN KEY ([EquipoLocalId]) REFERENCES [dbo].[Equipos]([Id]),
        CONSTRAINT [FK_Partidos_EquipoVisitante] FOREIGN KEY ([EquipoVisitanteId]) REFERENCES [dbo].[Equipos]([Id])
    );
END
GO

-- =============================================
-- TABLA: Goleadores (para futuras mejoras)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Jugadores]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Jugadores] (
        [Id]         INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [EquipoId]   INT NOT NULL,
        [Nombre]     NVARCHAR(100) NOT NULL,
        [Apodo]      NVARCHAR(50) NULL,
        [Numero]     INT NULL,
        [Posicion]   NVARCHAR(50) NULL,
        [Activo]     BIT NOT NULL DEFAULT 1,
        CONSTRAINT [FK_Jugadores_Equipos] FOREIGN KEY ([EquipoId]) REFERENCES [dbo].[Equipos]([Id])
    );
END
GO

-- =============================================
-- DATOS DE EJEMPLO
-- =============================================

-- Divisiones
IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[Divisiones])
BEGIN
    INSERT INTO [dbo].[Divisiones] ([Nombre], [Descripcion], [Orden]) VALUES
    ('Primera División', 'La máxima categoría del torneo', 1),
    ('Nacional B', 'Segunda categoría del torneo', 2);
END
GO

-- Equipos Primera División
IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[Equipos])
BEGIN
    INSERT INTO [dbo].[Equipos] ([DivisionId], [Nombre], [ColorPrincipal], [ColorSecundario]) VALUES
    (1, 'Los Cañones',   '#003366', '#FFD700'),
    (1, 'Estrellas FC',  '#8B0000', '#FFFFFF'),
    (1, 'El Clásico',    '#006400', '#FFFFFF'),
    (1, 'Trueno Azul',   '#00008B', '#87CEEB'),
    (1, 'Los Pibes',     '#4B0082', '#FFD700'),
    (1, 'Gambeta FC',    '#B8860B', '#000000'),

    (2, 'Potrero Kings', '#FF4500', '#FFFFFF'),
    (2, 'Los Volantes',  '#008080', '#FFFFFF'),
    (2, 'Barrio Norte',  '#2F4F4F', '#FFD700'),
    (2, 'Villa Sur',     '#800000', '#FFFFFF'),
    (2, 'Los Cracks',    '#191970', '#C0C0C0'),
    (2, 'Ferro Amigos',  '#006400', '#FFD700');
END
GO

-- Fechas Primera División
IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[Fechas])
BEGIN
    INSERT INTO [dbo].[Fechas] ([DivisionId], [Numero], [Nombre], [FechaInicio]) VALUES
    (1, 1, 'Fecha 1', '2025-03-01'),
    (1, 2, 'Fecha 2', '2025-03-08'),
    (1, 3, 'Fecha 3', '2025-03-15'),
    (1, 4, 'Fecha 4', '2025-03-22'),
    (1, 5, 'Fecha 5', '2025-03-29'),
    (2, 1, 'Fecha 1', '2025-03-01'),
    (2, 2, 'Fecha 2', '2025-03-08'),
    (2, 3, 'Fecha 3', '2025-03-15'),
    (2, 4, 'Fecha 4', '2025-03-22'),
    (2, 5, 'Fecha 5', '2025-03-29');
END
GO

-- Partidos de ejemplo (Primera División)
IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[Partidos])
BEGIN
    -- Fecha 1
    INSERT INTO [dbo].[Partidos] ([FechaId],[DivisionId],[EquipoLocalId],[EquipoVisitanteId],[GolesLocal],[GolesVisitante],[Jugado]) VALUES
    (1,1,1,2,3,1,1),
    (1,1,3,4,2,2,1),
    (1,1,5,6,1,0,1),
    -- Fecha 2
    (2,1,1,3,2,0,1),
    (2,1,2,5,3,1,1),
    (2,1,4,6,2,1,1),
    -- Fecha 3 (pendiente)
    (3,1,1,4,NULL,NULL,0),
    (3,1,2,6,NULL,NULL,0),
    (3,1,3,5,NULL,NULL,0);

    -- Nacional B
    INSERT INTO [dbo].[Partidos] ([FechaId],[DivisionId],[EquipoLocalId],[EquipoVisitanteId],[GolesLocal],[GolesVisitante],[Jugado]) VALUES
    (6,2,7,8,2,1,1),
    (6,2,9,10,1,1,1),
    (6,2,11,12,0,2,1),
    (7,2,7,9,3,0,1),
    (7,2,8,11,2,0,1),
    (8,2,7,10,NULL,NULL,0),
    (8,2,9,12,NULL,NULL,0);
END
GO

PRINT 'Base de datos TorneoAmigos creada correctamente.';
GO
