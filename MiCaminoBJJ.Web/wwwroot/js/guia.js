// Guía del Practicante — conversación (Etapa 5).
// Reglas: sin HTML del servidor (textContent → anti-XSS), token antiforgery
// por header, mensajes de error claros y estado de espera visible.
(function () {
  'use strict';

  var form = document.getElementById('guia-form');
  if (!form) return;

  var area = document.getElementById('guia-conversacion');
  var input = document.getElementById('guia-texto');
  var boton = document.getElementById('guia-enviar');
  var esperando = document.getElementById('guia-esperando');
  var cajaError = document.getElementById('guia-error');
  var tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
  var token = tokenInput ? tokenInput.value : '';
  var enProceso = false;

  function agregarMensaje(rol, texto, esError) {
    var envoltorio = document.createElement('div');
    envoltorio.className = 'mccj-chat-msg mccj-chat-msg--' + (esError ? 'error' : rol);

    var etiqueta = document.createElement('span');
    etiqueta.className = 'mccj-chat-rol';
    etiqueta.textContent = esError ? 'Error' : (rol === 'agente' ? 'Guía' : 'Vos');

    var burbuja = document.createElement('div');
    burbuja.className = 'mccj-chat-bubble';
    burbuja.textContent = texto;

    envoltorio.appendChild(etiqueta);
    envoltorio.appendChild(burbuja);
    area.appendChild(envoltorio);
    area.scrollTop = area.scrollHeight;
  }

  function mostrarError(mensaje) {
    cajaError.textContent = mensaje;
    cajaError.classList.remove('d-none');
    agregarMensaje('agente', mensaje, true);
  }

  function limpiarError() {
    cajaError.textContent = '';
    cajaError.classList.add('d-none');
  }

  function estadoEnviando(activo) {
    enProceso = activo;
    boton.disabled = activo;
    input.disabled = activo;
    if (activo) {
      esperando.classList.remove('d-none');
      esperando.classList.add('d-flex');
    } else {
      esperando.classList.add('d-none');
      esperando.classList.remove('d-flex');
    }
  }

  async function enviar(texto) {
    if (enProceso) return;
    limpiarError();

    if (!texto) {
      mostrarError('Escribí una pregunta antes de enviarla.');
      input.focus();
      return;
    }

    if (texto.length > 1000) {
      mostrarError('La pregunta no puede superar los 1000 caracteres.');
      return;
    }

    agregarMensaje('practicante', texto);
    estadoEnviando(true);

    try {
      var respuesta = await fetch('/GuiaPracticante/Consultar', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'RequestVerificationToken': token
        },
        body: JSON.stringify({ texto: texto })
      });

      var datos = null;
      try {
        datos = await respuesta.json();
      } catch (e) {
        datos = null;
      }

      if (!respuesta.ok) {
        mostrarError(datos && datos.texto
          ? datos.texto
          : 'No se pudo procesar la consulta (error ' + respuesta.status + '). Intentá nuevamente.');
        return;
      }

      if (!datos || !datos.texto) {
        mostrarError('La guía no devolvió contenido. Intentá nuevamente.');
        return;
      }

      if (datos.nota) {
        // Informativa (p. ej. fallback a catálogo): se muestra como mensaje del guía.
        agregarMensaje('agente', datos.nota);
      }
      agregarMensaje('agente', datos.texto);
    } catch (e) {
      mostrarError('No se pudo conectar con la guía. Revisá tu conexión e intentá nuevamente.');
    } finally {
      estadoEnviando(false);
      input.focus();
    }
  }

  form.addEventListener('submit', function (evento) {
    evento.preventDefault();
    var texto = (input.value || '').trim();
    enviar(texto);
  });

  input.addEventListener('keydown', function (evento) {
    if (evento.key === 'Enter' && !evento.shiftKey) {
      evento.preventDefault();
      form.requestSubmit();
    }
  });

  document.querySelectorAll('.mccj-chat-sugerencia').forEach(function (botonSugerencia) {
    botonSugerencia.addEventListener('click', function () {
      input.value = botonSugerencia.getAttribute('data-texto') || '';
      form.requestSubmit();
    });
  });
})();
