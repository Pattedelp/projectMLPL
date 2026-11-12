/* Fantasmita de la B: de vez en cuando aparece flotando sobre los últimos
   4 puestos de la mini tabla de Primera División (home).
   Solo animación visual: no toca datos ni bloquea clics. */
(function () {
    'use strict';

    if (!Element.prototype.animate) return;
    if (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

    var wrappers = document.querySelectorAll('[data-fantasma-b]');
    if (!wrappers.length) return;

    var cfg = window.FANTASMA_B || {};
    var PRIMERA_VEZ = cfg.primeraVez || [6000, 15000];   // ms hasta la primera aparición
    var ENTRE_VECES = cfg.entreVeces || [25000, 60000];  // ms entre apariciones
    var DURACION    = cfg.duracion    || [6500, 9500];   // ms que dura cada recorrido
    var FILAS       = 4;                                 // últimos puestos que ronda
    var OPACIDAD    = 0.85;

    var estilo = document.createElement('style');
    estilo.textContent =
        '[data-fantasma-b]{position:relative}' +
        '.fantasma-b{position:absolute;left:0;top:0;opacity:0;pointer-events:none;z-index:2;' +
        'user-select:none;will-change:transform,opacity;' +
        'filter:drop-shadow(0 0 6px rgba(160,200,255,.55));' +
        '-webkit-mask-image:linear-gradient(to right,transparent,#000 10%,#000 90%,transparent);' +
        'mask-image:linear-gradient(to right,transparent,#000 10%,#000 90%,transparent)}';
    document.head.appendChild(estilo);

    Array.prototype.forEach.call(wrappers, iniciar);

    function azar(rango) {
        return rango[0] + Math.random() * (rango[1] - rango[0]);
    }

    function iniciar(wrap) {
        var img = new Image();
        img.src = wrap.getAttribute('data-fantasma-b');
        img.alt = '';
        img.className = 'fantasma-b';
        img.draggable = false;
        img.setAttribute('aria-hidden', 'true');
        wrap.appendChild(img);

        programar(azar(PRIMERA_VEZ));

        function programar(ms) {
            setTimeout(intentar, ms);
        }

        function intentar() {
            if (img.complete && !img.naturalWidth) return;           // la imagen no cargó: no insistir
            var filas = wrap.querySelectorAll('tbody tr');
            var caja = wrap.getBoundingClientRect();
            var fueraDePantalla = caja.bottom < 0 || caja.top > window.innerHeight;
            if (document.hidden || fueraDePantalla || !filas.length || !img.naturalWidth) {
                return programar(5000);                               // reintenta más tarde
            }
            recorrer(filas);
        }

        function recorrer(filas) {
            var desde = Math.max(0, filas.length - FILAS);
            var rWrap = wrap.getBoundingClientRect();
            var rIni = filas[desde].getBoundingClientRect();
            var rFin = filas[filas.length - 1].getBoundingClientRect();
            var zonaTop = rIni.top - rWrap.top + wrap.scrollTop;
            var zonaAlto = rFin.bottom - rIni.top;

            var alto = Math.max(34, Math.min(72, zonaAlto * 0.65));
            var ancho = alto * img.naturalWidth / img.naturalHeight;
            var recorrido = wrap.clientWidth - ancho;
            if (recorrido < 20) return programar(azar(ENTRE_VECES));

            img.style.width = ancho + 'px';
            img.style.height = alto + 'px';

            var haciaDerecha = Math.random() < 0.5;
            var vueltas = 0.8 + Math.random() * 0.7;                  // cuántas ondas hace al cruzar
            var fase = Math.random() * Math.PI * 2;
            var margenY = Math.max(0, zonaAlto - alto);
            var PASOS = 40;
            var frames = [];

            for (var i = 0; i <= PASOS; i++) {
                var t = i / PASOS;
                var x = (haciaDerecha ? t : 1 - t) * recorrido + wrap.scrollLeft;
                var y = zonaTop + margenY * (0.5 + 0.5 * Math.sin(fase + Math.PI * 2 * vueltas * t));
                var op = t < 0.1 ? t / 0.1 : (t > 0.88 ? (1 - t) / 0.12 : 1);
                frames.push({
                    transform: 'translate(' + x.toFixed(1) + 'px,' + y.toFixed(1) + 'px)',
                    opacity: (op * OPACIDAD).toFixed(3)
                });
            }

            var anim = img.animate(frames, { duration: azar(DURACION), easing: 'linear' });
            anim.onfinish = function () { programar(azar(ENTRE_VECES)); };
        }
    }
})();
