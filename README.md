# ◆ Open Render

**Software de renderizado arquitectónico ligero, moderno y de alto rendimiento basado en Vulkan.**

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)
![Graphics](https://img.shields.io/badge/graphics-Vulkan-red.svg)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-green.svg)

---

## 🏗️ ¿Qué es Open Render?

Open Render es una solución de **renderizado arquitectónico** diseñada para maximizar la eficiencia y la calidad visual. A diferencia de las herramientas tradicionales pesadas, Open Render utiliza una arquitectura moderna basada en **Vulkan** para ofrecer un rendimiento excepcional incluso en hardware moderado.

**No es un programa de modelado 3D.** Su propósito es transformar modelos arquitectónicos existentes en imágenes realistas mediante un flujo de trabajo optimizado y una interfaz minimalista inspirada en los estándares de la industria (Lumion/Twinmotion).

### Diferenciadores clave

| Característica | Open Render | Otros (Lumion/Twinmotion) |
|---|---|---|
| **Motor Gráfico** | **Vulkan (Deferred)** | DX11/DX12/Unreal |
| **Consumo de VRAM** | Muy Bajo | Alto |
| **Arquitectura** | Modular .NET 10 | Monolítica / Heavy Engines |
| **Interfaz** | Pro Dark (Minimalista) | Compleja |
| **Licencia** | Código Abierto (MIT) | Comercial ($$$) |

---

## ⚡ Características Actuales

### 🎨 Motor de Renderizado (Vulkan Core)
- ✅ **Deferred Rendering Pipeline**: Manejo eficiente de múltiples luces y materiales complejos.
- ✅ **PBR (Physically Based Rendering)**: Soporte completo para Albedo, Roughness, Metalness y Normal Maps.
- ✅ **Efectos de Post-procesado**:
    - **SSAO**: Screen Space Ambient Occlusion para sombras de contacto realistas.
    - **Bloom**: Resplandor dinámico en áreas de alta intensidad lumínica.
    - **Tone Mapping**: Algoritmos de Reinhard y corrección Gamma.
- ✅ **G-Buffer**: Pipeline avanzado con buffers de Posición, Normales y Albedo.

### 🖥️ Interfaz de Usuario (Pro Dark)
- ✅ **Avalonia UI 11**: Interfaz multiplataforma, fluida y profesional.
- ✅ **Workspace Estilo Lumion**: Navegación intuitiva con paneles laterales de biblioteca, materiales y ajustes.
- ✅ **Sistema de Temas**: Interfaz oscura de alta gama (Pro Dark) diseñada para reducir la fatiga visual.
- ✅ **Localización**: Soporte nativo para Inglés y Español.

### 📦 Gestión de Escena y Activos
- ✅ **Importación OBJ**: Carga de mallas con generación automática de normales y optimización de vértices.
- ✅ **Editor de Materiales**: Control en tiempo real sobre las propiedades PBR de cada objeto.
- ✅ **Cámara Pro**: Sistema de navegación orbital y tipo "fly-through" con controles de exposición fotográfica.
- ✅ **Jerarquía de Escena**: Organización clara de nodos y objetos en el proyecto.

---

## 🚀 Inicio Rápido

### Prerrequisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- GPU compatible con **Vulkan 1.2+**

### Compilar y Ejecutar

```bash
# Clonar el repositorio
git clone https://github.com/yetsin7/OpenRender.git
cd OpenRender

# Ejecutar el script de inicio (Windows)
./run.cmd

# O manualmente vía dotnet
dotnet run --project OpenRender.UI/OpenRender.UI.csproj
```

---

## 📁 Estructura del Proyecto

OpenRender está diseñado de forma modular para facilitar la escalabilidad:

- **OpenRender.UI**: Aplicación principal y vistas (Avalonia UI, MVVM).
- **OpenRender.Rendering**: Motor central de renderizado basado en Vulkan.
- **OpenRender.Assets**: Gestión e importación de modelos y texturas.
- **OpenRender.Materials**: Definiciones y catálogos de materiales PBR.
- **OpenRender.Scene**: Grafo de escena, cámaras y gestión de nodos.
- **OpenRender.Engine**: Lógica de integración y ciclo de vida del motor.
- **OpenRender.Tools**: Utilidades matemáticas y espaciales compartidas.
- **OpenRender.Vegetation**: (En desarrollo) Sistema de instanciación de vegetación.

---

## 🗺️ Roadmap de Evolución

### ✅ Fase 1 — Motor Base (Completado)
- [x] Migración integral de OpenGL a **Vulkan**.
- [x] Pipeline diferido funcional.
- [x] Implementación de SSAO y Bloom.
- [x] Interfaz Pro Dark con Avalonia.

### 🔜 Fase 2 — Expansión de Formatos (En curso)
- [ ] Integración de **Assimp** para soporte FBX, glTF e IFC.
- [ ] Sistema de caché de assets para carga ultrarrápida.
- [ ] Soporte para múltiples LODs (Level of Detail).

### 📋 Fase 3 — Entorno y Vegetación
- [ ] Sistema de Cielo Dinámico (Physical Sky).
- [ ] Soporte para entornos HDRI.
- [ ] Sistema de "Paint" de vegetación con instanciación GPU masiva.

### 📋 Fase 4 — Render Final y Exportación
- [ ] Exportador de alta resolución (4K/8K) con Tiled Rendering.
- [ ] Denoiser inteligente para resultados limpios.
- [ ] Cola de renderizado para múltiples vistas.

---

## 🤝 Contribuir

Si eres desarrollador C#, experto en gráficos (Vulkan/HLSL) o entusiasta de la arquitectura, ¡tu ayuda es bienvenida!

1. Haz un **Fork** del proyecto.
2. Crea una rama para tu feature: `git checkout -b feature/amazing-feature`
3. Realiza tus cambios y haz un commit: `git commit -m 'Add amazing feature'`
4. Sube los cambios: `git push origin feature/amazing-feature`
5. Abre un **Pull Request**.

---

## 📄 Licencia

Distribuido bajo la Licencia MIT. Consulta `LICENSE` para más información.

---

*Desarrollado con ❤️ para democratizar el renderizado arquitectónico de alta calidad.*
