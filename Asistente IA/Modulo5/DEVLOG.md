# DevFlow — Devlog de sesión

## Proyecto

**DevFlow** es un sistema de soporte técnico con IA. Stack: Next.js 16 + React 19 + TypeScript + Tailwind 4. El frontend vive en `Modulo5/devflow-frontend/src/`.

Estructura de rutas:
- `/dashboard` — lista de tickets, creación, modal de detalle
- `/pool` — pool de prioridades con drag & drop (solo admin)
- `/history` — historial de actividad (todos los usuarios)
- `/login` — autenticación

Roles: `admin` (ve todo, puede aprobar/rechazar/borrar) y `client` (ve solo sus tickets, puede crear/borrar los propios).

---

## Cambios realizados en esta sesión

### 1. Fix drag & drop — `src/app/pool/page.tsx`

**Problema:** El drag & drop no funcionaba en desarrollo. React 19 con Strict Mode invoca los state updaters **dos veces** para detectar side effects. El código original mutaba `dragIdx.current` y llamaba `setDraggingIdx` *dentro* del updater de `setPoolOrder`, lo que hacía que la segunda invocación leyera el ref ya mutado y produjera un splice `[n → n]` (no-op).

**Fix:** Capturar `fromIdx = dragIdx.current` antes del updater, y mover las mutaciones fuera:

```ts
// ANTES (roto en Strict Mode)
setPoolOrder((prev) => {
  const next = [...prev];
  const [removed] = next.splice(dragIdx.current, 1); // lee ref
  next.splice(newIdx, 0, removed);
  dragIdx.current = newIdx;    // ← side effect dentro del updater
  setDraggingIdx(newIdx);      // ← setState dentro de setState
  return next;
});

// DESPUÉS (correcto)
const fromIdx = dragIdx.current;
if (newIdx === fromIdx) return;

dragIdx.current = newIdx;      // fuera del updater
setDraggingIdx(newIdx);        // fuera del updater

setPoolOrder((prev) => {       // updater puro
  const next = [...prev];
  const [removed] = next.splice(fromIdx, 1);
  next.splice(newIdx, 0, removed);
  return next;
});
```

---

### 2. Admin puede borrar tickets — `src/components/tickets/TicketModal.tsx` + `src/app/pool/page.tsx`

**Antes:** `canDelete` bloqueaba explícitamente al admin con `!isAdmin`.  
**Después:** cualquier rol puede borrar tickets en estado `received` o `rejected`.

```ts
// ANTES
const canDelete = !isAdmin && (ticket.status === "received" || ticket.status === "rejected");

// DESPUÉS
const canDelete = ticket.status === "received" || ticket.status === "rejected";
```

También se agregó `deleteTicket` + `handleDelete` + `onDelete={handleDelete}` en `pool/page.tsx` (el dashboard ya lo tenía cableado).

---

### 3. Página de historial — `src/app/history/page.tsx` + `src/components/ui/Sidebar.tsx`

Nueva ruta `/history` visible para admin y cliente. Muestra todos los tickets agrupados por día (Hoy / Ayer / Esta semana / mes-año), ordenados de más nuevo a más viejo.

Cada fila: dot de color según estado · ID · título · clientName (admin) · TypeBadge + PriorityBadge + StatusBadge · timestamp relativo ("hace 2h") + hora exacta.

En el Sidebar se agregó el botón "Historial" entre "Todos los tickets" y los filtros de estado, visible para ambos roles, se activa cuando `pathname === "/history"`.

Funciones de fecha clave en `history/page.tsx`:
- `formatRelative(date)` → "ahora", "hace 5m", "hace 3h", "ayer", "hace 4 días", "12 jun. 2025"
- `dayGroupLabel(date)` → "Hoy", "Ayer", "Esta semana", "junio 2025"

---

### 4. Etiquetas en tickets (form + cards)

#### 4a. `src/types/index.ts`
Agregado `createdAt?: string` a `CreateTicketPayload` para poder enviarlo desde el cliente.

#### 4b. `src/components/ui/Badge.tsx`
Nuevo componente `ClientBadge` (color `#A5B4FC` indigo-300, mismo patrón que los demás badges):
```tsx
export function ClientBadge({ name }: { name: string }) {
  return (
    <span style={{ color: "#A5B4FC", border: "1px solid #A5B4FC33", backgroundColor: "#A5B4FC18" }}
      className="inline-flex items-center px-2 py-0.5 rounded text-xs font-mono whitespace-nowrap">
      {name}
    </span>
  );
}
```

#### 4c. `src/components/tickets/TicketForm.tsx`
Selector de criticidad (priority) con 4 chips toggle (Crítica/Alta/Media/Baja) usando `PRIORITY_ORDER` y `PRIORITY_CONFIG`. Campo opcional — toggle: click en el activo lo deselecciona. Se inserta entre el campo "Descripción" y la grilla URL/ruta.

```tsx
<Field label="Criticidad">
  <div className="flex gap-2">
    {PRIORITY_ORDER.map((level) => {
      const cfg = PRIORITY_CONFIG[level];
      const selected = form.priority === level;
      return (
        <button key={level} type="button"
          onClick={() => setForm(prev => ({ ...prev, priority: selected ? undefined : level }))}
          className="flex-1 py-1.5 rounded-lg text-xs font-mono font-bold transition-all"
          style={{
            color: selected ? "#0A0A0F" : cfg.color,
            backgroundColor: selected ? cfg.color : `${cfg.color}18`,
            border: `1px solid ${cfg.color}${selected ? "FF" : "44"}`,
          }}>
          {cfg.label}
        </button>
      );
    })}
  </div>
</Field>
```

#### 4d. `src/components/tickets/TicketCard.tsx`
Layout del header reestructurado:

**Admin ve:**
```
T-001  [⚡ Auto]
[TechPyme SA]     ← ClientBadge (nuevo, reemplaza el texto plano que estaba ABAJO del título)
título del ticket
                          [StatusBadge]
                          [PriorityBadge]
                          [TypeBadge]   ← nuevo en header para admin
```

**Cliente ve:**
```
T-001  [⚡ Auto]
título del ticket
                          [StatusBadge]
                          [PriorityBadge]
```
Footer: TypeBadge solo para clientes (admin ya lo tiene en el header).

#### 4e. `src/app/dashboard/page.tsx`
Dos cambios en `handleCreate` y `runAgent`:

```ts
// handleCreate — timestamp siempre desde el reloj del cliente:
const ticket = await createTicket({
  ...payload,
  status: "received",
  stepCheckpoint: "created",
  createdAt: new Date().toISOString(),  // ← nuevo
});

// runAgent — IA solo completa prioridad si el usuario no eligió una:
await updateTicket(ticketId, {
  type: aiResult.type,
  ...(payload.priority == null ? { priority: aiResult.priority } : {}),  // ← respeta al usuario
  eta: aiResult.eta,
  // ...resto igual
});
```

---

## Estado actual de archivos clave

| Archivo | Qué hace |
|---|---|
| `src/app/pool/page.tsx` | Drag & drop corregido, admin puede borrar |
| `src/app/dashboard/page.tsx` | createdAt desde cliente, prioridad de IA condicional |
| `src/app/history/page.tsx` | Nuevo — historial agrupado por día |
| `src/components/ui/Sidebar.tsx` | Link "Historial" agregado |
| `src/components/ui/Badge.tsx` | Nuevo ClientBadge |
| `src/components/tickets/TicketForm.tsx` | Chips de criticidad |
| `src/components/tickets/TicketCard.tsx` | ClientBadge + TypeBadge en header admin |
| `src/components/tickets/TicketModal.tsx` | Admin puede borrar (canDelete sin !isAdmin) |

## Patrones del proyecto

- **Estilos:** solo `style={{}}` inline con `THEME` + clases Tailwind para layout/spacing. Sin variables CSS custom.
- **Fuente:** `font-mono` en todo, `font-syne` solo en títulos de sección.
- **Colores THEME:** `bg: "#0A0A0F"`, `surface: "#12121A"`, `border: "#1E1E30"`, `accent: "#6366F1"`.
- **Badges:** patrón `color`, `${color}33` border, `${color}18` background.
- **No hay librerías de UI ni de drag & drop** — todo nativo.
- **AGENTS.md** en el proyecto indica que es Next.js 16 con breaking changes; siempre revisar `node_modules/next/dist/docs/` antes de tocar APIs de Next.
