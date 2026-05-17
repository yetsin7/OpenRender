# OpenRender x Lumion Alignment Notes

# USA SIEMPRE ESTA IMPORTACION PARA PRUEBAS: "C:\Users\Yetsin\Documents\Arquitectura\Planos de Mi Casa\OpenRender\Mi casa Revit 2026 - Vista 3D - RenderOpenRender.obj"

Fecha: 2026-05-16

## Referencias oficiales revisadas

- Lumion Support: Importing and Working with 3D Models
  - https://support.lumion.com/hc/en-us/articles/12193299343260-Importing-and-Working-with-3D-Models
- Lumion Support: Materials Workflow
  - https://support.lumion.com/hc/en-us/articles/12213010162460-Materials-Workflow
- Lumion Support: Why are your Lumion materials missing after re-importing your model?
  - https://support.lumion.com/hc/en-us/articles/360008179854-Why-are-your-Lumion-materials-missing-after-re-importing-your-model
- Lumion Support: What do the properties of the Standard Material mean in Lumion 2023 and newer?
  - https://support.lumion.com/hc/en-us/articles/7764034284188-What-do-the-properties-of-the-Standard-Material-mean-in-Lumion-2023-and-newer
- Lumion Support: How do Auto-Converted Materials work in Lumion?
  - https://support.lumion.com/hc/en-us/articles/17389896136604
- Lumion Support: How do you use the Ray Tracing Effect?
  - https://support.lumion.com/hc/en-us/articles/7442305609628

## Qué hace Lumion que debemos imitar

1. Guarda los modelos importados en una biblioteca reutilizable.
2. Permite reimportar el mismo archivo sin rehacer el trabajo manual.
3. Usa el nombre del material importado como ancla para re-aplicar materiales.
4. Hace auto-conversión inicial de categorías comunes como vidrio, concreto, metal, madera, piedra y cerámica.
5. Mantiene el flujo de foto separado del flujo de modelado: importar, materializar, encuadrar, renderizar.

## Qué quedó implementado en OpenRender en esta iteración

1. Biblioteca local persistente en `Documents/OpenRender/Library/studio-library.json`.
2. Historial visible de archivos importados con nombre, conteos y ruta.
3. Reimportación rápida del archivo actual desde la UI.
4. Conservación de overrides de materiales por superficie y por nombre de material importado.
5. Detección y exposición del `SourceName` del material importado para no perder el vínculo al reimportar.
6. Auto-conversión extra para superficies transparentes hacia preset de vidrio, alineado con el comportamiento esperado en flujos tipo Lumion.
7. Render con mapas PBR locales para `albedo`, `normal`, `roughness` y `ambient occlusion`.
8. Catálogo local de texturas CC0 aplicado automáticamente por `PresetKey`.
9. Backfill de proyectos anteriores: si un override viejo no tenía rutas de textura, OpenRender las regenera desde el preset en la siguiente importación.
10. Lectura inicial de `map_Kd`, `map_bump` / `bump` / `norm` y `map_Pr` desde archivos `.mtl`.
11. Controles fotográficos en viewport para `Exposure`, `Gamma`, `Contrast` y `White Balance`, con presets base por entorno.

## Biblioteca de texturas añadida

Fuente de descarga usada en esta iteración:

- ambientCG (CC0): https://ambientcg.com/

Sets descargados y conectados:

1. `Concrete046`
2. `Travertine008`
3. `WoodFloor062`
4. `WoodFloor046`
5. `Tiles002`
6. `Grass002`
7. `RoofingTiles013A`
8. `Bricks059`

Mapeo inicial automático:

1. `concrete-polished`, `concrete-block` -> `Concrete046`
2. `stone-cantera`, `stone-travertine` -> `Travertine008`
3. `wood-oak` -> `WoodFloor062`
4. `wood-walnut` -> `WoodFloor046`
5. `ceramic-ivory` -> `Tiles002`
6. `landscape-grass` -> `Grass002`
7. `roof-terracotta` -> `RoofingTiles013A`
8. `brick-red` -> `Bricks059`

## Caso de prueba usado

- `C:\Users\Yetsin\Documents\Arquitectura\Planos de Mi Casa\OpenRender\Mi casa Revit 2026 - Vista 3D - RenderOpenRender.obj`
- Con `.mtl` asociado y múltiples nombres de materiales provenientes de Revit.

## Próximo bloque de mayor impacto

1. Slots de foto/cámaras guardadas estilo Photo Mode.
2. Bloom, vignette y saturación para terminar el look de still.
3. Reflejos y sombras más avanzadas para acercarnos a fotos realmente realistas.
4. UI para ver, cambiar y escalar mapas de textura manualmente desde el inspector.
