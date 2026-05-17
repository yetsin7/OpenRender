# Open Render - Plan de Ejecucion Continua

# USA SIEMPRE ESTA IMPORTACION PARA PRUEBAS: "C:\Users\Yetsin\Documents\Arquitectura\Planos de Mi Casa\OpenRender\Mi casa Revit 2026 - Vista 3D - RenderOpenRender.obj"

## Regla operativa de esta hoja

Este plan se ejecuta de forma continua.

- No esperar aprobacion manual entre fases.
- Cuando una fase quede suficientemente estable, iniciar la siguiente de inmediato.
- Si aparece un bloqueo tecnico, resolverlo o dejar fallback seguro y continuar con el siguiente frente util.
- Prioridad absoluta: estabilidad en laptop modesta, interfaz clara, mejora real de utilidad.
- Objetivo de estilo: flujo inspirado en renderizadores arquitectonicos modernos, sin depender de copiar exactamente a Lumion.

## Norte del producto

Open Render debe convertirse en un renderizador arquitectonico:

- estable;
- ligero en RAM;
- con uso de GPU controlado;
- agradable de usar;
- rapido para importar, revisar materiales, encuadrar y exportar.

## Estado actual diagnosticado

### Lo que ya existe

- Shell de escritorio en Avalonia.
- Intento de viewport nativo con Vulkan.
- Biblioteca local, historial de importacion y editor basico de materiales.
- Navegacion y estructura de herramientas tipo estudio de visualizacion.

### Bloqueos reales detectados

- El `NativeControlHost` del viewport tapa la UI cuando se monta como overlay. Solucion parcial: UI fuera del viewport y preview Avalonia cuando el modo seguro esta activo.
- El pipeline Vulkan sigue siendo experimental. Estado actual: contexto/surface/swapchain se inicializan con `OPENRENDER_ENABLE_EXPERIMENTAL_VULKAN_LOOP=1`; `CmdBeginRenderPass` crashea si se activa frame submit, por eso queda detras de `OPENRENDER_ENABLE_VULKAN_FRAME_SUBMIT=1`.
- La integracion UI/viewport ya esta separada por regiones reales, pero falta dibujar geometria real dentro del viewport Vulkan.
- Los perfiles `Laptop`, `Balanced` y `Presentation` ya existen.
- El arranque tiene manifest, fallback seguro y smoke test con modelo oficial.

## Regla de finalizacion por fases

Cada fase se considera completada cuando:

1. Compila.
2. Arranca.
3. No se cae al flujo principal de esa fase.
4. La mejora se ve o se puede probar.
5. Queda anotado el siguiente paso.

## Fases de ejecucion

### Fase 1 - Estabilidad de arranque

Objetivo:

- Hacer que la app abra siempre.
- Evitar que un fallo de Vulkan tumbe la app completa.

Checklist:

- [x] Agregar manifest de Windows al proyecto UI.
- [x] Evitar dependencia dura de `VK_LAYER_KHRONOS_validation`.
- [x] Agregar modo seguro para el viewport.
- [x] Evitar por defecto el host nativo cuando el loop Vulkan experimental este apagado.

### Fase 2 - Shell de editor visible

Objetivo:

- Separar viewport, panel izquierdo, inspector derecho y dock inferior en regiones reales.

Checklist:

- [x] Mover la UI fuera del esquema de overlays sobre el host nativo.
- [x] Hacer visible la biblioteca/import panel.
- [x] Hacer visible el inspector de materiales/camara/render.
- [x] Mantener hints y estados vacios sin depender del viewport nativo en modo seguro.

### Fase 3 - Rendimiento orientado a laptop

Objetivo:

- Dar perfiles claros para equipos modestos.

Checklist:

- [x] Perfil `Laptop Saver`.
- [x] Perfil `Balanced`.
- [x] Perfil `Presentation`.
- [x] Mostrar backend de viewport y presupuesto de carga en la UI.

## Avance actual

- Fase 1 funcional.
- Fase 2 visible en shell con rail, panel izquierdo, inspector derecho y dock inferior.
- Fase 3 funcional con perfiles y estado de backend expuestos en la UI.
- Importador Assimp reactivado: el modelo oficial carga con 3505 objetos, 3,623,690 tris y 198 materiales.
- Exportador software funcional: genera PNG desde la escena en smoke test.
- Vulkan inicializa sin tumbar la app cuando el frame submit queda pausado. El crash pendiente real esta aislado en `Vk.CmdBeginRenderPass`.

### Fase 4 - Utilidad de flujo

Objetivo:

- Mejorar el flujo real de trabajo antes de sofisticar el renderer.

Checklist:

- [x] Importar, reimportar y encuadrar desde un solo flujo base.
- [x] Smoke test con importacion oficial y exportaciones `final`, `front`, `top`.
- [ ] Mejorar seleccion de objeto/material.
- [x] Hacer mas claro el estado de escena, materiales y salida.
- [x] Preparar vista de presentacion rapida.

### Fase 5 - Render backend estable

Objetivo:

- Volver a encender el backend Vulkan paso a paso sin sacrificar estabilidad.

Checklist:

- [x] Validar inicializacion de contexto, surface y swapchain bajo `OPENRENDER_ENABLE_EXPERIMENTAL_VULKAN_LOOP=1`.
- [ ] Validar render pass principal. Bloqueo actual: `Vk.CmdBeginRenderPass` produce `0xC0000005`.
- [x] Encender preview experimental bajo bandera sin usarlo por defecto.
- [x] Medir crash principal y aislarlo detras de `OPENRENDER_ENABLE_VULKAN_FRAME_SUBMIT=1`.

### Fase 6 - Optimizacion de recursos

Objetivo:

- Mantener baja RAM y usar GPU solo donde realmente aporte.

Checklist:

- [ ] Instancing real para objetos repetidos.
- [ ] Carga diferida de assets.
- [ ] Texturas compactas y catalogo de resolucion.
- [ ] Liberacion de buffers CPU tras subir geometria a GPU.
- [ ] Presets de resolucion y calidad con presupuesto claro.

### Fase 7 - Capacidad visual incremental

Objetivo:

- Aumentar realismo sin disparar costos.

Checklist:

- [ ] Sol y ambiente mas consistentes.
- [ ] Materiales PBR mas utiles para arquitectura.
- [ ] Mejoras graduales de postproceso.
- [ ] Preview de foto arquitectonica mas limpia.

## Secuencia obligatoria

Mientras este plan siga vigente:

1. Completar lo que falte de la fase actual.
2. Marcar el avance.
3. Empezar la siguiente fase sin esperar aprobacion.
4. Si algo falla, dejar fallback seguro y continuar con otra mejora util.

## Proxima ejecucion inmediata

- Corregir `CmdBeginRenderPass` con validation/debug utils o reemplazar el render pass inicial por una ruta Vulkan aun mas minima verificada.
- Conectar geometria real del modelo oficial al viewport GPU solo despues de que el present pass no crashee.
- Mejorar apariencia del viewport Vulkan pausado para que no parezca vacio mientras se depura frame submit.
- Mantener siempre el smoke test con el OBJ oficial y exportaciones en `artifacts`.
