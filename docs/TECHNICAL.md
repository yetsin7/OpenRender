# Open Render — Documentación Técnica

## Índice

1. [Visión General](#visión-general)
2. [Arquitectura del Sistema](#arquitectura-del-sistema)
3. [Módulos del Proyecto](#módulos-del-proyecto)
4. [Pipeline de Renderizado](#pipeline-de-renderizado)
5. [Sistema de Materiales PBR](#sistema-de-materiales-pbr)
6. [Sistema de Importación](#sistema-de-importación)
7. [Interfaz de Usuario](#interfaz-de-usuario)
8. [Guía de Compilación](#guía-de-compilación)
9. [Próximos Pasos](#próximos-pasos)

---

## Visión General

Open Render es un renderizador arquitectónico diseñado con tres principios fundamentales:

1. **Ligereza** — bajo consumo de RAM y GPU
2. **Simplicidad** — interfaz intuitiva para arquitectos
3. **Calidad** — renders fotográficos con materiales PBR

### Stack Tecnológico

| Capa | Tecnología | Versión |
|---|---|---|
| Runtime | .NET | 10.0 |
| UI Framework | Avalonia UI | 11.2.3 |
| Gráficos | Silk.NET (OpenGL 3.3) | 2.21.0 |
| MVVM | CommunityToolkit.Mvvm | 8.4.0 |
| Imágenes | SixLabors.ImageSharp | 3.1.7 |
| Fuente | Inter (via Avalonia.Fonts.Inter) | — |

### Decisiones de Diseño

**¿Por qué OpenGL en vez de Vulkan para la Fase 1?**

Las instrucciones originales recomiendan Vulkan, pero para la primera versión funcional se eligió OpenGL 3.3 por estas razones:
- Vulkan requiere ~1500 líneas de código solo para inicializar
- OpenGL permite iterar más rápido en la fase inicial
- Silk.NET abstrae ambas APIs, facilitando la migración futura
- La arquitectura está diseñada para intercambiar el backend gráfico

**¿Por qué Avalonia y no WPF?**

Siguiendo la recomendación del documento original (Opción 1):
- Multiplataforma (Windows, Linux, macOS)
- Moderna y con rendimiento nativo
- Tema Fluent incluido
- Ecosistema activo

---

## Arquitectura del Sistema

### Diagrama de Capas

```
┌───────────────────────────────────────────────┐
│                OpenRender (UI)                │
│  ┌──────────┐  ┌──────────┐  ┌─────────────┐ │
│  │  Views   │  │ViewModels│  │  App Config  │ │
│  │ (AXAML)  │  │ (MVVM)   │  │   (Theme)   │ │
│  └────┬─────┘  └────┬─────┘  └─────────────┘ │
│       │              │                         │
├───────┼──────────────┼─────────────────────────┤
│       ▼              ▼                         │
│  ┌────────────────────────────────────────┐    │
│  │        OpenRender.Rendering            │    │
│  │  ┌──────────┐  ┌──────────────────┐    │    │
│  │  │ Scene    │  │ Shader Pipeline  │    │    │
│  │  │ Renderer │  │ (GLSL PBR)       │    │    │
│  │  └────┬─────┘  └────┬─────────────┘    │    │
│  │       │              │                  │    │
│  │  ┌────┴─────┐  ┌────┴──────┐           │    │
│  │  │ GPU Mesh │  │ Viewport  │           │    │
│  │  │ (VAO/VBO)│  │ Window    │           │    │
│  │  └──────────┘  └───────────┘           │    │
│  │  ┌──────────┐  ┌──────────────────┐    │    │
│  │  │ Import   │  │ Primitives       │    │    │
│  │  │ Manager  │  │ (Cube,Plane,Grid)│    │    │
│  │  └──────────┘  └──────────────────┘    │    │
│  └────────────────────────────────────────┘    │
│                      │                         │
├──────────────────────┼─────────────────────────┤
│                      ▼                         │
│  ┌────────────────────────────────────────┐    │
│  │          OpenRender.Core               │    │
│  │  ┌──────────┐  ┌──────────────────┐    │    │
│  │  │ Scene3D  │  │ PbrMaterial      │    │    │
│  │  │ SceneNode│  │ LightSource      │    │    │
│  │  │ MeshData │  │ Camera           │    │    │
│  │  └──────────┘  └──────────────────┘    │    │
│  │  ┌──────────┐  ┌──────────────────┐    │    │
│  │  │ Import   │  │ RenderSettings   │    │    │
│  │  │ Interface│  │                  │    │    │
│  │  └──────────┘  └──────────────────┘    │    │
│  └────────────────────────────────────────┘    │
└───────────────────────────────────────────────┘
```

### Flujo de Datos

```
Archivo 3D (.obj)
    │
    ▼
IModelImporter.ImportAsync()
    │
    ▼
Scene3D (escena completa)
    ├── SceneNode[] (jerarquía de objetos)
    │   └── MeshData (geometría)
    ├── PbrMaterial[] (materiales)
    ├── LightSource[] (luces)
    └── Camera (cámara orbital)
    │
    ▼
SceneRenderer.Render()
    │
    ├── Compila shaders GLSL
    ├── Sube meshes a GPU (GpuMesh)
    ├── Configura uniforms (materiales, luces, cámara)
    └── Dibuja frame
```

---

## Módulos del Proyecto

### OpenRender.Core

Contiene las abstracciones y modelos de dominio sin dependencias de renderizado.

#### Scene/Scene3D.cs
Contenedor raíz de la escena. Agrega nodos, materiales, luces y cámara.
- `GetAllNodes()` — Traversal DFS del grafo
- `GetTotalTriangleCount()` — Estadísticas de la escena

#### Scene/SceneNode.cs
Nodo del grafo de escena con transformación (position, rotation, scale).
- `GetLocalTransform()` — Genera la matriz 4x4 de transformación

#### Scene/MeshData.cs
Datos de geometría: vértices, normales, UVs, índices.
- `GetInterleavedData()` — Prepara datos para upload a GPU
- `ComputeBoundingBox()` — AABB para encuadre de cámara

#### Scene/Camera.cs
Cámara orbital alrededor de un punto target.
- `Orbit(yaw, pitch)` — Rotación
- `Pan(dx, dy)` — Paneo en plano local
- `Zoom(delta)` — Acercamiento/alejamiento
- `FrameBoundingBox(min, max)` — Auto-encuadre

#### Scene/PbrMaterial.cs
Material PBR con workflow metallic-roughness.
- Presets: Default, Concrete, Glass, Metal, Wood
- Soporte para texturas (paths, resueltas en render time)

#### Scene/LightSource.cs
Fuentes de luz: Directional (sol), Point, Spot.
- `CreateSun()` — Crea luz solar por defecto
- `CreatePointLight()` — Crea luz puntual

### OpenRender.Rendering

Motor de renderizado basado en Silk.NET + OpenGL.

#### SceneRenderer.cs
Renderizador principal. Coordina shaders, meshes, y estado OpenGL.
- `Initialize()` — Compila shaders, crea grid, configura GL state
- `Render(scene, width, height)` — Dibuja un frame completo

#### GpuMesh.cs
Wrapper para buffers de GPU (VAO, VBO, EBO).
- Sube datos interleaved al GPU
- Configura layout de atributos de vértice

#### Shaders/ShaderProgram.cs
Compila y gestiona programas shader de OpenGL.
- Cache de ubicaciones de uniforms
- Métodos tipados: `SetVec3`, `SetMat4`, `SetFloat`

#### Shaders/ShaderSources.cs
Código GLSL embebido:
- **Vertex shader**: Transforma vértices, pasa normales/posiciones
- **Fragment shader**: Blinn-Phong con tone mapping Reinhard
- **Grid shaders**: Plano de referencia con desvanecimiento

#### ViewportWindow.cs
Ventana standalone de OpenGL para testing del motor.
- Controles de mouse: orbit, pan, zoom
- Carga de archivos OBJ

#### Import/ObjImporter.cs
Parser de archivos Wavefront OBJ.
- Soporta: v, vn, vt, f (con triangulación)
- Genera normales si faltan
- Auto-encuadra la cámara al modelo

#### Import/ImportManager.cs
Registro de importadores. Selecciona el correcto por extensión.

#### Primitives/PrimitiveGenerator.cs
Genera geometría procedural:
- `CreateCube()` — Cubo unitario
- `CreatePlane()` — Plano de suelo
- `CreateGrid()` — Grid de referencia
- `CreateArchBox()` — Caja arquitectónica paramétrica

### OpenRender (UI)

Aplicación Avalonia UI con patrón MVVM.

#### Views/MainWindow.axaml
Layout principal con tema claro (blanco):
- **Top bar**: Logo, menús File/View/Render/Help con flyouts, botón de render
- **Toolbar**: Acciones rápidas (Import, New, Reset View, Frame All, Render, Export)
- **Left panel**: Jerarquía de escena con conteo de items
- **Center**: Viewport 3D con botón de importación integrado
- **Right panel**: Propiedades dinámicas (material, cámara, render, iluminación)
- **Bottom bar**: Información de escena y render
- **Decoraciones**: Minimizar, maximizar, cerrar, mover (SystemDecorations=Full)

#### Views/MainWindow.axaml.cs
Code-behind con manejo de atajos de teclado:
- Ctrl+O: Importar modelo
- Ctrl+N: Nueva escena
- Ctrl+S: Exportar render
- F5: Renderizar

#### ViewModels/MainViewModel.cs
ViewModel principal con todos los comandos funcionales:
- `ImportFileCommand` — Diálogo real de archivo para importar OBJ
- `NewSceneCommand` — Crear nueva escena (carga escena demo)
- `ExportRenderCommand` — Diálogo de guardado para exportar render
- `ExitCommand` — Cerrar aplicación
- `ToggleGridCommand` — Mostrar/ocultar grid
- `ResetCameraCommand` — Resetear cámara a posición por defecto
- `FrameAllCommand` — Encuadrar todos los objetos
- `ZoomInCommand` / `ZoomOutCommand` — Control de zoom
- `RenderCommand` — Renderizar con simulación de tiempo según calidad
- `SetQualityCommand` — Cambiar calidad (Draft/Medium/High/Ultra)
- `AboutCommand` / `ShortcutsCommand` — Información de la app
- `OpenGithubCommand` — Abrir repositorio en navegador

---

## Pipeline de Renderizado

### Shader PBR (Simplificado)

```
1. Vertex Shader
   ├── Transforma posición: MVP * position
   ├── Transforma normal: normalMatrix * normal
   └── Pasa: FragPos, Normal, TexCoord al fragment

2. Fragment Shader
   ├── Ambient: albedo × ambientColor × ambientIntensity
   ├── Diffuse: Lambert (N·L) × lightColor × intensity × albedo
   ├── Specular: Blinn-Phong (N·H)^shininess × specColor
   ├── shininess = mix(8, 256, 1-roughness)
   ├── specColor = mix(0.04, albedo, metallic)
   ├── Combine: ambient + diffuse + specular
   ├── Tone map: 1 - exp(-result × exposure)  [Reinhard]
   └── Gamma: result^(1/gamma)
```

### Orden de Renderizado

1. Clear buffers (color + depth)
2. Render grid (con transparencia)
3. Para cada nodo visible:
   - Calcular matriz modelo
   - Calcular matriz normal (inversa transpuesta)
   - Configurar uniforms de material
   - Dibujar mesh indexada

---

## Sistema de Materiales PBR

El sistema usa el workflow **Metallic-Roughness**, estándar en la industria.

### Parámetros

| Parámetro | Rango | Descripción |
|---|---|---|
| Albedo | RGB [0,1] | Color base del material |
| Metallic | [0,1] | 0=dieléctrico, 1=metal |
| Roughness | [0,1] | 0=espejo, 1=difuso |
| AO | [0,1] | Oclusión ambiental |
| Opacity | [0,1] | Transparencia |
| Emissive | RGB [0,∞) | Auto-iluminación |
| NormalStrength | [0,1] | Intensidad del mapa de normales |

### Texturas (Planeadas Fase 3)

- Albedo Map
- Normal Map
- Metallic Map
- Roughness Map
- AO Map

---

## Sistema de Importación

### Interfaz IModelImporter

```csharp
public interface IModelImporter
{
    IReadOnlyList<string> SupportedExtensions { get; }
    string FormatDescription { get; }
    bool CanImport(string filePath);
    Task<ImportResult> ImportAsync(string filePath, ImportOptions? options);
}
```

### Agregar un Nuevo Importador

1. Crear clase que implemente `IModelImporter`
2. Registrarlo en `ImportManager`:
```csharp
var manager = new ImportManager();
manager.RegisterImporter(new FbxImporter());
```

### Opciones de Importación

| Opción | Default | Descripción |
|---|---|---|
| MergeMeshes | false | Combinar meshes con mismo material |
| GenerateNormals | true | Generar normales si no existen |
| FlipUVs | false | Voltear coordenadas UV |
| Scale | 1.0 | Escala uniforme al importar |
| Triangulate | true | Convertir polígonos a triángulos |

---

## Interfaz de Usuario

### Paleta de Colores (Tema Claro)

| Color | Hex | Uso |
|---|---|---|
| Primary BG | `#FFFFFF` | Fondo principal |
| Secondary BG | `#F5F6F8` | Paneles y barras |
| Panel BG | `#EFF0F3` | Secciones de propiedades |
| Surface BG | `#E8E9ED` | Elementos interactivos |
| Border | `#D5D7DC` | Bordes y divisores |
| Accent | `#2563EB` | Color de acento (azul) |
| Text Primary | `#1A1A2E` | Texto principal |
| Text Secondary | `#5A5A6E` | Texto secundario |
| Text Muted | `#8E8E9A` | Texto deshabilitado |
| Success | `#16A34A` | Estado positivo |
| Warning | `#EA580C` | Advertencias |
| Error | `#DC2626` | Errores |

### Layout de la Interfaz

```
┌─ ◆ Open Render — Architectural Renderer ─ ☐ ✕ ┐  ← Window Decorations
├─────────────────────────────────────────────────┤
│ ◆ OPEN RENDER  File▾ View▾ Render▾ Help▾ ⬤Render│  ← Menu Bar
│ 📂Import 🆕New │ 🎯Reset 📐Frame │ 📸Ren 💾Exp│  ← Toolbar
├──────────┬────────────────────────┬─────────────┤
│  SCENE   │                        │ PROPERTIES  │
│ 10 items │     3D Viewport         │             │
│          │                        │ MATERIAL    │
│ ☀️ Sun   │  Open Render — 3D      │  Roughness  │
│ 📷 Camera│                        │  Metallic   │
│ 📦 Ground│  [📂 Import 3D Model]  │  Opacity    │
│ 📦 Wall  │                        │             │
│ 📦 Column│                        │ CAMERA      │
│ 📦 Roof  │                        │  FOV / Dist │
│ 📦 Table │  LMB:Orbit MMB:Pan     │  Yaw / Pitch│
│          │  Scroll:Zoom     XYZ   │             │
├──────────┴────────────────────────┴─────────────┤
│ Objects: 8  Triangles: 72  Materials: 5  │ High │  ← Status Bar
└─────────────────────────────────────────────────┘
```

---

## Guía de Compilación

### Requisitos del Sistema

| Componente | Mínimo | Recomendado |
|---|---|---|
| **OS** | Windows 10 / Ubuntu 20.04 / macOS 12 | Windows 11 / Ubuntu 22.04 |
| **RAM** | 4 GB | 8 GB |
| **GPU** | OpenGL 3.3 compatible | OpenGL 4.5+ |
| **.NET SDK** | 10.0 | 10.0 (latest) |

### Comandos

```bash
# Restaurar paquetes NuGet
dotnet restore OpenRender.sln

# Compilar en modo Debug
dotnet build OpenRender.sln

# Compilar en modo Release
dotnet build OpenRender.sln -c Release

# Ejecutar la aplicación UI
dotnet run --project OpenRender.csproj

# Publicar para distribución
dotnet publish OpenRender.csproj -c Release -o ./publish
```

### Solución de Problemas

**Error: "Unable to find package Silk.NET"**
```bash
dotnet nuget add source https://api.nuget.org/v3/index.json
dotnet restore
```

**Error: "OpenGL 3.3 not supported"**
Actualizar drivers de GPU. La mayoría de GPUs desde 2010+ soportan OpenGL 3.3.

---

## Próximos Pasos

### Para la Fase 2 (Importación Avanzada)

1. **Integrar Assimp** via `Silk.NET.Assimp` para soporte de FBX, glTF, DAE, STL
2. **Importador glTF** nativo (es el formato del futuro)
3. **Importador IFC** vía librería xBIM para modelos BIM

### Para la Fase 3 (Materiales)

1. **Carga de texturas** con ImageSharp a GPU
2. **Editor de materiales** visual en el panel derecho
3. **HDRI** para iluminación basada en imagen

### Para la migración a Vulkan

La arquitectura está preparada. Los pasos serían:
1. Crear `VulkanRenderer` implementando la misma interfaz que `SceneRenderer`
2. Reescribir shaders de GLSL 330 a SPIR-V
3. Implementar el pipeline de Vulkan (device, swapchain, command buffers)
4. Intercambiar el backend en la configuración

---

*Documento generado el 16 de mayo de 2026*
*Versión: 0.1.0 — Fase 1*
