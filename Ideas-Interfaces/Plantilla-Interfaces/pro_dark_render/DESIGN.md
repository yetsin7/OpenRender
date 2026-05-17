---
name: Pro Dark Render
colors:
  surface: '#131313'
  surface-dim: '#131313'
  surface-bright: '#393939'
  surface-container-lowest: '#0e0e0e'
  surface-container-low: '#1c1b1b'
  surface-container: '#201f1f'
  surface-container-high: '#2a2a2a'
  surface-container-highest: '#353534'
  on-surface: '#e5e2e1'
  on-surface-variant: '#bdc8d1'
  inverse-surface: '#e5e2e1'
  inverse-on-surface: '#313030'
  outline: '#87929b'
  outline-variant: '#3e4850'
  surface-tint: '#82cfff'
  primary: '#82cfff'
  on-primary: '#00344b'
  primary-container: '#00aeef'
  on-primary-container: '#003e58'
  inverse-primary: '#00658d'
  secondary: '#c8c6c5'
  on-secondary: '#303030'
  secondary-container: '#474746'
  on-secondary-container: '#b7b5b4'
  tertiary: '#ffb876'
  on-tertiary: '#4b2800'
  tertiary-container: '#ea8c21'
  on-tertiary-container: '#572f00'
  error: '#ffb4ab'
  on-error: '#690005'
  error-container: '#93000a'
  on-error-container: '#ffdad6'
  primary-fixed: '#c6e7ff'
  primary-fixed-dim: '#82cfff'
  on-primary-fixed: '#001e2d'
  on-primary-fixed-variant: '#004c6b'
  secondary-fixed: '#e5e2e1'
  secondary-fixed-dim: '#c8c6c5'
  on-secondary-fixed: '#1b1b1c'
  on-secondary-fixed-variant: '#474746'
  tertiary-fixed: '#ffdcc0'
  tertiary-fixed-dim: '#ffb876'
  on-tertiary-fixed: '#2d1600'
  on-tertiary-fixed-variant: '#6b3b00'
  background: '#131313'
  on-background: '#e5e2e1'
  surface-variant: '#353534'
typography:
  headline-lg:
    fontFamily: Inter
    fontSize: 32px
    fontWeight: '600'
    lineHeight: '1.2'
    letterSpacing: -0.02em
  headline-md:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '600'
    lineHeight: '1.3'
    letterSpacing: -0.01em
  headline-sm:
    fontFamily: Inter
    fontSize: 18px
    fontWeight: '500'
    lineHeight: '1.4'
  body-lg:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: '1.6'
  body-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: '1.5'
  label-md:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '500'
    lineHeight: '1'
    letterSpacing: 0.02em
  label-sm:
    fontFamily: Inter
    fontSize: 11px
    fontWeight: '600'
    lineHeight: '1'
    letterSpacing: 0.05em
  mono-sm:
    fontFamily: JetBrains Mono
    fontSize: 12px
    fontWeight: '400'
    lineHeight: '1.4'
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  base: 4px
  xs: 4px
  sm: 8px
  md: 16px
  lg: 24px
  xl: 48px
  panel-width: 320px
  toolbar-height: 56px
---

## Brand & Style
The design system is engineered for professional 3D visualization workflows, prioritizing deep immersion and visual focus on the viewport. The aesthetic follows a **Pro Dark Mode** philosophy—a synthesis of high-end corporate reliability and architectural minimalism. 

By utilizing a monochromatic foundation with a singular, high-vibrancy accent, the interface recedes into the background, allowing the user's creative content to take center stage. The emotional response is one of precision, power, and technical sophistication. Surfaces are treated with a matte finish, avoiding distracting glares or aggressive gradients, ensuring comfort during long-duration rendering sessions.

## Colors
The palette is dominated by "True Dark" and "Slate" tones to maintain a low-light environment conducive to color-accurate 3D work. 

- **Primary (Lumion Blue):** Reserved exclusively for active states, primary actions, and selection highlights. 
- **Neutral Core:** The base uses `#121212` for the main workspace background. `#1E1E1E` is utilized for floating panels and sidebar containers to provide subtle separation.
- **Support Tones:** Success, Warning, and Error states should use desaturated versions of green, amber, and red to prevent them from "popping" more than the primary brand accent.
- **Borders:** Instead of high-contrast lines, use `#2A2A2A` for hair-line borders to define UI boundaries without creating visual noise.

## Typography
The design system utilizes **Inter** for its neutral, highly legible character at small scale—critical for complex toolbars and property inspectors. 

- **Hierarchy:** Use weight (Medium to Semi-Bold) rather than size to establish hierarchy in dense panels.
- **Labels:** Small, uppercase labels with slightly increased letter spacing are used for property headers to differentiate them from user input data.
- **Monospacing:** For coordinate inputs (X, Y, Z) and hex codes, use a monospaced font to ensure numerical alignment and readability.
- **Readability:** Maintain a high contrast ratio for text (Off-white `#E0E0E0` on dark backgrounds) while avoiding pure `#FFFFFF` to reduce eye strain.

## Layout & Spacing
The layout follows a **structured fixed-panel model** combined with a fluid central viewport. 

- **Grid:** A strict 4px/8px baseline grid governs all component alignments.
- **Panels:** Sidebars (Properties/Assets) are fixed at 320px to ensure predictability in complex workflows. 
- **Project Previews:** Use a fluid grid for project galleries with a minimum card width of 280px.
- **Padding:** Maintain a consistent 16px (md) internal padding for all cards and modals. Use 8px (sm) for dense tool settings to maximize vertical space.

## Elevation & Depth
Depth is communicated through **Tonal Layering** rather than heavy shadows. 

1. **Level 0 (Canvas):** `#0A0A0A` - The base layer for the software frame.
2. **Level 1 (Panels/Bars):** `#121212` - Used for the main sidebar and bottom shelf.
3. **Level 2 (Cards/Containers):** `#1E1E1E` - Floating elements or inset property groups.
4. **Level 3 (Pop-overs/Modals):** `#252525` - These should feature a subtle 1px border (`#333333`) and a soft, large-radius shadow (32px blur, 0.4 opacity) to lift them off the UI.

Glassmorphism is applied sparingly: only on viewport-overlay HUDs to maintain a sense of space while viewing the 3D scene.

## Shapes
The shape language is **Soft (Radius: 4px)**. This provides a modern, approachable feel while maintaining the structural rigidity expected of professional CAD and rendering software.

- **Buttons & Inputs:** 4px radius.
- **Cards:** 8px (rounded-lg) for larger project containers.
- **Selection Brackets:** Sharp corners should be used for viewport selection indicators to signify technical precision.

## Components
- **Buttons:** 
  - *Primary:* Solid `#00AEEF` with white text. 
  - *Secondary:* Ghost style with `#2A2A2A` borders, turning primary on hover.
- **Input Fields:** Darker than the container background (`#0F0F0F`) with a 1px border. Focus state is a 1px `#00AEEF` outline.
- **Sliders:** Minimalist lines with a circular handle. The track should fill with the primary color as it progresses.
- **Cards:** Project cards feature a large 16:9 thumbnail, a subtle 1px border, and a bottom section for metadata. Use a "Dim" state for inactive projects.
- **Toolbars:** Use icon-only buttons with a 32x32px hit area. Active tools are highlighted with a vertical primary-colored bar on the left edge of the button.
- **Tabs:** Underline style using the primary color for the active state; no background fills for tabs to keep the interface clean.