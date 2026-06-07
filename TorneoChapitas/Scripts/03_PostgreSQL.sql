-- =============================================
-- TORNEO CHAPITAS - PostgreSQL
-- Ejecutar en tu base de datos PostgreSQL
-- (Railway, Supabase, o local)
-- =============================================

-- Tablas
CREATE TABLE IF NOT EXISTS divisiones (
    id          SERIAL PRIMARY KEY,
    nombre      VARCHAR(100) NOT NULL,
    descripcion VARCHAR(500),
    orden       INT NOT NULL DEFAULT 1,
    activa      BOOLEAN NOT NULL DEFAULT true,
    fechacreacion TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS equipos (
    id              SERIAL PRIMARY KEY,
    divisionid      INT NOT NULL REFERENCES divisiones(id),
    nombre          VARCHAR(100) NOT NULL,
    escudo          VARCHAR(200),
    colorprincipal  VARCHAR(20) DEFAULT '#003366',
    colorsecundario VARCHAR(20) DEFAULT '#FFD700',
    activo          BOOLEAN NOT NULL DEFAULT true,
    fechacreacion   TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS fechas (
    id          SERIAL PRIMARY KEY,
    divisionid  INT NOT NULL REFERENCES divisiones(id),
    numero      INT NOT NULL,
    nombre      VARCHAR(50) NOT NULL,
    fechainicio DATE,
    fechafin    DATE,
    activa      BOOLEAN NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS partidos (
    id                  SERIAL PRIMARY KEY,
    fechaid             INT NOT NULL REFERENCES fechas(id),
    divisionid          INT NOT NULL REFERENCES divisiones(id),
    equipolocalid       INT NOT NULL REFERENCES equipos(id),
    equipovisitanteid   INT NOT NULL REFERENCES equipos(id),
    goleslocal          INT,
    golesvisitante      INT,
    jugado              BOOLEAN NOT NULL DEFAULT false,
    fechapartido        TIMESTAMP,
    lugar               VARCHAR(200),
    observaciones       VARCHAR(500),
    fechacreacion       TIMESTAMP NOT NULL DEFAULT NOW()
);

-- ── DATOS ──────────────────────────────────────────

-- Divisiones
INSERT INTO divisiones (nombre, descripcion, orden) VALUES
('Primera División', 'Torneo Chapitas - AAC', 1),
('Nacional B',       'Torneo Chapitas - AAC', 2);

-- Equipos Primera División (IDs 1-10)
INSERT INTO equipos (divisionid, nombre, colorprincipal, colorsecundario) VALUES
(1, 'Pato',     '#FF6600', '#FFFFFF'),
(1, 'Ponti',    '#C60C30', '#FFFFFF'),
(1, 'Juani',    '#002B7F', '#FFFFFF'),
(1, 'Tiago RC', '#009246', '#FFFFFF'),
(1, 'Nahuel',   '#B22234', '#FFFFFF'),
(1, 'Nacho G',  '#239F40', '#FFFFFF'),
(1, 'Matic',    '#1C1C1C', '#FFFFFF'),
(1, 'Lucas M',  '#1C1C1C', '#FFD700'),
(1, 'Tiago S',  '#005BBB', '#FFD700'),
(1, 'Enzo',     '#000000', '#DD0000');

-- Equipos Nacional B (IDs 11-19)
INSERT INTO equipos (divisionid, nombre, colorprincipal, colorsecundario) VALUES
(2, 'Joan',    '#CD2E3A', '#FFFFFF'),
(2, 'Pocho',   '#FFD100', '#003893'),
(2, 'Jere',    '#009C3B', '#FFDF00'),
(2, 'Fede O',  '#C60B1E', '#F1BF00'),
(2, 'Tomás',   '#DA251D', '#FFFF00'),
(2, 'Carlos',  '#FCD116', '#003087'),
(2, 'Sebas C', '#74ACDF', '#FFFFFF'),
(2, 'Santino', '#FF0000', '#FFFFFF'),
(2, 'Lucas G', '#0038A8', '#FCD116');

-- Fechas Primera División (IDs 1-9)
INSERT INTO fechas (divisionid, numero, nombre) VALUES
(1,1,'Fecha 1'),(1,2,'Fecha 2'),(1,3,'Fecha 3'),
(1,4,'Fecha 4'),(1,5,'Fecha 5'),(1,6,'Fecha 6'),
(1,7,'Fecha 7'),(1,8,'Fecha 8'),(1,9,'Fecha 9');

-- Fechas Nacional B (IDs 10-18)
INSERT INTO fechas (divisionid, numero, nombre) VALUES
(2,1,'Fecha 1'),(2,2,'Fecha 2'),(2,3,'Fecha 3'),
(2,4,'Fecha 4'),(2,5,'Fecha 5'),(2,6,'Fecha 6'),
(2,7,'Fecha 7'),(2,8,'Fecha 8'),(2,9,'Fecha 9');

-- ── FIXTURE PRIMERA DIVISIÓN ────────────────────────
-- Fecha 1
INSERT INTO partidos (fechaid,divisionid,equipolocalid,equipovisitanteid,jugado) VALUES
(1,1,1,9,false),(1,1,5,10,false),(1,1,3,8,false),(1,1,6,2,false),(1,1,4,7,false);
-- Fecha 2
INSERT INTO partidos (fechaid,divisionid,equipolocalid,equipovisitanteid,jugado) VALUES
(2,1,1,8,false),(2,1,9,5,false),(2,1,10,3,false),(2,1,2,4,false),(2,1,6,7,false);
-- Fecha 3
INSERT INTO partidos (fechaid,divisionid,equipolocalid,equipovisitanteid,jugado) VALUES
(3,1,1,2,false),(3,1,3,4,false),(3,1,5,6,false),(3,1,7,8,false),(3,1,9,10,false);
-- Fecha 4
INSERT INTO partidos (fechaid,divisionid,equipolocalid,equipovisitanteid,jugado) VALUES
(4,1,1,3,false),(4,1,6,4,false),(4,1,9,2,false),(4,1,7,10,false),(4,1,8,5,false);
-- Fecha 5
INSERT INTO partidos (fechaid,divisionid,equipolocalid,equipovisitanteid,jugado) VALUES
(5,1,1,4,false),(5,1,10,2,false),(5,1,8,6,false),(5,1,9,3,false),(5,1,7,5,false);
-- Fecha 6
INSERT INTO partidos (fechaid,divisionid,equipolocalid,equipovisitanteid,jugado) VALUES
(6,1,1,6,false),(6,1,10,4,false),(6,1,8,2,false),(6,1,9,7,false),(6,1,3,5,false);
-- Fecha 7
INSERT INTO partidos (fechaid,divisionid,equipolocalid,equipovisitanteid,jugado) VALUES
(7,1,1,7,false),(7,1,2,3,false),(7,1,5,4,false),(7,1,6,10,false),(7,1,8,9,false);
-- Fecha 8
INSERT INTO partidos (fechaid,divisionid,equipolocalid,equipovisitanteid,jugado) VALUES
(8,1,1,10,false),(8,1,8,4,false),(8,1,9,6,false),(8,1,3,7,false),(8,1,2,5,false);
-- Fecha 9
INSERT INTO partidos (fechaid,divisionid,equipolocalid,equipovisitanteid,jugado) VALUES
(9,1,1,5,false),(9,1,2,7,false),(9,1,3,6,false),(9,1,4,9,false),(9,1,8,10,false);

-- ── FIXTURE NACIONAL B ──────────────────────────────
-- Fecha 1
INSERT INTO partidos (fechaid,divisionid,equipolocalid,equipovisitanteid,jugado) VALUES
(10,2,11,12,false),(10,2,13,14,false),(10,2,15,16,false),(10,2,17,18,false);
-- Fecha 2
INSERT INTO partidos (fechaid,divisionid,equipolocalid,equipovisitanteid,jugado) VALUES
(11,2,12,19,false),(11,2,11,16,false),(11,2,13,18,false),(11,2,15,17,false);
-- Fecha 3
INSERT INTO partidos (fechaid,divisionid,equipolocalid,equipovisitanteid,jugado) VALUES
(12,2,14,19,false),(12,2,12,16,false),(12,2,11,17,false),(12,2,13,15,false);
-- Fecha 4
INSERT INTO partidos (fechaid,divisionid,equipolocalid,equipovisitanteid,jugado) VALUES
(13,2,16,19,false),(13,2,14,18,false),(13,2,12,17,false),(13,2,11,13,false);
-- Fecha 5
INSERT INTO partidos (fechaid,divisionid,equipolocalid,equipovisitanteid,jugado) VALUES
(14,2,18,19,false),(14,2,16,17,false),(14,2,14,15,false),(14,2,12,13,false);
-- Fecha 6
INSERT INTO partidos (fechaid,divisionid,equipolocalid,equipovisitanteid,jugado) VALUES
(15,2,17,19,false),(15,2,18,15,false),(15,2,16,13,false),(15,2,14,11,false);
-- Fecha 7
INSERT INTO partidos (fechaid,divisionid,equipolocalid,equipovisitanteid,jugado) VALUES
(16,2,15,19,false),(16,2,17,13,false),(16,2,18,11,false),(16,2,14,12,false);
-- Fecha 8
INSERT INTO partidos (fechaid,divisionid,equipolocalid,equipovisitanteid,jugado) VALUES
(17,2,13,19,false),(17,2,15,11,false),(17,2,18,12,false),(17,2,16,14,false);
-- Fecha 9
INSERT INTO partidos (fechaid,divisionid,equipolocalid,equipovisitanteid,jugado) VALUES
(18,2,11,19,false),(18,2,15,12,false),(18,2,17,14,false),(18,2,18,16,false);

-- Verificar
SELECT 'divisiones' as tabla, COUNT(*) FROM divisiones
UNION ALL SELECT 'equipos', COUNT(*) FROM equipos
UNION ALL SELECT 'fechas', COUNT(*) FROM fechas
UNION ALL SELECT 'partidos', COUNT(*) FROM partidos;
