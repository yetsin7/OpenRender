# Open Render -> Lumion-like Evolution Plan

Fecha: 2026-05-16

## Objetivo real

No intentar clonar Lumion completo de una vez.

La meta correcta es acercar Open Render a ese flujo por capas:

1. Importar proyecto sin fricción.
2. Navegar la escena en tiempo real.
3. Reasignar y ajustar materiales con velocidad.
4. Preparar encuadres y exportar imágenes.
5. Añadir entorno, efectos, biblioteca y sincronización en vivo.

## Qué quedó listo en esta iteración

- Shell visual de estudio con tema oscuro.
- Escena demo cargada por defecto para evitar viewport vacío.
- Inspector con foco en escena, material, cámara, foto y entorno.
- Navegación de cámara más consistente usando distancia orbital real.
- Presets rápidos de materiales.
- Presets de resolución, calidad y formato de salida.
- Presets de entorno base.
- Exportación de imagen del viewport a PNG/JPEG/BMP/TIFF.

## Brecha principal actual

### 1. Importación

Actual:
- OBJ sólido.
- Opciones básicas de orientación y recentrado.

Falta:
- FBX.
- glTF / GLB.
- IFC.
- Separación más inteligente por materiales y jerarquías.
- Reimportación del mismo archivo sin rehacer la escena manualmente.

### 2. Materiales

Actual:
- PBR básico.
- Presets manuales.
- Ajustes de albedo, roughness, metallic y opacity.

Falta:
- Texturas albedo/normal/roughness/metallic/AO.
- Biblioteca visual de materiales.
- Arrastrar y soltar materiales sobre mallas.
- Escala/rotación UV.
- Favoritos y materiales recientes.

### 3. Viewport y escena

Actual:
- Orbit, look, pan, fly y encuadre.
- ViewCube y vistas ortográficas.
- Grid y fondo configurable.

Falta:
- Selección directa de mallas en viewport.
- Gizmos de mover/rotar/escalar.
- Culling y streaming para escenas pesadas.
- Navegación con presets de velocidad según tamaño del proyecto.

### 4. Foto / render

Actual:
- Preview en tiempo real.
- Exportación de still desde el framebuffer.

Falta:
- Photo mode con múltiples cámaras guardadas.
- Safe frame y aspect ratios arquitectónicos.
- Exposure, white balance, contrast, bloom y vignette.
- Sombras más avanzadas, SSAO y reflections.
- Cola de exportación con lotes.

### 5. Entorno

Actual:
- Sol direccional y ambiente.
- Presets base de día, nublado, sunset y studio.

Falta:
- Control horario real.
- HDRI.
- Niebla, cielo volumétrico y clima.
- Luces artificiales editables desde UI.

## Orden recomendado de construcción

### Fase 1

Import pipeline serio:
- glTF/GLB.
- FBX.
- reimport.
- scene tree usable para modelos reales.

### Fase 2

Material workflow:
- texturas.
- thumbnails.
- reasignación rápida por objeto/material.

### Fase 3

Photo mode:
- cámaras guardadas.
- parámetros fotográficos.
- export batch.

### Fase 4

Calidad visual:
- sombras mejores.
- ambient occlusion.
- reflections.
- postprocesado.

### Fase 5

Escalado de producto:
- librería de assets.
- presets reutilizables.
- sincronización tipo LiveSync.

## Próxima acción recomendada

La siguiente iteración debe atacar esto:

1. Importador glTF/GLB.
2. Selección directa de malla en viewport.
3. Texturas PBR reales en materiales.
4. Guardado de cámaras para stills.

Ese es el siguiente bloque que más acerca la experiencia a un flujo tipo Lumion.
