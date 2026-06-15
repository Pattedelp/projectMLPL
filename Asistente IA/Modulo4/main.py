"""
DevFlow — main.py
Entry point de la API FastAPI.
Ejecutar: uvicorn main:app --reload --port 8000
"""

import os
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from database import Base, engine, SessionLocal
from routers import auth, tickets
from seed import seed_demo_data


# ─────────────────────────────────────────────
# Startup: crear tablas + seed demo
# ─────────────────────────────────────────────
@asynccontextmanager
async def lifespan(app: FastAPI):
    # Crea todas las tablas si no existen
    Base.metadata.create_all(bind=engine)
    # Carga datos demo (solo si la DB está vacía)
    db = SessionLocal()
    try:
        seed_demo_data(db)
    finally:
        db.close()
    yield
    # Cleanup (si hiciera falta)


# ─────────────────────────────────────────────
# App
# ─────────────────────────────────────────────
app = FastAPI(
    title="DevFlow API",
    description="Backend de soporte técnico con IA para desarrolladores independientes.",
    version="0.4.0",
    lifespan=lifespan,
)


# ─────────────────────────────────────────────
# CORS
# Permite requests desde el frontend Next.js (localhost:3000 en dev,
# dominio de Vercel en producción).
# ─────────────────────────────────────────────
ALLOWED_ORIGINS = os.getenv(
    "ALLOWED_ORIGINS",
    "http://localhost:3000,http://127.0.0.1:3000"
).split(",")

app.add_middleware(
    CORSMiddleware,
    allow_origins=ALLOWED_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# ─────────────────────────────────────────────
# Routers
# ─────────────────────────────────────────────
app.include_router(auth.router)
app.include_router(tickets.router)


# ─────────────────────────────────────────────
# Health check
# ─────────────────────────────────────────────
@app.get("/health", tags=["meta"])
def health():
    return {"status": "ok", "version": "0.4.0"}
