"""
DevFlow — database.py
Conexión a PostgreSQL con SQLAlchemy.
"""

import os
from sqlalchemy import create_engine
from sqlalchemy.orm import declarative_base, sessionmaker
from dotenv import load_dotenv

load_dotenv()

DATABASE_URL = os.getenv(
    "DATABASE_URL",
    "postgresql://devflow:devflow@localhost:5432/devflow"
)

engine = create_engine(
    DATABASE_URL,
    pool_pre_ping=True,   # detecta conexiones caídas antes de usarlas
    pool_size=10,
    max_overflow=20,
    echo=False,           # cambiar a True para ver SQL en consola (dev)
)

SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)

Base = declarative_base()


# ─────────────────────────────────────────────
# Dependency para FastAPI (inyección de sesión)
# ─────────────────────────────────────────────
def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()
