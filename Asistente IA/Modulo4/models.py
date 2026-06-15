"""
DevFlow — models.py
Modelos SQLAlchemy para User, Client y Ticket.
Todos los campos respetan el schema definido en devflow-referencia-maestro-1.md
"""

from datetime import datetime, timezone
from sqlalchemy import (
    Boolean, Column, DateTime, Enum, ForeignKey,
    String, Text, Integer
)
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.orm import relationship
import enum

from database import Base


# ─────────────────────────────────────────────
# ENUMS
# ─────────────────────────────────────────────
class RoleEnum(str, enum.Enum):
    admin  = "admin"
    client = "client"


class TicketTypeEnum(str, enum.Enum):
    FE = "FE"
    BE = "BE"
    DB = "DB"


class PriorityEnum(str, enum.Enum):
    LOW      = "LOW"
    MEDIUM   = "MEDIUM"
    HIGH     = "HIGH"
    CRITICAL = "CRITICAL"


class StatusEnum(str, enum.Enum):
    received   = "received"
    analyzing  = "analyzing"
    queued     = "queued"
    approval   = "approval"
    inprogress = "inprogress"
    completed  = "completed"
    rejected   = "rejected"
    reopened   = "reopened"


# ─────────────────────────────────────────────
# ESCALADO DE PRIORIDAD AL REABRIR
# LOW → MEDIUM → HIGH (tope, sin llegar a CRITICAL)
# HIGH y CRITICAL se mantienen igual
# ─────────────────────────────────────────────
PRIORITY_ESCALATION = {
    PriorityEnum.LOW:      PriorityEnum.MEDIUM,
    PriorityEnum.MEDIUM:   PriorityEnum.HIGH,
    PriorityEnum.HIGH:     PriorityEnum.HIGH,
    PriorityEnum.CRITICAL: PriorityEnum.CRITICAL,
}


# ─────────────────────────────────────────────
# CLIENT
# ─────────────────────────────────────────────
class Client(Base):
    __tablename__ = "clients"

    id    = Column(String, primary_key=True)      # "c1", "c2", etc.
    name  = Column(String, nullable=False)         # "TechPyme SA"
    email = Column(String, nullable=False, unique=True)

    users   = relationship("User",   back_populates="client")
    tickets = relationship("Ticket", back_populates="client")


# ─────────────────────────────────────────────
# USER
# ─────────────────────────────────────────────
class User(Base):
    __tablename__ = "users"

    id            = Column(String, primary_key=True)
    email         = Column(String, nullable=False, unique=True)
    name          = Column(String, nullable=False)
    hashed_password = Column(String, nullable=False)
    role          = Column(Enum(RoleEnum), nullable=False, default=RoleEnum.client)
    client_id     = Column(String, ForeignKey("clients.id"), nullable=True)

    client = relationship("Client", back_populates="users")


# ─────────────────────────────────────────────
# TICKET
# ─────────────────────────────────────────────
class Ticket(Base):
    __tablename__ = "tickets"

    # Identificación
    id          = Column(String, primary_key=True)   # "T-001"
    client_id   = Column(String, ForeignKey("clients.id"), nullable=False)
    client_name = Column(String, nullable=False)     # desnormalizado para queries rápidas

    # Descripción
    title       = Column(String,  nullable=False)
    description = Column(Text,    nullable=False)
    type        = Column(Enum(TicketTypeEnum), nullable=True)   # asignado por IA
    page        = Column(String,  nullable=False)    # URL completa
    page_name   = Column(String,  nullable=False)    # "/ruta"

    # Clasificación IA
    priority    = Column(Enum(PriorityEnum), nullable=True)
    status      = Column(Enum(StatusEnum),   nullable=False, default=StatusEnum.received)
    eta         = Column(String, nullable=True)      # "2h", "30m", "1d"

    # Timestamps
    created_at  = Column(DateTime(timezone=True), default=lambda: datetime.now(timezone.utc))
    updated_at  = Column(DateTime(timezone=True), default=lambda: datetime.now(timezone.utc),
                         onupdate=lambda: datetime.now(timezone.utc))

    # Campos de análisis IA (solo visibles para admin)
    ai_suggestion  = Column(Text,  nullable=True)
    page_analysis  = Column(Text,  nullable=True)
    # code_hints: lista de { file, lines, description, fix }
    code_hints     = Column(JSONB, nullable=True, default=list)

    # Flags operacionales
    page_fetched   = Column(Boolean, nullable=False, default=False)
    auto_executed  = Column(Boolean, nullable=False, default=False)
    approved       = Column(Boolean, nullable=True)   # null=pendiente, True=aprobado, False=rechazado
    ai_error       = Column(Text,  nullable=True)

    # Resumabilidad del agente (Módulo 4+)
    step_checkpoint = Column(String, nullable=True)   # último paso completado
    agent_history   = Column(JSONB,  nullable=True, default=list)  # historial de mensajes

    # Pool order (Módulo 3)
    pool_position   = Column(Integer, nullable=True)  # orden manual del admin

    client = relationship("Client", back_populates="tickets")
