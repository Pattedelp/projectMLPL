USE TorneoChapitas;
GO

-- Limpiar todo
DELETE FROM Partidos;
DELETE FROM Fechas;
DELETE FROM Equipos;
DELETE FROM Divisiones;
DBCC CHECKIDENT ('Divisiones', RESEED, 0);
DBCC CHECKIDENT ('Equipos',    RESEED, 0);
DBCC CHECKIDENT ('Fechas',     RESEED, 0);
DBCC CHECKIDENT ('Partidos',   RESEED, 0);
GO

-- Divisiones
INSERT INTO Divisiones (Nombre, Descripcion, Orden) VALUES
('Primera División', 'Torneo Chapitas - AAC', 1),
('Nacional B',       'Torneo Chapitas - AAC', 2);
GO

-- =============================================
-- EQUIPOS PRIMERA DIVISIÓN  (IDs 1-10)
-- =============================================
INSERT INTO Equipos (DivisionId, Nombre, ColorPrincipal, ColorSecundario) VALUES
(1, 'Pato',     '#FF6600', '#FFFFFF'),  -- 1  🇳🇱
(1, 'Ponti',    '#C60C30', '#FFFFFF'),  -- 2  🇩🇰
(1, 'Juani',    '#002B7F', '#FFFFFF'),  -- 3  🇨🇷
(1, 'Tiago RC', '#009246', '#FFFFFF'),  -- 4  🇮🇹
(1, 'Nahuel',   '#B22234', '#FFFFFF'),  -- 5  🇺🇲
(1, 'Nacho G',  '#239F40', '#FFFFFF'),  -- 6  🇮🇷
(1, 'Matic',    '#1C1C1C', '#FFFFFF'),  -- 7  🏴
(1, 'Lucas M',  '#1C1C1C', '#FFD700'),  -- 8  🇧🇪
(1, 'Tiago S',  '#005BBB', '#FFD700'),  -- 9  🇺🇦
(1, 'Enzo',     '#000000', '#DD0000'); -- 10  🇩🇪
GO

-- =============================================
-- EQUIPOS NACIONAL B  (IDs 11-19)
-- =============================================
INSERT INTO Equipos (DivisionId, Nombre, ColorPrincipal, ColorSecundario) VALUES
(2, 'Joan',    '#CD2E3A', '#FFFFFF'),  -- 11 🇰🇷
(2, 'Pocho',   '#FFD100', '#003893'),  -- 12 🇪🇨
(2, 'Jere',    '#009C3B', '#FFDF00'),  -- 13 🇧🇷
(2, 'Fede O',  '#C60B1E', '#F1BF00'),  -- 14 🇪🇸
(2, 'Tomás',   '#DA251D', '#FFFF00'),  -- 15 🇻🇳
(2, 'Carlos',  '#FCD116', '#003087'),  -- 16 🇨🇴
(2, 'Sebas C', '#74ACDF', '#FFFFFF'),  -- 17 🇦🇷
(2, 'Santino', '#FF0000', '#FFFFFF'),  -- 18 🇨🇭
(2, 'Lucas G', '#0038A8', '#FCD116'); -- 19 🇵🇭
GO

-- =============================================
-- FECHAS PRIMERA DIVISIÓN  (IDs 1-9)
-- =============================================
INSERT INTO Fechas (DivisionId, Numero, Nombre) VALUES
(1,1,'Fecha 1'),(1,2,'Fecha 2'),(1,3,'Fecha 3'),
(1,4,'Fecha 4'),(1,5,'Fecha 5'),(1,6,'Fecha 6'),
(1,7,'Fecha 7'),(1,8,'Fecha 8'),(1,9,'Fecha 9');

-- =============================================
-- FECHAS NACIONAL B  (IDs 10-18)
-- =============================================
INSERT INTO Fechas (DivisionId, Numero, Nombre) VALUES
(2,1,'Fecha 1'),(2,2,'Fecha 2'),(2,3,'Fecha 3'),
(2,4,'Fecha 4'),(2,5,'Fecha 5'),(2,6,'Fecha 6'),
(2,7,'Fecha 7'),(2,8,'Fecha 8'),(2,9,'Fecha 9');
GO

-- =============================================
-- FIXTURE PRIMERA DIVISIÓN
-- Pato=1 Ponti=2 Juani=3 TiagoRC=4 Nahuel=5
-- NachoG=6 Matic=7 LucasM=8 TiagoS=9 Enzo=10
-- =============================================

-- Fecha 1
INSERT INTO Partidos (FechaId,DivisionId,EquipoLocalId,EquipoVisitanteId,Jugado) VALUES
(1,1,1,9,0),   -- Pato vs Tiago S
(1,1,5,10,0),  -- Nahuel vs Enzo
(1,1,3,8,0),   -- Juani vs Lucas M
(1,1,6,2,0),   -- Nacho G vs Ponti
(1,1,4,7,0);   -- Tiago RC vs Matic

-- Fecha 2
INSERT INTO Partidos (FechaId,DivisionId,EquipoLocalId,EquipoVisitanteId,Jugado) VALUES
(2,1,1,8,0),   -- Pato vs Lucas M
(2,1,9,5,0),   -- Tiago S vs Nahuel
(2,1,10,3,0),  -- Enzo vs Juani
(2,1,2,4,0),   -- Ponti vs Tiago RC
(2,1,6,7,0);   -- Nacho G vs Matic

-- Fecha 3
INSERT INTO Partidos (FechaId,DivisionId,EquipoLocalId,EquipoVisitanteId,Jugado) VALUES
(3,1,1,2,0),   -- Pato vs Ponti
(3,1,3,4,0),   -- Juani vs Tiago RC
(3,1,5,6,0),   -- Nahuel vs Nacho G
(3,1,7,8,0),   -- Matic vs Lucas M
(3,1,9,10,0);  -- Tiago S vs Enzo

-- Fecha 4
INSERT INTO Partidos (FechaId,DivisionId,EquipoLocalId,EquipoVisitanteId,Jugado) VALUES
(4,1,1,3,0),   -- Pato vs Juani
(4,1,6,4,0),   -- Nacho G vs Tiago RC
(4,1,9,2,0),   -- Tiago S vs Ponti
(4,1,7,10,0),  -- Matic vs Enzo
(4,1,8,5,0);   -- Lucas M vs Nahuel

-- Fecha 5
INSERT INTO Partidos (FechaId,DivisionId,EquipoLocalId,EquipoVisitanteId,Jugado) VALUES
(5,1,1,4,0),   -- Pato vs Tiago RC
(5,1,10,2,0),  -- Enzo vs Ponti
(5,1,8,6,0),   -- Lucas M vs Nacho G
(5,1,9,3,0),   -- Tiago S vs Juani
(5,1,7,5,0);   -- Matic vs Nahuel

-- Fecha 6
INSERT INTO Partidos (FechaId,DivisionId,EquipoLocalId,EquipoVisitanteId,Jugado) VALUES
(6,1,1,6,0),   -- Pato vs Nacho G
(6,1,10,4,0),  -- Enzo vs Tiago RC
(6,1,8,2,0),   -- Lucas M vs Ponti
(6,1,9,7,0),   -- Tiago S vs Matic
(6,1,3,5,0);   -- Juani vs Nahuel

-- Fecha 7
INSERT INTO Partidos (FechaId,DivisionId,EquipoLocalId,EquipoVisitanteId,Jugado) VALUES
(7,1,1,7,0),   -- Pato vs Matic
(7,1,2,3,0),   -- Ponti vs Juani
(7,1,5,4,0),   -- Nahuel vs Tiago RC
(7,1,6,10,0),  -- Nacho G vs Enzo
(7,1,8,9,0);   -- Lucas M vs Tiago S

-- Fecha 8
INSERT INTO Partidos (FechaId,DivisionId,EquipoLocalId,EquipoVisitanteId,Jugado) VALUES
(8,1,1,10,0),  -- Pato vs Enzo
(8,1,8,4,0),   -- Lucas M vs Tiago RC
(8,1,9,6,0),   -- Tiago S vs Nacho G
(8,1,3,7,0),   -- Juani vs Matic
(8,1,2,5,0);   -- Ponti vs Nahuel

-- Fecha 9
INSERT INTO Partidos (FechaId,DivisionId,EquipoLocalId,EquipoVisitanteId,Jugado) VALUES
(9,1,1,5,0),   -- Pato vs Nahuel
(9,1,2,7,0),   -- Ponti vs Matic
(9,1,3,6,0),   -- Juani vs Nacho G
(9,1,4,9,0),   -- Tiago RC vs Tiago S
(9,1,8,10,0);  -- Lucas M vs Enzo

-- =============================================
-- FIXTURE NACIONAL B
-- Joan=11 Pocho=12 Jere=13 FedeO=14 Tomás=15
-- Carlos=16 SebasC=17 Santino=18 LucasG=19
-- =============================================

-- Fecha 1
INSERT INTO Partidos (FechaId,DivisionId,EquipoLocalId,EquipoVisitanteId,Jugado) VALUES
(10,2,11,12,0),  -- Joan vs Pocho
(10,2,13,14,0),  -- Jere vs Fede O
(10,2,15,16,0),  -- Tomás vs Carlos
(10,2,17,18,0);  -- Sebas C vs Santino

-- Fecha 2
INSERT INTO Partidos (FechaId,DivisionId,EquipoLocalId,EquipoVisitanteId,Jugado) VALUES
(11,2,12,19,0),  -- Pocho vs Lucas G
(11,2,11,16,0),  -- Joan vs Carlos
(11,2,13,18,0),  -- Jere vs Santino
(11,2,15,17,0);  -- Tomás vs Sebas C

-- Fecha 3
INSERT INTO Partidos (FechaId,DivisionId,EquipoLocalId,EquipoVisitanteId,Jugado) VALUES
(12,2,14,19,0),  -- Fede O vs Lucas G
(12,2,12,16,0),  -- Pocho vs Carlos
(12,2,11,17,0),  -- Joan vs Sebas C
(12,2,13,15,0);  -- Jere vs Tomás

-- Fecha 4
INSERT INTO Partidos (FechaId,DivisionId,EquipoLocalId,EquipoVisitanteId,Jugado) VALUES
(13,2,16,19,0),  -- Carlos vs Lucas G
(13,2,14,18,0),  -- Fede O vs Santino
(13,2,12,17,0),  -- Pocho vs Sebas C
(13,2,11,13,0);  -- Joan vs Jere

-- Fecha 5
INSERT INTO Partidos (FechaId,DivisionId,EquipoLocalId,EquipoVisitanteId,Jugado) VALUES
(14,2,18,19,0),  -- Santino vs Lucas G
(14,2,16,17,0),  -- Carlos vs Sebas C
(14,2,14,15,0),  -- Fede O vs Tomás
(14,2,12,13,0);  -- Pocho vs Jere

-- Fecha 6
INSERT INTO Partidos (FechaId,DivisionId,EquipoLocalId,EquipoVisitanteId,Jugado) VALUES
(15,2,17,19,0),  -- Sebas C vs Lucas G
(15,2,18,15,0),  -- Santino vs Tomás
(15,2,16,13,0),  -- Carlos vs Jere
(15,2,14,11,0);  -- Fede O vs Joan

-- Fecha 7
INSERT INTO Partidos (FechaId,DivisionId,EquipoLocalId,EquipoVisitanteId,Jugado) VALUES
(16,2,15,19,0),  -- Tomás vs Lucas G
(16,2,17,13,0),  -- Sebas C vs Jere
(16,2,18,11,0),  -- Santino vs Joan
(16,2,14,12,0);  -- Fede O vs Pocho

-- Fecha 8
INSERT INTO Partidos (FechaId,DivisionId,EquipoLocalId,EquipoVisitanteId,Jugado) VALUES
(17,2,13,19,0),  -- Jere vs Lucas G
(17,2,15,11,0),  -- Tomás vs Joan
(17,2,18,12,0),  -- Santino vs Pocho
(17,2,16,14,0);  -- Carlos vs Fede O

-- Fecha 9
INSERT INTO Partidos (FechaId,DivisionId,EquipoLocalId,EquipoVisitanteId,Jugado) VALUES
(18,2,11,19,0),  -- Joan vs Lucas G
(18,2,15,12,0),  -- Tomás vs Pocho
(18,2,17,14,0),  -- Sebas C vs Fede O
(18,2,18,16,0);  -- Santino vs Carlos

PRINT 'Torneo Chapitas - Fixture completo cargado!';
GO
