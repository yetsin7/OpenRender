# ◆ Open Render

**Software de renderizado arquitectónico ligero y moderno**

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-green.svg)

---

## 🏗️ ¿Qué es Open Render?

Open Render es un software especializado en **renderizado arquitectónico** diseñado para ser más ligero y eficiente que alternativas como Lumion, Twinmotion o Enscape.

**No es un programa de modelado 3D.** Su enfoque está en importar modelos existentes y generar renders fotográficos de alta calidad.

### Diferenciadores clave

| Característica | Open Render | Competencia |
|---|---|---|
| **Consumo de RAM** | Bajo | Alto |
| **Consumo de GPU** | Optimizado | Intensivo |
| **Velocidad** | Rápido | Variable |
| **Curva de aprendizaje** | Mínima | Moderada-Alta |
| **Precio** | Gratuito (Open Source) | $500-$2000+/año |

---

## ⚡ Características Actuales (Fase 1)

### Motor de Renderizado
- ✅ Renderizado OpenGL 3.3+ vía **Silk.NET**
- ✅ Pipeline de materiales **PBR** (Metallic-Roughness)
- ✅ Iluminación tipo sol (directional) con Blinn-Phong
- ✅ Tone mapping (Reinhard) y corrección gamma
- ✅ Grid de referencia en viewport
- ✅ Anti-aliasing

### Escena 3D
- ✅ Grafo de escena jerárquico con transformaciones
- ✅ Cámara orbital (orbit, pan, zoom)
- ✅ Sistema de materiales PBR con presets arquitectónicos
- ✅ Sistema de iluminación (direccional, puntual, spot)
- ✅ Escena demo con estructura arquitectónica

### Importación de Modelos
- ✅ Formato **OBJ** (Wavefront)
- ✅ Generación automática de normales
- ✅ Triangulación de polígonos
- ✅ Auto-encuadre de cámara

### Interfaz de Usuario
- ✅ **Avalonia UI** multiplataforma
- ✅ Tema claro profesional (blanco)
- ✅ Ventana con minimizar, maximizar, cerrar y mover
- ✅ Panel de jerarquía de escena
- ✅ Panel de propiedades (materiales, cámara, render, iluminación)
- ✅ Barra de herramientas con acciones rápidas
- ✅ Menús funcionales: File, View, Render, Help
- ✅ Diálogo real de importación/exportación de archivos
- ✅ Atajos de teclado (Ctrl+O, Ctrl+N, Ctrl+S, F5)
- ✅ Barra de estado con información de escena
- ✅ Patrón **MVVM** con CommunityToolkit.Mvvm

---

## 🚀 Inicio Rápido

### Prerrequisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download) o superior
- GPU compatible con OpenGL 3.3+

### Compilar y Ejecutar

```bash
# Clonar el repositorio
git clone https://github.com/yetsin7/OpenRender.git
cd OpenRender

# Restaurar dependencias
dotnet restore

# Compilar
dotnet build

# Ejecutar
dotnet run --project OpenRender.csproj
```

### Controles del Viewport

| Acción | Control |
|---|---|
| **Orbitar** | Click izquierdo + arrastrar |
| **Pan** | Click medio + arrastrar |
| **Zoom** | Rueda del ratón |

### Atajos de Teclado

| Atajo | Acción |
|---|---|
| **Ctrl+O** | Importar modelo 3D |
| **Ctrl+N** | Nueva escena |
| **Ctrl+S** | Exportar render |
| **F5** | Renderizar |

---

## 📁 Estructura del Proyecto

```
OpenRender/
├── OpenRender.sln                    # Solución .NET
├── Directory.Build.props             # Propiedades compartidas
├── README.md                         # Este archivo
├── Instrucciones.md                  # Documento de diseño original
│
└── src/
    ├── OpenRender/                   # 🖥️ Aplicación UI (Avalonia)
    │   ├── Program.cs                # Punto de entrada
    │   ├── App.axaml(.cs)            # Configuración de la app
    │   ├── Views/
    │   │   └── MainWindow.axaml(.cs) # Ventana principal
    │   └── ViewModels/
    │       └── MainViewModel.cs      # ViewModel principal (MVVM)
    │
    ├── OpenRender.Core/              # 📐 Modelos de dominio
    │   ├── Scene/
    │   │   ├── Scene3D.cs            # Contenedor raíz de escena
    │   │   ├── SceneNode.cs          # Nodo del grafo de escena
    │   │   ├── MeshData.cs           # Geometría de malla
    │   │   ├── Camera.cs             # Cámara orbital
    │   │   ├── PbrMaterial.cs        # Material PBR
    │   │   └── LightSource.cs        # Fuentes de luz
    │   ├── Import/
    │   │   └── IModelImporter.cs     # Interfaz de importación
    │   └── Rendering/
    │       └── RenderSettings.cs     # Configuración de render
    │
    └── OpenRender.Rendering/         # 🎨 Motor de renderizado
        ├── SceneRenderer.cs          # Renderer principal (OpenGL)
        ├── GpuMesh.cs                # Buffers GPU (VAO/VBO/EBO)
        ├── ViewportWindow.cs         # Ventana de viewport standalone
        ├── DemoScene.cs              # Escena demo arquitectónica
        ├── Shaders/
        │   ├── ShaderSources.cs      # Código GLSL de shaders
        │   └── ShaderProgram.cs      # Manejo de programa shader
        ├── Primitives/
        │   └── PrimitiveGenerator.cs # Generador de primitivas 3D
        └── Import/
            ├── ObjImporter.cs        # Importador OBJ
            └── ImportManager.cs      # Registro de importadores
```

---

## 🏛️ Arquitectura Moderna (Stride Engine)

### Módulos Principales

```
┌─────────────────────────────────────────┐
│           OpenRender (UI)               │
│   Avalonia UI · MVVM · Stride Viewport  │
├─────────────────┬───────────────────────┤
│  OpenRender     │  OpenRender           │
│  .Scene         │  .Engine              │
│                 │                       │
│  Scene Bridge   │  Stride Engine Core   │
│  Entity Sync    │  AAA Navigation       │
│  PBR Materials  │  PBR & HDR Render     │
│  Model Mapping  │  Post-Processing      │
└─────────────────┴───────────────────────┘
```

### Tecnologías

| Componente | Tecnología | Propósito |
|---|---|---|
| **Lenguaje** | C# | Lenguaje principal |
| **Framework** | .NET 8-windows | Runtime de alto rendimiento |
| **Motor 3D** | Stride Engine 4.2 | Core de renderizado y física |
| **UI** | Avalonia UI 11.2 | Interfaz gráfica profesional |
| **Navegación** | Lumion-style Script | Experiencia cinematográfica |


---

## 🗺️ Roadmap

### ✅ Fase 1 — Base del Programa (Actual)
- [x] Ventana principal con Avalonia UI
- [x] Motor de renderizado OpenGL
- [x] Cámara orbital básica
- [x] Escena demo arquitectónica
- [x] Importador OBJ
- [x] Sistema de materiales PBR
- [x] Sistema de iluminación

### 🔜 Fase 2 — Importación Avanzada
- [ ] Importador FBX (vía Assimp)
- [ ] Importador glTF/GLB
- [ ] Importador IFC (arquitectura BIM)
- [ ] Importador múltiples mallas/materiales

### 📋 Fase 3 — Materiales Avanzados
- [ ] Texturas de albedo, normales, metallic, roughness
- [ ] HDRI para iluminación ambiental
- [ ] Materiales predefinidos (madera, concreto, vidrio, metal)
- [ ] Editor visual de materiales

### 📋 Fase 4 — Render Fotográfico
- [ ] Sombras suaves (shadow mapping)
- [ ] Reflejos (screen-space reflections)
- [ ] Ambient Occlusion (SSAO)
- [ ] Anti-aliasing MSAA
- [ ] Exportación PNG/JPG de alta resolución

### 📋 Fase 5 — Optimización
- [ ] Render progresivo por etapas
- [ ] Instancing para geometrías repetidas
- [ ] Compresión de texturas
- [ ] Carga diferida de modelos
- [ ] Culling de objetos fuera de vista

### 📋 Fase 6 — Compatibilidad Avanzada
- [ ] Soporte Revit (.rvt)
- [ ] Soporte AutoCAD (.dwg/.dxf)
- [ ] Soporte SketchUp (.skp)

---

## 📦 Formatos Soportados

### Actualmente Soportados
| Formato | Estado | Descripción |
|---|---|---|
| `.obj` | ✅ Implementado | Wavefront OBJ |

### Planeados (Prioritarios)
| Formato | Estado | Descripción |
|---|---|---|
| `.fbx` | 🔜 Fase 2 | Modelos 3D generales |
| `.gltf` / `.glb` | 🔜 Fase 2 | Modelos modernos optimizados |
| `.ifc` | 🔜 Fase 2 | Arquitectura BIM |

### Planeados (Secundarios)
| Formato | Estado | Descripción |
|---|---|---|
| `.rvt` | 📋 Fase 6 | Revit |
| `.dwg` | 📋 Fase 6 | AutoCAD |
| `.dxf` | 📋 Fase 6 | AutoCAD |
| `.skp` | 📋 Fase 6 | SketchUp |
| `.stl` | 📋 Fase 2 | Modelos 3D |
| `.dae` | 📋 Fase 2 | Collada |
| `.3ds` | 📋 Fase 2 | 3D Studio |

---

## 🎨 Materiales PBR Presets

Open Render incluye materiales predefinidos para uso arquitectónico:

| Material | Albedo | Metallic | Roughness |
|---|---|---|---|
| **Default** | Gris claro | 0.0 | 0.5 |
| **Concrete** | Gris cálido | 0.0 | 0.85 |
| **Glass** | Blanco azulado | 0.0 | 0.05 |
| **Brushed Metal** | Plata | 1.0 | 0.3 |
| **Wood** | Marrón | 0.0 | 0.7 |

---

## 💡 Sistema de Iluminación

| Tipo | Implementado | Descripción |
|---|---|---|
| **Directional (Sol)** | ✅ | Luz paralela tipo sol |
| **Point** | ✅ (modelo) | Luz puntual omnidireccional |
| **Spot** | ✅ (modelo) | Luz tipo foco con cono |
| **HDRI** | 🔜 | Iluminación basada en imagen |

---

## 🤝 Contribuir

Las contribuciones son bienvenidas. Por favor:

1. Fork el repositorio
2. Crea una rama (`git checkout -b feature/nueva-funcionalidad`)
3. Commit tus cambios (`git commit -m 'Agrega nueva funcionalidad'`)
4. Push a la rama (`git push origin feature/nueva-funcionalidad`)
5. Abre un Pull Request

---

## 📄 Licencia

Este proyecto es de código abierto. Consulta el archivo [LICENSE](LICENSE) para más detalles.

---

## 🔗 Links

- **Repositorio**: https://github.com/yetsin7/OpenRender
- **Issues**: https://github.com/yetsin7/OpenRender/issues

---

*Hecho con ❤️ para arquitectos y diseñadores que merecen herramientas mejores.*
