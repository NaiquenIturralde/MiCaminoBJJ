/* ===== Reproductor del recorrido (Etapa 6A) =====
   Vista previa (miniatura de YouTube) + botón Play. El iframe recién se
   construye al hacer clic: NO hay autoplay en el HTML inicial y no se
   descarga ningún video dentro del proyecto. La URL de embed llega desde
   data-embed, que el servidor arma a partir de un ID validado. */
(function () {
  "use strict";

  function reproducir(contenedor) {
    var embed = contenedor.getAttribute("data-embed");
    var titulo = contenedor.getAttribute("data-title") || "Video de demostración";
    var marco = contenedor.querySelector(".mccjrc-video-frame");

    if (!embed || !marco) {
      return;
    }

    var iframe = document.createElement("iframe");
    iframe.className = "mccjrc-video-iframe";
    iframe.src = embed + (embed.indexOf("?") >= 0 ? "&" : "?") + "autoplay=1&rel=0";
    iframe.title = titulo;
    iframe.setAttribute(
      "allow",
      "accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
    );
    iframe.setAttribute("allowfullscreen", "");
    iframe.setAttribute("loading", "lazy");
    // El reproductor de YouTube exige la cabecera "Referer" en la petición de
    // embed: sin ella responde con "Error 153" (configuración inválida).
    // La política del documento ya es "strict-origin-when-cross-origin", pero
    // se fija también a nivel de marco para no depender de ella.
    iframe.setAttribute("referrerpolicy", "strict-origin-when-cross-origin");

    // La miniatura y el botón Play NO se borran: quedan ocultos por CSS
    // (clase "mccjrc-video-reproduciendo") para que al cambiar de video
    // (ficha de técnica con varios videos) se pueda volver al estado inicial
    // sin volver a descargar la página. El iframe se agrega al final para
    // quedar por encima de la miniatura.
    marco.appendChild(iframe);
    marco.classList.add("mccjrc-video-reproduciendo");
    iframe.focus();
  }

  document.addEventListener("click", function (evento) {
    var destino = evento.target;
    if (!destino || typeof destino.closest !== "function") {
      return;
    }

    var boton = destino.closest("[data-mccjrc-play]");
    if (!boton) {
      return;
    }

    var contenedor = boton.closest("[data-mccjrc-video]");
    if (!contenedor) {
      return;
    }

    evento.preventDefault();
    reproducir(contenedor);
  });
})();
