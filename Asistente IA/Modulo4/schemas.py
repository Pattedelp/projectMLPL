"""
DevFlow — schemas.py
Schemas Pydantic v2 para requests y responses.
"""

from datetime import datetime
from typing import Any, Optional
from pydantic import BaseModel, ConfigDict, EmailStr, field_validator
from pydantic.alias_generators import to_camel
import enum


# ─────────────────────────────────────────────
# ENUMS (espejo de models.py para Pydantic)
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
# AUTH
# ─────────────────────────────────────────────
class LoginRequest(BaseModel):
    email:    EmailStr
    password: str

class TokenResponse(BaseModel):
    access_token: str
    token_type:   str = "bearer"

class UserOut(BaseModel):
    id:        str
    email:     str
    name:      str
    role:      RoleEnum
    client_id: Optional[str] = None

    model_config = {"from_attributes": True}


# ─────────────────────────────────────────────
# CLIENT
# ─────────────────────────────────────────────
class ClientOut(BaseModel):
    id:    str
    name:  str
    email: str

    model_config = {"from_attributes": True}


# ─────────────────────────────────────────────
# CODE HINT
# ─────────────────────────────────────────────
class CodeHint(BaseModel):
    file:        str
    lines:       str
    description: str
    fix:         str


# ─────────────────────────────────────────────
# TICKET — CREATE
# ─────────────────────────────────────────────
class TicketCreate(BaseModel):
    title:       str
    description: str
    page:        str = ""
    page_name:   str = ""
    priority:    Optional[PriorityEnum] = None  # selección del usuario; IA completa si es None
    client_id:   Optional[str]         = None   # solo relevante cuando crea un admin

    @field_validator("title")
    @classmethod
    def title_not_empty(cls, v: str) -> str:
        if not v.strip():
            raise ValueError("El título no puede estar vacío")
        return v.strip()

    @field_validator("page")
    @classmethod
    def page_must_be_url(cls, v: str) -> str:
        if v and not v.startswith("http"):
            raise ValueError("La URL debe comenzar con http o https")
        return v


# ─────────────────────────────────────────────
# TICKET — UPDATE (admin)
# ─────────────────────────────────────────────
class TicketUpdate(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)

    status:          Optional[StatusEnum]      = None
    priority:        Optional[PriorityEnum]    = None
    type:            Optional[TicketTypeEnum]  = None
    eta:             Optional[str]             = None
    step_checkpoint: Optional[str]             = None
    agent_history:   Optional[list[Any]]       = None
    pool_position:   Optional[int]             = None
    # Campos de análisis IA
    ai_suggestion:   Optional[str]             = None
    page_analysis:   Optional[str]             = None
    code_hints:      Optional[list[Any]]       = None
    page_fetched:    Optional[bool]            = None
    auto_executed:   Optional[bool]            = None
    ai_error:        Optional[str]             = None
    approved:        Optional[bool]            = None


# ─────────────────────────────────────────────
# TICKET — OUT (respuesta completa)
# ─────────────────────────────────────────────
class TicketOut(BaseModel):
    id:              str
    client_id:       str
    client_name:     str
    title:           str
    description:     str
    type:            Optional[TicketTypeEnum]  = None
    page:            str
    page_name:       str
    priority:        Optional[PriorityEnum]    = None
    status:          StatusEnum
    eta:             Optional[str]             = None
    created_at:      datetime
    updated_at:      datetime

    # Campos IA (admin only — el router filtra según rol)
    ai_suggestion:   Optional[str]             = None
    page_analysis:   Optional[str]             = None
    code_hints:      Optional[list[CodeHint]]  = None

    # Flags
    page_fetched:    bool
    auto_executed:   bool
    approved:        Optional[bool]            = None
    ai_error:        Optional[str]             = None

    # Resumabilidad
    step_checkpoint: Optional[str]             = None
    agent_history:   Optional[list[Any]]       = None

    # Pool
    pool_position:   Optional[int]             = None

    model_config = {"from_attributes": True}


# ─────────────────────────────────────────────
# TICKET — OUT reducido para cliente
# (sin datos IA)
# ─────────────────────────────────────────────
class TicketOutClient(BaseModel):
    id:           str
    client_id:    str
    client_name:  str
    title:        str
    description:  str
    type:         Optional[TicketTypeEnum] = None
    page:         str
    page_name:    str
    priority:     Optional[PriorityEnum]   = None
    status:       StatusEnum
    eta:          Optional[str]            = None
    created_at:   datetime
    updated_at:   datetime
    page_fetched: bool
    auto_executed: bool
    approved:     Optional[bool]           = None
    ai_error:     Optional[str]            = None

    model_config = {"from_attributes": True}


# ─────────────────────────────────────────────
# ACCIONES ESPECÍFICAS
# ─────────────────────────────────────────────
class ApproveRejectRequest(BaseModel):
    """Body para aprobar o rechazar un ticket (admin)."""
    approved: bool   # True = aprobar, False = rechazar

class PoolReorderRequest(BaseModel):
    """Lista ordenada de IDs de tickets para actualizar pool_position."""
    ordered_ids: list[str]
