// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Etapa 1 — menú desplegable del avatar (encabezado).
// Comportamiento accesible: abre/cierra con Escape (devolviendo el foco al
// avatar), al pulsar fuera y al elegir una opción; flechas/Home/End entre las
// opciones habilitadas; aria-expanded siempre sincronizado. Solo lectura del
// DOM: sin envíos, sin almacenamiento y sin dependencias nuevas.
(function () {
  "use strict";

  var boton = document.getElementById("mccjUserMenuBtn");
  var menu = document.getElementById("mccjUserMenu");
  if (!boton || !menu) return; // rama visitante: no hay menú

  var contenedor = boton.parentElement; // .mccj-user-menu-wrap

  function opciones() {
    return Array.prototype.filter.call(
      menu.querySelectorAll('[role="menuitem"]'),
      function (item) {
        // Las opciones "Próximamente" están deshabilitadas y no reciben foco.
        return !item.disabled && item.getAttribute("aria-disabled") !== "true";
      }
    );
  }

  function abierto() {
    return boton.getAttribute("aria-expanded") === "true";
  }

  function abrir() {
    if (abierto()) return;
    menu.hidden = false;
    boton.setAttribute("aria-expanded", "true");
    var primera = opciones()[0];
    if (primera) primera.focus();
  }

  function cerrar(devolverFoco) {
    if (!abierto()) return;
    menu.hidden = true;
    boton.setAttribute("aria-expanded", "false");
    if (devolverFoco) boton.focus();
  }

  boton.addEventListener("click", function () {
    if (abierto()) cerrar(false);
    else abrir();
  });

  // Escape: cierra el menú y devuelve el foco al avatar.
  document.addEventListener("keydown", function (evento) {
    if (evento.key === "Escape" && abierto()) {
      evento.preventDefault();
      cerrar(true);
    }
  });

  // Clic fuera del bloque del menú: cierra sin robar el foco.
  document.addEventListener("click", function (evento) {
    if (abierto() && contenedor && !contenedor.contains(evento.target)) {
      cerrar(false);
    }
  });

  // Al seleccionar cualquier opción (enlace o botón de envío) se cierra.
  menu.addEventListener("click", function (evento) {
    if (evento.target.closest && evento.target.closest('[role="menuitem"]')) {
      cerrar(false);
    }
  });

  // Navegación con teclado dentro del menú (patrón ARIA menu).
  menu.addEventListener("keydown", function (evento) {
    var lista = opciones();
    if (!lista.length) return;

    var actual = lista.indexOf(document.activeElement);
    var destino = null;

    switch (evento.key) {
      case "ArrowDown":
        destino = lista[(actual + 1) % lista.length];
        break;
      case "ArrowUp":
        destino = lista[(actual <= 0 ? lista.length : actual) - 1];
        break;
      case "Home":
        destino = lista[0];
        break;
      case "End":
        destino = lista[lista.length - 1];
        break;
      default:
        return;
    }

    evento.preventDefault();
    destino.focus();
  });

  // Tab fuera del menú: se cierra (el foco sigue su curso normal).
  if (contenedor) {
    contenedor.addEventListener("focusout", function (evento) {
      if (abierto() && evento.relatedTarget && !contenedor.contains(evento.relatedTarget)) {
        cerrar(false);
      }
    });
  }
})();

// Etapa UI/UX — toast de login exitoso: se auto-oculta tras unos segundos
// con una fundida breve y puede cerrarse con su botón. Solo toca el DOM de
// un elemento propio: sin envíos, sin almacenamiento y sin dependencias.
(function () {
  "use strict";

  var toast = document.getElementById("mccjToastBienvenida");
  if (!toast) return;

  var ocultando = false;

  function ocultar() {
    if (ocultando) return;
    ocultando = true;
    toast.classList.add("is-saliendo");
    window.setTimeout(function () {
      if (toast.parentNode) toast.parentNode.removeChild(toast);
    }, 320);
  }

  var botonCerrar = toast.querySelector(".mccj-toast-cerrar");
  if (botonCerrar) {
    botonCerrar.addEventListener("click", ocultar);
  }

  window.setTimeout(ocultar, 3500);
})();
