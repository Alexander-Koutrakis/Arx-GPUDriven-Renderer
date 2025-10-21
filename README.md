# Arx GPUDriven Renderer

GPU-Driven Rendering of Arx Fatalis Level Geometry (Unity URP)

## Overview

This repository contains a Unity URP prototype that reconstructs and renders static level geometry from Arx Fatalis (2001) using a GPU-driven pipeline. Geometry is organized into spatial cells and rendered with DrawProceduralIndirect; culling is performed entirely on the GPU using frustum tests and a hierarchical Z-buffer (Hi-Z) built from the previous frame’s depth.

Player View

![arx-player2](https://github.com/user-attachments/assets/28d7475e-255b-4f5a-a03b-c0d90af7cf9c)

Editor

![arx-editor2](https://github.com/user-attachments/assets/001cd74d-5989-44c0-98a8-6fc639d19b2d)

## Key points

### Data sourcing and reconstruction:
  - Static geometry in Arx appears to be stored as spatial cells referencing vertex indices and textures. This project reconstructs that data into a `ModelData` ScriptableObject.
  - Source conversion was done from FastSceneLoad (.fts) to JSON via the arx-convert tool by Lajos Mészáros (see “Credits”). Geometry/materials were then rebuilt in Unity meshes/materials and exported into `ModelData`.
  - Textures were upscaled (Midjourney) and normal maps generated in GIMP. Textures are grouped into Texture2DArrays to reduce bindings; arrays require uniform width/height/format.

### GPU-driven rendering:
  - A custom `ScriptableRendererFeature` (`GPURendererFeature`) injects three passes:
    1) `CullingPass`: Frustum + Hi-Z occlusion culling at the cell (AABB) level; visible triangle indices are written into append buffers for opaque and transparent.
    2) `RenderingPass` (opaque): Draws visible opaque triangles via `DrawProceduralIndirect`.
    3) `RenderingPass` (transparent): Draws visible transparent triangles via `DrawProceduralIndirect` with alpha blending.
    4) `CopyDepthPass`: Copies this frame’s depth and generates mipmaps to build a Hi-Z pyramid for the next frame’s occlusion.
  - Rationale: Culling with the previous frame’s depth avoids CPU-GPU sync and heavy CPU-side visibility work.

### Shaders and compute:
  - Compute: `TriangleCulling.compute` (cell culling + append visible triangle lists), `DepthCopy.compute` (copy + hole fill for zero depth), `DepthMipGen.compute` (min-reduction mip chain for Hi-Z).
  - Forward shaders: `TriangleDrawerForwardOpaque.shader`, `TriangleDrawerForwardTransparent.shader` consume structured buffers (`_VertexBuffer`, `_TriangleBuffer`, `_MaterialBuffer`, `_VisibleTrianglesBuffer`) and sample from up to six 2D texture arrays (`_TexArray0`…`_TexArray5`).

### Occlusion Culling (Hi‑Z)

- Uses a previous‑frame depth pyramid (min‑reduction mip chain) to occlude whole cells (AABBs).
- For each cell, estimates screen‑space footprint to choose a mip; compares AABB depth vs Hi‑Z to reject occluded cells.
- Entirely GPU‑driven: culling in `Resources/Shaders/TriangleCulling.compute`; depth copy + mip chain in `Resources/Shaders/DepthCopy.compute` and `Resources/Shaders/DepthMipGen.compute`; scheduled by `Scripts/Rendering/CullingPass.cs` and `GPURendererFeature.cs`.
- Result: reduced work from <X k> → <Y k> triangles, ~<Z ms> → ~<W ms> on <GPU>.

Details:
- Mip selection: use the cell’s screen‑space AABB size; larger on‑screen → coarser mip.
- Hi‑Z sampling: sample a 4×4 block at that mip and take the min to stay conservative against cracks and temporal changes.
- Test: compare the cell’s near depth vs the sampled Hi‑Z with a small positive bias to avoid false occlusion.

![depth_mip3-AABB-Test](https://github.com/user-attachments/assets/75833508-8f7f-4e10-a949-eec929d90981)
<sub>Previous‑frame Hi‑Z depth pyramid, mip 3. Green box = 4×4 conservative sample for the cell’s occlusion test; mip chosen from screen‑space footprint.</sub>

## How it works

### Data layout (`ModelData`):
   - Positions, normals, tangents, UVs in `Vertices`.
   - `Triangles` reference vertex indices and a material index.
   - `Materials` reference into grouped Texture2DArrays (albedo and normal), plus PBR parameters (metallic, smoothness, color) and alpha clip controls.
   - A 3D grid (`Grid`) partitions the world AABB into cells; each populated cell stores a range into a flat triangle-index list.

### Runtime pipeline:
   - On `GPURendererFeature.Create()`, compute buffers and texture arrays are created from `ModelData`. A depth RTHandle is set up for `_PreviousFrameDepthTexture` with mipmaps.
   - Per-frame:
     - Culling pass (skip first frame):
       - Frustum test on cell AABBs.
       - Hi-Z occlusion test by sampling `_PreviousFrameDepthTexture` mip level approximated from the cell’s screen-space footprint.
       - Append visible triangle indices to opaque/transparent lists; write instance counts to indirect draw args.
     - Opaque render pass: `DrawProceduralIndirect` of visible triangles; URP forward lighting is evaluated in the shader using material data and texture arrays.
     - Transparent render pass: same draw path with alpha blending.
     - Depth copy pass: Copy current active depth to `_PreviousFrameDepthTexture` (hole-fill), then generate mips (min reduction) for next frame.

## Performance snapshot (RenderDoc)

Recorded at 1080p on an NVIDIA GeForce RTX 3070 Laptop GPU.

| Metric | Value |
|:--|:--|
| **File size** | 609.46 MB (822.95 MB uncompressed, compression ratio ≈ 1.35 : 1) |
| **Persistent Data** | ~20.87 MB |
| **Frame-initial Data** | ~801.86 MB |
| **Draw calls** | 35 |
| **Dispatch calls** | 12 |
| **Total API calls** | 560 |
| **Draw / Dispatch ratio** | 11.91 : 1 |
| **Textures** | 31 (391.75 MB total, avg. 318×318 px / 541×634 over 32×32) |
| **Render Targets (RTs)** | 9 – 73.95 MB |
| **Buffers** | 605 – 390.65 MB total (0.13 MB IBs, 0.13 MB VBs) |
| **Total GPU buffer + texture load** | **≈ 856.35 MB** |

<sub>Captured using RenderDoc. Represents typical per-frame resource usage under the GPU-driven rendering pipeline.</sub>

## Limitations/Obstacles:
- No real-time light shadows: procedurally drawn triangles don’t run a ShadowCaster pass, so lights don’t cast shadows on them.
- No emissive materials: emission textures/HDR color are not yet supported.
- Previous-frame occlusion: relies on last frame’s Hi-Z; rapid camera motion or large scene changes can cause conservative errors. Mitigate with camera-velocity gating, temporal dilation/blur of depth, and conservative depth bias.
- Triangle gaps in depth: tiny cracks between triangles can bleed during Hi-Z generation. A simple hole-fill is applied during depth copy; a more robust morphological dilation is recommended.
- Dataset quirks: some vertices are erroneous per arx-convert; several were fixed in Blender, others may remain.
- Transparency ordering: transparent geometry isn’t depth-sorted; order-dependent artifacts may appear.
- Texture array budget: up to six Texture2DArrays are bound; adding more requires extending shader and feature bindings.

## Comparison and evaluation

A direct performance comparison against Unity’s built-in (CPU-driven) rendering pipeline is not included.  
This is primarily because the source geometry is represented as a **single unified scene dataset**, not as individual `GameObject` hierarchies with `MeshRenderer` components.  

In traditional Unity rendering:
- Each object (mesh renderer) incurs **CPU-side culling, batching, and draw call management**.
- Visibility and material binding are resolved per object, which introduces CPU overhead but allows per-object culling.

In this GPU-driven prototype:
- The **entire level’s static geometry** is stored as one large data structure and rendered procedurally via `DrawProceduralIndirect`.
- Geometry is subdivided only into **spatial cells** for GPU culling, with no per-object representation on the CPU.
- This makes a one-to-one comparison with standard GameObject/MeshRenderer rendering impractical.

For testing purposes, an alternative setup was tried:
- Triangles were grouped by material, and one mesh was built per material to simulate a traditional approach.
- However, this led to **very large per-material meshes** (e.g., ~12,000 triangles for a single cave section), where only a small subset of the geometry was visible per frame.
- As a result, the GPU still processed most triangles unnecessarily, and the approach did **not reflect a realistic performance baseline**.

In summary:
- The current pipeline focuses on **fully GPU-driven visibility and draw submission**.
- A meaningful comparison would require reconstructing the level as **many smaller meshes** (per cell or per object) and profiling Unity’s CPU-driven culling and batching against the GPU approach—an effort outside the scope of this prototype.


## Setup and usage

### Prerequisites

- Unity with URP and RenderGraph (Unity 2023.3+ / Unity 6 recommended).
- A GPU with compute shader support (Shader Model 5.0+).

### Steps

1) Open the project in Unity.
2) Ensure your URP Renderer has `GPURendererFeature` added. Asset path example: `Settings/New Universal Render Pipeline Asset_Renderer.asset`.
3) In the `GPURendererFeature` component, assign:
   - `modelData`: one of the prebuilt assets in `Assets/Resources/ModelData/`.
   - `cullingSettings.cullingComputeShader`: `Resources/Shaders/TriangleCulling`.
   - `opaqueDrawingSettings.renderMaterial`: `Resources/Shaders/TriangleDrawerForwardOpaque`.
   - `trasnparentDrawingSettings.renderMaterial`: `Resources/Shaders/TriangleDrawerForwardTransparent`.
4) Press Play. Use WASD to move and Arrow Keys to look (`Scripts/CameraController.cs`).

### Folder highlights

- `Assets/Scripts/Rendering/`
  - `GPURendererFeature.cs`: owns buffers/texture arrays and enqueues passes.
  - `CullingPass.cs`: GPU frustum + occlusion culling.
  - `RenderingPass.cs`: indirect drawing for opaque and transparent.
  - `CopyDepthPass.cs`: builds `_PreviousFrameDepthTexture` + mip chain.
- `Assets/Resources/Shaders/`
  - `TriangleCulling.compute`, `DepthCopy.compute`, `DepthMipGen.compute`
  - `TriangleDrawerForwardOpaque.shader`, `TriangleDrawerForwardTransparent.shader`
- `Assets/Scripts/Data/`
  - `ModelData.cs`, `RenderingData.cs` (vertex/triangle/material/cell layout)
  - `Grid.cs` (builds cell structure and triangle index list)

### Future work
- Emission: support emission per material (texture + HDR color multiplier).
- Shadowing: add a procedural shadow caster path or screen-space shadowing (SSS/SSCS).


### Credits and legal

- Data conversion: arx-convert by Lajos Mészáros (`https://github.com/arx-tools/arx-convert`).
- Arx Fatalis is a game by Arkane Studios. Any extracted assets remain the property of their respective owners. If you intend to publish this repository publicly, ensure you have the right to distribute included assets. Consider providing an extraction script and instructions instead of bundling game data.

