/**
 * Mostrar / ocultar contraseña (Etapa 4C).
 *
 * Comportamiento:
 * - Alterna input[type=password] ↔ input[type=text] sin enviar nada al
 *   servidor ni guardar el valor en el almacenamiento del navegador.
 * - Accesible por teclado (es un <button type="button">) con aria-label
 *   dinámico y aria-pressed reflejando el estado.
 * - El foco permanece en el botón tras alternar.
 * - Sin dependencias: vanilla JS; se carga solo en Registro y Login.
 */
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var botones = document.querySelectorAll('[data-password-toggle]');

        botones.forEach(function (boton) {
            boton.addEventListener('click', function () {
                var selector = boton.getAttribute('data-password-toggle');
                var input = selector ? document.querySelector(selector) : null;
                if (!input) return;

                var oculta = input.type === 'password';
                input.type = oculta ? 'text' : 'password';

                boton.setAttribute('aria-pressed', oculta ? 'true' : 'false');
                boton.setAttribute('aria-label', oculta ? 'Ocultar contraseña' : 'Mostrar contraseña');
                // El foco sigue en el botón (no se mueve ni se pierde).
            });
        });
    });
})();
