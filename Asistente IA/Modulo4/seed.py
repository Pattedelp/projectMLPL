"""
DevFlow — seed.py
Carga los datos demo en la DB si está vacía.
Credenciales:
  Admin   → dev@devflow.app    / admin123
  Client1 → admin@techpyme.com / client123
  Client2 → admin@cnorte.com  / client456
"""

from datetime import datetime, timezone
from sqlalchemy.orm import Session

from models import Client, PriorityEnum, StatusEnum, TicketTypeEnum, User, Ticket
from routers.auth import hash_password


def seed_demo_data(db: Session) -> None:
    # No re-seedear si ya hay datos
    if db.query(User).count() > 0:
        return

    print("[seed] Cargando datos demo...")

    # ── Clientes ───────────────────────────────
    c1 = Client(id="c1", name="TechPyme SA",      email="contacto@techpyme.com")
    c2 = Client(id="c2", name="Comercial Norte",  email="contacto@cnorte.com")
    db.add_all([c1, c2])
    db.flush()

    # ── Usuarios ───────────────────────────────
    admin = User(
        id="u0",
        email="dev@devflow.app",
        name="Admin DevFlow",
        hashed_password=hash_password("admin123"),
        role="admin",
        client_id=None,
    )
    client1 = User(
        id="u1",
        email="admin@techpyme.com",
        name="TechPyme Admin",
        hashed_password=hash_password("client123"),
        role="client",
        client_id="c1",
    )
    client2 = User(
        id="u2",
        email="admin@cnorte.com",
        name="Norte Admin",
        hashed_password=hash_password("client456"),
        role="client",
        client_id="c2",
    )
    db.add_all([admin, client1, client2])
    db.flush()

    # ── Tickets (refleja los mocks del Módulo 3) ──
    tickets = [
        Ticket(
            id="T-001", client_id="c1", client_name="TechPyme SA",
            title="Login falla en mobile Safari",
            description="El formulario de login no responde al tap en iPhone 14 con Safari 17.",
            type=TicketTypeEnum.FE,
            page="https://app.techpyme.com/login", page_name="/login",
            priority=PriorityEnum.CRITICAL, status=StatusEnum.approval, eta="2h",
            ai_suggestion=(
                "El problema está en el handler de eventos táctiles. Safari mobile tiene un bug "
                "conocido con `pointer-events` en inputs dentro de formularios con `position: fixed`.\n"
                "1. Cambiar el wrapper del formulario de `position: fixed` a `position: relative`\n"
                "2. Agregar `touch-action: manipulation` al botón de submit\n"
                "3. Verificar que no haya z-index conflictos con el overlay del modal"
            ),
            page_analysis="Página de login con formulario estándar. Se detectó un wrapper con `position: fixed`.",
            code_hints=[
                {"file": "LoginForm.jsx",   "lines": "línea 23",       "description": "Cambiar position del wrapper a relative", "fix": "<div style={{ position: 'relative' }}>"},
                {"file": "LoginButton.jsx", "lines": "entre 10 y 15",  "description": "Agregar touch-action al botón",           "fix": "<button style={{ touchAction: 'manipulation' }}>"},
            ],
            page_fetched=True, auto_executed=False, approved=None,
            pool_position=1,
        ),
        Ticket(
            id="T-002", client_id="c2", client_name="Comercial Norte",
            title="API de reportes timeout en producción",
            description="El endpoint /api/reports/monthly tarda más de 30 segundos y da timeout.",
            type=TicketTypeEnum.BE,
            page="https://norte.app/reportes", page_name="/reportes",
            priority=PriorityEnum.HIGH, status=StatusEnum.queued, eta="4h",
            ai_suggestion=(
                "Query sin índices en tabla `transactions`. Con 500K+ registros el full scan explica el timeout.\n"
                "1. Agregar índice compuesto en `(client_id, created_at)`\n"
                "2. Paginar la query con LIMIT/OFFSET\n"
                "3. Considerar materializar el reporte en background con Celery"
            ),
            page_analysis="Dashboard de reportes con gráficos de barras y tabla de transacciones paginada.",
            code_hints=[
                {"file": "reports.py", "lines": "línea 87", "description": "Agregar índice y paginación",
                 "fix": "results = db.query(Transaction).filter(...).limit(500).offset(page * 500).all()"},
            ],
            page_fetched=True, auto_executed=True, approved=None,
            pool_position=2,
        ),
        Ticket(
            id="T-003", client_id="c1", client_name="TechPyme SA",
            title="Datos duplicados en tabla usuarios",
            description="Aparecen registros duplicados en la tabla de usuarios tras la migración.",
            type=TicketTypeEnum.DB,
            page="https://app.techpyme.com/admin/users", page_name="/admin/users",
            priority=PriorityEnum.HIGH, status=StatusEnum.approval, eta="3h",
            ai_suggestion=(
                "La migración no aplicó UNIQUE constraint en `email`.\n"
                "1. Identificar duplicados con `GROUP BY email HAVING COUNT(*) > 1`\n"
                "2. Mantener el registro más reciente por email\n"
                "3. Aplicar constraint: `ALTER TABLE users ADD CONSTRAINT unique_email UNIQUE (email)`"
            ),
            page_analysis=None,
            code_hints=[
                {"file": "migration_003.sql", "lines": "línea 1", "description": "Limpiar duplicados y agregar constraint",
                 "fix": "DELETE FROM users WHERE id NOT IN (SELECT MAX(id) FROM users GROUP BY email);"},
            ],
            page_fetched=False, auto_executed=False, approved=None,
            pool_position=3,
        ),
        Ticket(
            id="T-004", client_id="c2", client_name="Comercial Norte",
            title="Gráfico de ventas no renderiza en Firefox",
            description="El chart de ventas semanales muestra solo el eje X en Firefox 124.",
            type=TicketTypeEnum.FE,
            page="https://norte.app/dashboard", page_name="/dashboard",
            priority=PriorityEnum.MEDIUM, status=StatusEnum.queued, eta="1h",
            ai_suggestion=(
                "Incompatibilidad de SVG `foreignObject` en Firefox. Recharts usa este elemento.\n"
                "1. Actualizar Recharts a v2.12+\n"
                "2. Si no es posible, deshabilitar `allowEscapeViewBox` en el tooltip"
            ),
            page_analysis="Dashboard con múltiples gráficos Recharts.",
            code_hints=[
                {"file": "SalesChart.jsx", "lines": "entre 45 y 60", "description": "Deshabilitar foreignObject en tooltip",
                 "fix": "<Tooltip allowEscapeViewBox={{ x: false, y: false }} />"},
            ],
            page_fetched=True, auto_executed=True, approved=None,
            pool_position=4,
        ),
        Ticket(
            id="T-005", client_id="c1", client_name="TechPyme SA",
            title="Email de bienvenida no llega a Gmail",
            description="Los correos de bienvenida caen en spam o no llegan en cuentas Gmail.",
            type=TicketTypeEnum.BE,
            page="https://app.techpyme.com/signup", page_name="/signup",
            priority=PriorityEnum.MEDIUM, status=StatusEnum.inprogress, eta="2h",
            ai_suggestion=(
                "Problema de reputación de dominio.\n"
                "1. Verificar registros SPF, DKIM y DMARC en DNS\n"
                "2. Configurar DKIM en el servidor de envío\n"
                "3. Considerar usar SendGrid o Resend para mejor deliverability"
            ),
            page_analysis=None,
            code_hints=[
                {"file": "email_service.py", "lines": "línea 12", "description": "Usar servicio externo con DKIM",
                 "fix": "import sendgrid\nsg = sendgrid.SendGridAPIClient(api_key=os.environ['SENDGRID_KEY'])"},
            ],
            page_fetched=False, auto_executed=True, approved=None,
            pool_position=5,
        ),
        Ticket(
            id="T-006", client_id="c2", client_name="Comercial Norte",
            title="Filtro de fechas ignora timezone",
            description="Al filtrar ventas por fecha, los resultados están desplazados 3 horas.",
            type=TicketTypeEnum.BE,
            page="https://norte.app/ventas", page_name="/ventas",
            priority=PriorityEnum.LOW, status=StatusEnum.queued, eta="45m",
            ai_suggestion=(
                "Las fechas se almacenan en UTC pero el filtro aplica sin conversión.\n"
                "Usar `pytz` para convertir antes de filtrar."
            ),
            page_analysis="Página de ventas con date range picker.",
            code_hints=[
                {"file": "sales_router.py", "lines": "entre 34 y 50", "description": "Convertir fechas a UTC antes de filtrar",
                 "fix": "from pytz import timezone\nARG_TZ = timezone('America/Argentina/Buenos_Aires')"},
            ],
            page_fetched=True, auto_executed=True, approved=None,
            pool_position=6,
        ),
        # Ticket rechazado — para mostrar el estado rejected en la UI
        Ticket(
            id="T-007", client_id="c1", client_name="TechPyme SA",
            title="Cambio de logo en header solicitado",
            description="El cliente pide cambiar el logo del header por una versión actualizada.",
            type=TicketTypeEnum.FE,
            page="https://app.techpyme.com/", page_name="/",
            priority=PriorityEnum.LOW, status=StatusEnum.rejected, eta="30m",
            ai_suggestion="Reemplazar el archivo `logo.svg` en `/public/assets/` y actualizar el import en `Header.jsx`.",
            page_analysis=None,
            code_hints=[
                {"file": "Header.jsx", "lines": "línea 5", "description": "Actualizar import del logo",
                 "fix": "import Logo from '@/assets/logo-v2.svg';"},
            ],
            page_fetched=False, auto_executed=False, approved=False,
            pool_position=None,  # excluido del pool (rejected)
        ),
    ]

    db.add_all(tickets)
    db.commit()
    print(f"[seed] ✓ {len(tickets)} tickets, 3 usuarios y 2 clientes cargados.")
