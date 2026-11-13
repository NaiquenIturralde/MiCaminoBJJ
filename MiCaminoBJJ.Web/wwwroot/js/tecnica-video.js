/* ===== Reproductor de la ficha de técnica (Biblioteca) =====
   Complemento de recorrido-video.js.

   recorrido-video.js crea el iframe al pulsar Play (reproducción diferida).
   Este archivo resuelve la SELECCIÓN entre varios videos de la misma técnica:

   - el reproductor principal (el único iframe de la página) cambia de embed,
     miniatura, título, descripción y enlace canónico sin salir de la ficha;
   - al cambiar de video se descarta el iframe anterior: en todo momento hay
     como máximo UN reproductor cargado (nunca todos en paralelo);
   - la miniatura activa queda marcada visualmente y con aria-current.

   No depende de frameworks ni de librerías externas. */
(function () {
  "use strict";

  var SELECTOR_RAIZ = "[data-mccj-tecnica-video]";
  var SELECTOR_MINIATURA = "[data-mccj-tecnica-select]";

  function leerDatos(miniatura) {
    return {
      embed: miniatura.getAttribute("data-embed") || "",
      titulo: miniatura.getAttribute("data-title") || "Video de demostración",
      poster: miniatura.getAttribute("data-poster") || "",
      url: miniatura.getAttribute("data-url") || "",
      descripcion: miniatura.getAttribute("data-description") || ""
    };
  }

  function marcarActiva(raiz, miniatura) {
    var miniaturas = raiz.querySelectorAll(SELECTOR_MINIATURA);
    for (var i = 0; i < miniaturas.length; i++) {
      var activa = miniaturas[i] === miniatura;
      miniaturas[i].classList.toggle("is-activa", activa);
      if (activa) {
        miniaturas[i].setAttribute("aria-current", "true");
      } else {
        miniaturas[i].removeAttribute("aria-current");
      }
    }
  }

  function actualizarEtapa(raiz, datos) {
    var etapa = raiz.querySelector("[data-mccjrc-video]");
    if (!etapa) {
      return;
    }

    etapa.setAttribute("data-embed", datos.embed);
    etapa.setAttribute("data-title", datos.titulo);
    if (datos.poster) {
      etapa.setAttribute("data-poster", datos.poster);
    }
    if (datos.url) {
      etapa.setAttribute("data-url", datos.url);
    }

    // Se descarta el reproductor anterior y se vuelve al estado inicial
    // (miniatura + botón Play). El nuevo video solo se carga cuando el
    // visitante vuelve a pulsar Reproducir.
    var marco = etapa.querySelector(".mccjrc-video-frame");
    if (marco) {
      var iframe = marco.querySelector("iframe");
      if (iframe) {
        iframe.parentNode.removeChild(iframe);
      }
      marco.classList.remove("mccjrc-video-reproduciendo");
    }

    var poster = etapa.querySelector("[data-mccj-tecnica-poster]");
    if (poster && datos.poster) {
      poster.setAttribute("src", datos.poster);
      poster.setAttribute("alt", "Vista previa del video: " + datos.titulo);
    }

    var boton = etapa.querySelector("[data-mccjrc-play]");
    if (boton) {
      boton.setAttribute("aria-label", "Reproducir el video: " + datos.titulo);
    }

    var enlace = etapa.querySelector("[data-mccj-tecnica-enlace]");
    if (enlace && datos.url) {
      enlace.setAttribute("href", datos.url);
    }

    var nota = etapa.querySelector("[data-mccj-tecnica-nota]");
    if (nota) {
      nota.textContent = datos.titulo;
    }

    var descripcion = etapa.querySelector("[data-mccj-tecnica-descripcion]");
    if (descripcion) {
      if (datos.descripcion) {
        descripcion.textContent = datos.descripcion;
        descripcion.classList.remove("d-none");
      } else {
        descripcion.textContent = "";
        descripcion.classList.add("d-none");
      }
    }
  }

  function seleccionar(miniatura) {
    var raiz = miniatura.closest(SELECTOR_RAIZ);
    if (!raiz) {
      return;
    }

    var datos = leerDatos(miniatura);
    if (!datos.embed) {
      return;
    }

    actualizarEtapa(raiz, datos);
    marcarActiva(raiz, miniatura);

    var etapa = raiz.querySelector("[data-mccjrc-video]");
    if (etapa && typeof etapa.scrollIntoView === "function") {
      etapa.scrollIntoView({ behavior: "smooth", block: "center" });
    }
  }

  document.addEventListener("click", function (evento) {
    var destino = evento.target;
    if (!destino || typeof destino.closest !== "function") {
      return;
    }

    var miniatura = destino.closest(SELECTOR_MINIATURA);
    if (!miniatura) {
      return;
    }

    evento.preventDefault();
    seleccionar(miniatura);
  });
})();
