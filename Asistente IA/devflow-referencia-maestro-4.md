# DevFlow — Documento de Referencia Maestro v4
> Pegá este documento al inicio de cada conversación nueva para mantener coherencia entre módulos.

---

## ¿Qué es DevFlow?
Sistema de soporte técnico con IA para un desarrollador independiente (admin) que gestiona tickets de clientes/pymes. El admin actúa como QA/PO: la IA sugiere soluciones y el admin aprueba o rechaza antes de ejecutar. Hay un modo automático (toggle ON/OFF) que permite ejecución sin aprobación para problemas de baja criticidad.

---

## Stack tecnológico
| Capa | Tecnología |
|---|---|
| Frontend / Dashboard | Next.js 16 (App Router) + React 19 + TypeScript + Tailwind 4 |
| Backend / API | Python (FastAPI) |
| Agente IA | Claude API (claude-sonnet-4-20250514) |
| Base de datos | PostgreSQL |
| Cola de tareas | Redis + Celery (planificado) |
| Auth | JWT propio (backend FastAPI) — sin NextAuth |
| Hosting inicial | Railway (backend) + Vercel (frontend) + Supabase (PostgreSQL) |
| Hosting producción | Hetzner VPS + Docker Compose |
| Integraciones | GitHub API + Jira API (Módulo 6, pendiente) |

> ⚠️ El stack real del proyecto es **Next.js 16 + React 19 + Tailwind 4**, no Next.js 15 como figura en versiones anteriores del maestro. AGENTS.md en el proyecto lo confirma; siempre revisar `node_modules/next/dist/docs/` antes de tocar APIs de Next.

---

## Roles
- **admin** → ve todos los tickets de todos los clientes, aprueba/rechaza/reabre, controla modo auto, ve sugerencias IA y análisis de código, puede borrar tickets en `received` o `rejected`
- **client** → solo ve sus propios tickets, puede crear nuevos, reabrir tickets rechazados y borrar tickets en estado `received` o `rejected`. NO ve sugerencias IA ni code hints

---

## Estructura de datos

### Usuario / Sesión
```
User: {
  id: string,
  role: "admin" | "client",
  name: string,
  email: string,
  clientId?: string   // solo si role === "client"
}
```

### Cliente
```
Client: {
  id: string,          // "c1", "c2", etc.
  name: string,        // "TechPyme SA"
  email: string
}
```

### Ticket
```
Ticket: {
  id: string,              // "T-001"
  clientId: string,
  clientName: string,
  title: string,
  description: string,
  type: "FE" | "BE" | "DB",
  page: string,            // URL completa
  page_name: string,       // "/ruta" — snake_case para el backend
  priority: "LOW" | "MEDIUM" | "HIGH" | "CRITICAL",
  status: "received" | "analyzing" | "queued" | "approval" | "inprogress" | "completed" | "rejected" | "reopened",
  eta: string,
  createdAt: string,       // ISO 8601 — se setea desde el cliente al crear
  aiSuggestion: string,    // solo visible para admin
  pageAnalysis: string,    // solo visible para admin
  codeHints: [
    {
      file: string,
      lines: string,
      description: string,
      fix: string
    }
  ],
  pageFetched: boolean,
  autoExecuted: boolean,
  approved: null | true | false,
  aiError: string | null,
  stepCheckpoint: string,
  agentHistory: array,
  poolPosition: number
}
```

### Prioridades
```
LOW      → color #22C55E — ejecuta solo si AUTO = ON
MEDIUM   → color #EAB308 — ejecuta solo si AUTO = ON
HIGH     → color #F97316 — siempre requiere aprobación
CRITICAL → color #EF4444 — siempre requiere aprobación
```

### Escalado de prioridad al reabrir
```
LOW → MEDIUM → HIGH (tope sin ser CRITICAL)
HIGH y CRITICAL se mantienen igual
```

### Estados del ticket
```
Flujo normal:
received → analyzing → queued → inprogress → completed
                     → approval → inprogress → completed

Estados especiales:
rejected  → rechazado por admin
reopened  → reabierto tras rechazo, prioridad escala un nivel, vuelve a "approval"
```

> `queued` existe preparando el terreno para Redis + Celery (cola real). Hoy es un estado visual — el agente transiciona directo de `analyzing` a `queued` o `approval`, sin worker real detrás.

### STATUS_CONFIG
```
received:   { color: "#94A3B8", label: "Recibido"    }
analyzing:  { color: "#60A5FA", label: "Analizando"  }
queued:     { color: "#A78BFA", label: "En cola"     }
approval:   { color: "#FBBF24", label: "Aprobación"  }
inprogress: { color: "#34D399", label: "En progreso" }
completed:  { color: "#22C55E", label: "Completado"  }
rejected:   { color: "#EF4444", label: "Rechazado"   }
reopened:   { color: "#F97316", label: "Reabierto"   }
```

---

## Lógica del modo AUTO
```
AUTO OFF → todos los tickets pasan por "approval"
AUTO ON  → LOW y MEDIUM saltean "approval" → status: "queued", autoExecuted: true
           HIGH y CRITICAL siempre pasan por "approval"
```

---

## Credenciales demo
```
Admin    → dev@devflow.app     / admin123
Cliente1 → admin@techpyme.com  / client123
Cliente2 → admin@cnorte.com    / client456
```

---

## Sistema de toasts
```
- Aparecen abajo a la derecha, apilados (máx 4)
- Cada toast: ID ticket, título, nuevo estado con su color
- Barra de progreso animada, desaparece a los 4 segundos
- Clickeable para cerrar manualmente

Visibilidad:
- Admin → ve toast en cada transición de estado
- Cliente → ve toast solo al crear o reabrir un ticket
```

---

## Agente IA

### ⚠️ Estado actual de la API key — PENDIENTE DE IMPLEMENTACIÓN REAL

El agente IA **funciona en modo demo/desarrollo** solamente. Para que funcione en producción hacen falta dos cosas que aún no están implementadas:

1. **API key de Anthropic paga**: actualmente se usa una key en el `.env.local` del frontend que puede ser de prueba o estar limitada. Para uso real en producción se necesita una cuenta de Anthropic con créditos cargados (`https://console.anthropic.com`).

2. **Mover la key al backend**: hoy la API key viaja desde el browser con el header `anthropic-dangerous-direct-browser-access: true`. Esto es válido para desarrollo pero **no es seguro para producción**. La llamada a Claude debe hacerse desde el backend FastAPI, nunca desde el cliente.

Hasta que ambas cosas estén resueltas, el agente puede fallar silenciosamente o devolver errores si la key no tiene saldo o no existe.

---

### Modelo
`claude-sonnet-4-20250514` — max_tokens: 1500

### System prompt
En inglés (mejor performance). Outputs siempre en español.

### Flujo de análisis
1. Fetch de la URL del ticket via `api.allorigins.win` (proxy CORS)
2. Limpia HTML (strip scripts/styles/tags), límite 2000 chars
3. Llama a Claude API con system prompt + ticket data + page content
4. Parsea JSON estricto de la respuesta
5. Resuelve `nextStatus` según `priority` + `autoMode`
6. Si el usuario eligió una prioridad al crear el ticket, la IA la respeta y no la sobreescribe
7. Actualiza el ticket en el backend con todos los campos

### Headers requeridos para llamada desde browser (modo dev)
```ts
"x-api-key": ANTHROPIC_KEY,
"anthropic-version": "2023-06-01",
"anthropic-dangerous-direct-browser-access": "true",
```

### Campos que devuelve la IA
```json
{
  "type": "FE" | "BE" | "DB",
  "priority": "LOW" | "MEDIUM" | "HIGH" | "CRITICAL",
  "eta": "tiempo estimado en español",
  "aiSuggestion": "solución técnica detallada en español con pasos numerados",
  "pageAnalysis": "análisis del contenido de la página en español o null",
  "codeHints": [
    {
      "file": "NombreArchivo.jsx",
      "lines": "línea 40 o entre 20 y 40",
      "description": "qué cambiar en español",
      "fix": "código con el arreglo"
    }
  ]
}
```

---

## Tipografía y diseño
```
Fuentes:  DM Mono (body / monospace) + Syne (títulos/headings)
Tema:     Oscuro
Acento:   #6366F1
THEME = {
  bg:      "#0A0A0F",
  surface: "#12121A",
  border:  "#1E1E30",
  accent:  "#6366F1",
}
```

### Patrones de estilo
- Solo `style={{}}` inline con `THEME` + clases Tailwind para layout/spacing. Sin variables CSS custom.
- `font-mono` en todo, `font-syne` solo en títulos de sección.
- Badges: patrón `color`, `${color}33` border, `${color}18` background.
- No hay librerías de UI ni de drag & drop — todo nativo.

---

## Estructura real del proyecto frontend

```
Modulo5/devflow-frontend/src/
├── app/
│   ├── layout.tsx          ✅ fuentes DM Mono + Syne, tema oscuro
│   ├── page.tsx            ✅ redirect según cookie
│   ├── login/page.tsx      ✅ login real contra API
│   ├── dashboard/page.tsx  ✅ dashboard + agente IA
│   ├── pool/page.tsx       ✅ pool con drag & drop (fix Strict Mode React 19)
│   └── history/page.tsx    ✅ historial agrupado por día
├── components/
│   ├── layout/AppShell.tsx ✅
│   ├── pool/PoolRow.tsx     ✅
│   ├── tickets/
│   │   ├── TicketCard.tsx  ✅ header diferente admin/cliente, ClientBadge
│   │   ├── TicketModal.tsx ✅ admin puede borrar (canDelete sin !isAdmin)
│   │   └── TicketForm.tsx  ✅ chips de prioridad opcionales
│   └── ui/
│       ├── Badge.tsx       ✅ incluye ClientBadge nuevo
│       ├── Toast.tsx       ✅
│       └── Sidebar.tsx     ✅ link "Historial" agregado
├── hooks/
│   ├── useAuth.ts          ✅
│   └── useTickets.ts       ✅
├── lib/
│   ├── api.ts              ✅
│   ├── auth.ts             ✅
│   └── constants.ts        ✅
└── types/index.ts          ✅ incluye createdAt?: string en CreateTicketPayload
```

---

## Historial de módulos

### ✅ MÓDULO 1 — Auth + Dashboard base
Login con roles. Admin ve todos los tickets. Cliente ve solo los suyos. Filtros por estado, modal de detalle, creación de tickets.

### ✅ MÓDULO 2 — Agente IA + Sistema de toasts
Integración con Claude API desde el browser. Fetch de contenido de página via allorigins.win. Sistema de toasts animados. Prompt base del admin editable desde el sidebar. Estado `analyzing` con borde pulsante. Visibilidad por rol.

### ✅ MÓDULO 3 — Pool de prioridades
Vista admin con drag & drop HTML5 nativo. Delta vs orden IA. Botón para revertir al orden IA. Modal de detalle al hacer click.

### ✅ MÓDULO 4 — Backend FastAPI + PostgreSQL
FastAPI + SQLAlchemy + PostgreSQL en Docker. JWT con `python-jose`. Campos JSONB para `codeHints` y `agentHistory`. Seed automático al arrancar.

**Endpoints:**
```
POST   /auth/login              → recibe JSON { email, password }
GET    /auth/me
GET    /tickets                 → filtra por rol automáticamente
POST   /tickets                 → solo clientes pueden crear tickets
GET    /tickets/{id}
PATCH  /tickets/{id}
DELETE /tickets/{id}            → solo received o rejected, solo el cliente dueño
POST   /tickets/{id}/approve    → body: { approved: true } o { approved: false }
POST   /tickets/{id}/reopen
POST   /tickets/pool/reorder    → body: { ordered_ids: string[] }
GET    /health
```

**Para levantar:**
```bash
docker run -d --name devflow-db \
  -e POSTGRES_USER=devflow \
  -e POSTGRES_PASSWORD=devflow \
  -e POSTGRES_DB=devflow \
  -p 5432:5432 postgres:16

cd devflow-m4
pip install -r requirements.txt
uvicorn main:app --reload --port 8000
```

### ✅ MÓDULO 5 — Frontend Next.js conectado al backend
Frontend real conectado al backend. Agente IA funcionando en browser. Pool guarda orden en DB. Modo AUTO operativo.

**Variables de entorno (.env.local):**
```
NEXT_PUBLIC_API_URL=http://localhost:8000
NEXT_PUBLIC_ANTHROPIC_API_KEY=sk-ant-...
```

**Para levantar:**
```bash
cd Modulo5/devflow-frontend
npm install
npm run dev   # → http://localhost:3000
```

### ✅ EXTRAS (implementados fuera de módulos numerados)

**Fix drag & drop React 19 Strict Mode** — `pool/page.tsx`
Strict Mode invoca los updaters dos veces. El fix captura `fromIdx = dragIdx.current` antes del updater y saca las mutaciones afuera, manteniendo el updater puro.

**Admin puede borrar tickets** — `TicketModal.tsx` + `pool/page.tsx`
```ts
// ANTES
const canDelete = !isAdmin && (ticket.status === "received" || ticket.status === "rejected");
// DESPUÉS
const canDelete = ticket.status === "received" || ticket.status === "rejected";
```

**Historial de actividad** — `history/page.tsx` + `Sidebar.tsx`
Nueva ruta `/history` para admin y cliente. Tickets agrupados por día (Hoy / Ayer / Esta semana / mes-año). Timestamps relativos ("hace 2h") + hora exacta. Link en Sidebar visible para ambos roles.

**ClientBadge** — `Badge.tsx`
Nuevo badge indigo-300 (`#A5B4FC`) que muestra el nombre del cliente en el header de la TicketCard (solo visible para admin).

**Chips de prioridad en el formulario** — `TicketForm.tsx`
Selector opcional de criticidad con 4 chips toggle. La IA respeta la prioridad elegida por el usuario y no la sobreescribe.

**createdAt desde el cliente** — `dashboard/page.tsx`
```ts
createdAt: new Date().toISOString()  // se setea al crear, no en el backend
```

---

### 🔲 MÓDULO 6 — Integraciones GitHub + Jira
**Estado:** PENDIENTE

- GitHub: al aprobar ticket, crear commit/PR automático con el fix de `codeHints`
- Jira: crear issue al recibir ticket, sincronizar estados, webhook para `completed`
- Configuración por cliente: repo GitHub y proyecto Jira propios

---

## Pendientes para producción (pre-venta)
```
🔴 Crítico:
- Mover agente IA al backend (API key segura) ← AÚN NO IMPLEMENTADO
- Cargar créditos en cuenta Anthropic para que el agente funcione en producción ← AÚN NO IMPLEMENTADO
- HTTPS obligatorio
- Rate limiting en el backend
- Panel admin para crear/gestionar clientes sin tocar la DB

🟡 Importante:
- Emails de notificación (SendGrid/Resend)
- Log de cambios de estado por ticket con timestamp
- Métricas: tiempo de resolución, tickets por cliente, SLA

🟢 Diferenciador:
- Módulo 6 (GitHub/Jira)
- White label por cliente
- Chat en tiempo real dentro del ticket
```

---

## Notas para la IA al arrancar cada sesión

1. El stack real es **Next.js 16 + React 19 + Tailwind 4** — no Next.js 15
2. El frontend vive en `Modulo5/devflow-frontend/src/`
3. Respetar los nombres de campos definidos en esta referencia
4. El backend espera `page_name` (snake_case), no `pageName`
5. `POST /auth/login` espera JSON, no form-urlencoded
6. `POST /tickets/pool/reorder` espera `{ ordered_ids: string[] }`
7. `POST /tickets/{id}/approve` con `{ approved: true/false }` — mismo endpoint para aprobar y rechazar
8. Drag & drop del pool usa HTML5 nativo (`draggable`) — no mouse events ni librerías
9. El updater de `setPoolOrder` debe ser puro — las mutaciones del ref van afuera (fix Strict Mode)
10. La API key de Anthropic se manda con `anthropic-dangerous-direct-browser-access: true` desde el browser — solo para dev
11. **El agente IA no funcionará en producción hasta que**: (a) se carguen créditos en la cuenta Anthropic y (b) se mueva la key al backend FastAPI
12. La IA respeta la prioridad elegida por el usuario — no la sobreescribe
13. `canDelete` no tiene `!isAdmin` — tanto admin como cliente pueden borrar en `received`/`rejected`
14. Tipografía: DM Mono (body) + Syne (títulos). Tema oscuro. Acento #6366F1
15. Todo en español. System prompt del agente en inglés
