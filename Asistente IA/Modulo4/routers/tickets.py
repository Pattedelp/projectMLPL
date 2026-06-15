"""
DevFlow — routers/tickets.py
CRUD completo de tickets + acciones: aprobar, rechazar, reabrir.

Endpoints:
  GET    /tickets              → lista (admin: todos | client: los suyos)
  POST   /tickets              → crear ticket nuevo
  GET    /tickets/{id}         → detalle de un ticket
  PATCH  /tickets/{id}         → actualización general (admin)
  DELETE /tickets/{id}         → eliminar (admin)
  POST   /tickets/{id}/approve → aprobar o rechazar (admin)
  POST   /tickets/{id}/reopen  → reabrir ticket rechazado (admin o cliente dueño)
  POST   /tickets/pool/reorder → guardar orden manual del pool (admin)
"""

import uuid
from datetime import datetime, timezone
from typing import Optional

from fastapi import APIRouter, Depends, HTTPException, Query, status
from sqlalchemy.orm import Session

from database import get_db
from models import (
    Client, PriorityEnum, PRIORITY_ESCALATION,
    StatusEnum, Ticket
)
from routers.auth import get_current_user, require_admin
from schemas import (
    ApproveRejectRequest, PoolReorderRequest,
    TicketCreate, TicketOut, TicketOutClient, TicketUpdate
)
from models import User

router = APIRouter(prefix="/tickets", tags=["tickets"])


# ─────────────────────────────────────────────
# Helper: siguiente ID de ticket
# ─────────────────────────────────────────────
def _next_ticket_id(db: Session) -> str:
    count = db.query(Ticket).count()
    return f"T-{count + 1:03d}"


# ─────────────────────────────────────────────
# Helper: serializar ticket según rol
# ─────────────────────────────────────────────
def _serialize(ticket: Ticket, role: str) -> dict:
    if role == "admin":
        return TicketOut.model_validate(ticket).model_dump()
    # cliente: eliminar campos IA
    out = TicketOutClient.model_validate(ticket).model_dump()
    return out


# ─────────────────────────────────────────────
# GET /tickets
# ─────────────────────────────────────────────
@router.get("/")
def list_tickets(
    status_filter: Optional[str] = Query(None, alias="status"),
    priority:      Optional[str] = Query(None),
    type_filter:   Optional[str] = Query(None, alias="type"),
    client_id:     Optional[str] = Query(None),
    db:            Session       = Depends(get_db),
    current_user:  User          = Depends(get_current_user),
):
    q = db.query(Ticket)

    # Clientes solo ven sus propios tickets
    if current_user.role == "client":
        q = q.filter(Ticket.client_id == current_user.client_id)
    elif client_id:
        q = q.filter(Ticket.client_id == client_id)

    if status_filter:
        q = q.filter(Ticket.status == status_filter)
    if priority:
        q = q.filter(Ticket.priority == priority)
    if type_filter:
        q = q.filter(Ticket.type == type_filter)

    # Ordenar: primero por pool_position (si existe), luego por created_at desc
    tickets = q.order_by(
        Ticket.pool_position.asc().nullslast(),
        Ticket.created_at.desc()
    ).all()

    return [_serialize(t, current_user.role) for t in tickets]


# ─────────────────────────────────────────────
# POST /tickets
# ─────────────────────────────────────────────
@router.post("/", status_code=status.HTTP_201_CREATED)
def create_ticket(
    body:         TicketCreate,
    db:           Session = Depends(get_db),
    current_user: User    = Depends(get_current_user),
):
    # Determinar client_id
    if current_user.role == "client":
        client_id = current_user.client_id
    else:
        # Admin: usar el client_id del body o el primer cliente disponible como fallback
        client_id = body.client_id
        if not client_id:
            first = db.query(Client).first()
            if not first:
                raise HTTPException(status_code=400, detail="No hay clientes registrados")
            client_id = first.id

    client = db.query(Client).filter(Client.id == client_id).first()
    if not client:
        raise HTTPException(status_code=404, detail="Cliente no encontrado")

    ticket = Ticket(
        id          = _next_ticket_id(db),
        client_id   = client_id,
        client_name = client.name,
        title       = body.title,
        description = body.description,
        page        = body.page,
        page_name   = body.page_name,
        priority    = body.priority,
        status      = StatusEnum.received,
    )
    db.add(ticket)
    db.commit()
    db.refresh(ticket)
    return _serialize(ticket, current_user.role)


# ─────────────────────────────────────────────
# GET /tickets/{id}
# ─────────────────────────────────────────────
@router.get("/{ticket_id}")
def get_ticket(
    ticket_id:    str,
    db:           Session = Depends(get_db),
    current_user: User    = Depends(get_current_user),
):
    ticket = db.query(Ticket).filter(Ticket.id == ticket_id).first()
    if not ticket:
        raise HTTPException(status_code=404, detail="Ticket no encontrado")

    # Cliente solo puede ver sus propios tickets
    if current_user.role == "client" and ticket.client_id != current_user.client_id:
        raise HTTPException(status_code=403, detail="Acceso denegado")

    return _serialize(ticket, current_user.role)


# ─────────────────────────────────────────────
# PATCH /tickets/{id}
# ─────────────────────────────────────────────
@router.patch("/{ticket_id}")
def update_ticket(
    ticket_id:    str,
    body:         TicketUpdate,
    db:           Session = Depends(get_db),
    current_user: User    = Depends(get_current_user),
):
    ticket = db.query(Ticket).filter(Ticket.id == ticket_id).first()
    if not ticket:
        raise HTTPException(status_code=404, detail="Ticket no encontrado")

    # El cliente solo puede actualizar sus propios tickets (runAgent corre en el browser)
    if current_user.role == "client" and ticket.client_id != current_user.client_id:
        raise HTTPException(status_code=403, detail="Acceso denegado")

    update_data = body.model_dump(exclude_unset=True)
    for field, value in update_data.items():
        setattr(ticket, field, value)

    ticket.updated_at = datetime.now(timezone.utc)
    db.commit()
    db.refresh(ticket)
    return _serialize(ticket, current_user.role)


# ─────────────────────────────────────────────
# DELETE /tickets/{id}
# ─────────────────────────────────────────────
@router.delete("/{ticket_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_ticket(
    ticket_id:    str,
    db:           Session = Depends(get_db),
    current_user: User    = Depends(get_current_user),
):
    ticket = db.query(Ticket).filter(Ticket.id == ticket_id).first()
    if not ticket:
        raise HTTPException(status_code=404, detail="Ticket no encontrado")

    if current_user.role == "client":
        if ticket.client_id != current_user.client_id:
            raise HTTPException(status_code=403, detail="Acceso denegado")
        if ticket.status not in (StatusEnum.received, StatusEnum.rejected):
            raise HTTPException(
                status_code=400,
                detail="Solo podés borrar tickets en estado 'received' o 'rejected'"
            )

    db.delete(ticket)
    db.commit()


# ─────────────────────────────────────────────
# POST /tickets/{id}/approve
# Aprobar (True) o rechazar (False) — solo admin
# ─────────────────────────────────────────────
@router.post("/{ticket_id}/approve")
def approve_or_reject(
    ticket_id: str,
    body:      ApproveRejectRequest,
    db:        Session = Depends(get_db),
    _:         User    = Depends(require_admin),
):
    ticket = db.query(Ticket).filter(Ticket.id == ticket_id).first()
    if not ticket:
        raise HTTPException(status_code=404, detail="Ticket no encontrado")

    if ticket.status not in (StatusEnum.approval, StatusEnum.reopened):
        raise HTTPException(
            status_code=400,
            detail=f"Solo se puede aprobar/rechazar un ticket en estado 'approval' o 'reopened'. Estado actual: {ticket.status}"
        )

    if body.approved:
        ticket.status   = StatusEnum.inprogress
        ticket.approved = True
    else:
        ticket.status   = StatusEnum.rejected
        ticket.approved = False

    ticket.updated_at = datetime.now(timezone.utc)
    db.commit()
    db.refresh(ticket)
    return TicketOut.model_validate(ticket).model_dump()


# ─────────────────────────────────────────────
# POST /tickets/{id}/reopen
# Reabrir un ticket rechazado — admin o cliente dueño
# Escala prioridad un nivel y vuelve a "approval"
# ─────────────────────────────────────────────
@router.post("/{ticket_id}/reopen")
def reopen_ticket(
    ticket_id:    str,
    db:           Session = Depends(get_db),
    current_user: User    = Depends(get_current_user),
):
    ticket = db.query(Ticket).filter(Ticket.id == ticket_id).first()
    if not ticket:
        raise HTTPException(status_code=404, detail="Ticket no encontrado")

    # Verificar que el cliente solo pueda reabrir sus propios tickets
    if current_user.role == "client" and ticket.client_id != current_user.client_id:
        raise HTTPException(status_code=403, detail="Acceso denegado")

    if ticket.status != StatusEnum.rejected:
        raise HTTPException(
            status_code=400,
            detail=f"Solo se puede reabrir un ticket rechazado. Estado actual: {ticket.status}"
        )

    # Escalar prioridad
    if ticket.priority:
        ticket.priority = PRIORITY_ESCALATION[ticket.priority]

    ticket.status      = StatusEnum.reopened
    ticket.approved    = None   # reset para nueva aprobación
    ticket.updated_at  = datetime.now(timezone.utc)

    db.commit()
    db.refresh(ticket)
    return _serialize(ticket, current_user.role)
# ─────────────────────────────────────────────
# DELETE /tickets
# Solo el cliente puede borrar
# Body: { "ordered_ids": ["T-001", "T-003", "T-002", ...] }
# ─────────────────────────────────────────────
@router.delete("/{ticket_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_ticket(
    ticket_id:    str,
    db:           Session = Depends(get_db),
    current_user: User    = Depends(get_current_user),
):
    ticket = db.query(Ticket).filter(Ticket.id == ticket_id).first()
    if not ticket:
        raise HTTPException(status_code=404, detail="Ticket no encontrado")

    # Cliente solo puede borrar sus propios tickets
    if current_user.role == "client" and ticket.client_id != current_user.client_id:
        raise HTTPException(status_code=403, detail="No tenés permiso para borrar este ticket")

    # Solo se pueden borrar tickets en estado received o rejected
    if ticket.status not in ("received", "rejected"):
        raise HTTPException(
            status_code=400,
            detail="Solo podés borrar tickets en estado 'Recibido' o 'Rechazado'"
        )

    db.delete(ticket)
    db.commit()
    return None
# ─────────────────────────────────────────────
# POST /tickets/pool/reorder
# Guardar orden manual del pool (admin)
# Body: { "ordered_ids": ["T-001", "T-003", "T-002", ...] }
# ─────────────────────────────────────────────
@router.post("/pool/reorder", status_code=status.HTTP_200_OK)
def reorder_pool(
    body: PoolReorderRequest,
    db:   Session = Depends(get_db),
    _:    User    = Depends(require_admin),
):
    for position, ticket_id in enumerate(body.ordered_ids, start=1):
        db.query(Ticket).filter(Ticket.id == ticket_id).update(
            {"pool_position": position, "updated_at": datetime.now(timezone.utc)}
        )
    db.commit()
    return {"detail": f"Pool reordenado: {len(body.ordered_ids)} tickets actualizados"}
