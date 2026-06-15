# DevFlow

Sistema de soporte técnico con IA para gestionar tickets de clientes. El admin revisa y aprueba lo que la IA sugiere antes de ejecutar. Diseñado para un desarrollador independiente que actúa como QA/PO entre la IA y el cliente.

---

## Stack tecnológico

| Capa | Tecnología |
|---|---|
| Frontend | Next.js 16 + React 19 + TypeScript + Tailwind 4 |
| Backend | Python + FastAPI |
| Base de datos | PostgreSQL |
| Agente IA | Claude API (claude-sonnet-4-20250514) |
| Auth | JWT propio (sin NextAuth) |

---

## Módulos implementados

### Módulo 1 — Auth + Dashboard base
Login con roles diferenciados. El **admin** ve todos los tickets de todos los clientes con opciones de aprobación, rechazo y reapertura. El **cliente** solo ve sus propios tickets y puede crear nuevos. Incluye filtros por estado y modal de detalle por ticket.

### Módulo 2 — Agente IA
Al crear un ticket, la IA analiza automáticamente la URL del problema: extrae contenido de la página, clasifica el ticket por tipo (FE/BE/DB), prioridad y tiempo estimado, y genera una sugerencia de solución con hints de código específicos. Todo esto es visible solo para el admin. Incluye sistema de toasts animados que notifican cada cambio de estado.

### Módulo 3 — Pool de prioridades
Vista exclusiva del admin con todos los tickets activos ordenados por prioridad sugerida por la IA. Permite reordenar manualmente con drag & drop nativo (sin librerías), muestra un indicador de cuánto se alejó el orden manual del sugerido por la IA, y tiene un botón para restaurar el orden original.

### Módulo 4 — Backend real (FastAPI + PostgreSQL)
API REST completa con autenticación JWT, gestión de tickets, aprobación/rechazo, reapertura y reordenamiento del pool. El backend filtra automáticamente los tickets según el rol del usuario autenticado. Seed automático con usuarios y tickets de prueba al arrancar.

### Módulo 5 — Frontend Next.js conectado al backend
El frontend pasa de datos mock a datos reales. El agente IA funciona desde el browser, el pool guarda el orden en la base de datos, y el modo AUTO permite que tickets LOW/MEDIUM se ejecuten sin necesidad de aprobación manual.

---

### Extras implementados

- **Historial de actividad** (`/history`): muestra todos los tickets agrupados por día (Hoy / Ayer / Esta semana / mes-año), con timestamps relativos.
- **Chips de prioridad en el formulario**: el cliente puede sugerir una prioridad al crear el ticket; la IA la respeta si fue elegida.
- **Admin puede borrar tickets** en estado `received` o `rejected`.
- **Fix drag & drop para React 19 Strict Mode**: el updater de estado es puro, sin side effects adentro.

---

## Cómo levantar el proyecto

### Lo que necesitás tener instalado

| Herramienta | Versión recomendada |
|---|---|
| Node.js | 20 o superior |
| npm | 9 o superior |
| Python | 3.11 o superior |
| Docker Desktop | última versión estable |
| Git | cualquier versión reciente |

---

### 1. Base de datos (PostgreSQL con Docker)

```bash
docker run -d --name devflow-db \
  -e POSTGRES_USER=devflow \
  -e POSTGRES_PASSWORD=devflow \
  -e POSTGRES_DB=devflow \
  -p 5432:5432 \
  postgres:16
```

Verificar que esté corriendo:
```bash
docker ps
```

---

### 2. Backend (FastAPI)

```bash
cd devflow-m4
pip install -r requirements.txt
uvicorn main:app --reload --port 8000
```

El seed se ejecuta automáticamente al arrancar. La API queda disponible en `http://localhost:8000`.

Documentación interactiva: `http://localhost:8000/docs`

---

### 3. Frontend (Next.js)

```bash
cd devflow-frontend
npm install
npm run dev
```

El frontend queda disponible en `http://localhost:3000`.

#### Variables de entorno

Crear el archivo `.env.local` dentro de `devflow-frontend/`:

```env
NEXT_PUBLIC_API_URL=http://localhost:8000
NEXT_PUBLIC_ANTHROPIC_API_KEY=sk-ant-...
```

> La API key de Anthropic se usa directamente desde el browser (modo desarrollo). En producción debe moverse al backend.

---

### Orden de arranque recomendado

```
1. Docker (PostgreSQL)  →  docker run ...
2. Backend              →  uvicorn main:app --reload --port 8000
3. Frontend             →  npm run dev
```

---

## Credenciales demo

| Rol | Email | Contraseña |
|---|---|---|
| Admin | dev@devflow.app | admin123 |
| Cliente 1 | admin@techpyme.com | client123 |
| Cliente 2 | admin@cnorte.com | client456 |

---

## Rutas del frontend

| Ruta | Acceso | Descripción |
|---|---|---|
| `/login` | todos | Pantalla de autenticación |
| `/dashboard` | todos | Lista de tickets, creación, modal de detalle |
| `/pool` | solo admin | Pool de prioridades con drag & drop |
| `/history` | todos | Historial de actividad agrupado por día |

---

## Módulo pendiente

### Módulo 6 — GitHub + Jira *(en desarrollo)*
Integración para crear commits y PRs automáticos con los fixes sugeridos por la IA, y sincronizar estados con proyectos de Jira. Configuración por cliente con repo y proyecto propios.
