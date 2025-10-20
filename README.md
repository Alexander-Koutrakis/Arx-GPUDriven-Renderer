# GPU-Based Rendering

GPU-Driven Rendering of Arx Fatalis Level Geometry (Unity URP)

Overview

This repository contains a Unity URP prototype that reconstructs and renders static level geometry from Arx Fatalis (2001) using a GPU-driven pipeline. Geometry is organized into spatial cells and rendered with DrawProceduralIndirect; culling is performed entirely on the GPU using frustum tests and a hierarchical Z-buffer (Hi-Z) built from the previous frame’s depth.

Key points

- Data sourcing and reconstruction:
  - Static geometry in Arx appears to be stored as spatial cells referencing vertex indices and textures. This project reconstructs that data into a `ModelData` ScriptableObject.
  - Source conversion was done from FastSceneLoad (.fts) to JSON via the arx-convert tool by Lajos Mészáros (see “Credits”). Geometry/materials were then rebuilt in Unity meshes/materials and exported into `ModelData`.
  - Textures were upscaled (Midjourney) and normal maps generated in GIMP. Textures are grouped into Texture2DArrays to reduce bindings; arrays require uniform width/height/format.

- GPU-driven rendering:
  - A custom `ScriptableRendererFeature` (`GPURendererFeature`) injects three passes:
    1) `CullingPass`: Frustum + Hi-Z occlusion culling at the cell (AABB) level; visible triangle indices are written into append buffers for opaque and transparent.
    2) `RenderingPass` (opaque): Draws visible opaque triangles via `DrawProceduralIndirect`.
    3) `RenderingPass` (transparent): Draws visible transparent triangles via `DrawProceduralIndirect` with alpha blending.
    4) `CopyDepthPass`: Copies this frame’s depth and generates mipmaps to build a Hi-Z pyramid for the next frame’s occlusion.
  - Rationale: Culling with the previous frame’s depth avoids CPU-GPU sync and heavy CPU-side visibility work.

- Shaders and compute
  - Compute: `TriangleCulling.compute` (cell culling + append visible triangle lists), `DepthCopy.compute` (copy + hole fill for zero depth), `DepthMipGen.compute` (min-reduction mip chain for Hi-Z).
  - Forward shaders: `TriangleDrawerForwardOpaque.shader`, `TriangleDrawerForwardTransparent.shader` consume structured buffers (`_VertexBuffer`, `_TriangleBuffer`, `_MaterialBuffer`, `_VisibleTrianglesBuffer`) and sample from up to six 2D texture arrays (`_TexArray0`…`_TexArray5`).


How it works

1) Data layout (`ModelData`):
   - Positions, normals, tangents, UVs in `Vertices`.
   - `Triangles` reference vertex indices and a material index.
   - `Materials` reference into grouped Texture2DArrays (albedo and normal), plus PBR parameters (metallic, smoothness, color) and alpha clip controls.
   - A 3D grid (`Grid`) partitions the world AABB into cells; each populated cell stores a range into a flat triangle-index list.

2) Runtime pipeline:
   - On `GPURendererFeature.Create()`, compute buffers and texture arrays are created from `ModelData`. A depth RTHandle is set up for `_PreviousFrameDepthTexture` with mipmaps.
   - Per-frame:
     - Culling pass (skip first frame):
       - Frustum test on cell AABBs.
       - Hi-Z occlusion test by sampling `_PreviousFrameDepthTexture` mip level approximated from the cell’s screen-space footprint.
       - Append visible triangle indices to opaque/transparent lists; write instance counts to indirect draw args.
     - Opaque render pass: `DrawProceduralIndirect` of visible triangles; URP forward lighting is evaluated in the shader using material data and texture arrays.
     - Transparent render pass: same draw path with alpha blending.
     - Depth copy pass: Copy current active depth to `_PreviousFrameDepthTexture` (hole-fill), then generate mips (min reduction) for next frame.

Limitations/Obstacles:
- No real-time light shadows: procedurally drawn triangles don’t run a ShadowCaster pass, so lights don’t cast shadows on them.
- No emissive materials: emission textures/HDR color are not yet supported.
- Previous-frame occlusion: relies on last frame’s Hi-Z; rapid camera motion or large scene changes can cause conservative errors. Mitigate with camera-velocity gating, temporal dilation/blur of depth, and conservative depth bias.
- Triangle gaps in depth: tiny cracks between triangles can bleed during Hi-Z generation. A simple hole-fill is applied during depth copy; a more robust morphological dilation is recommended.
- Dataset quirks: some vertices are erroneous per arx-convert; several were fixed in Blender, others may remain.
- Transparency ordering: transparent geometry isn’t depth-sorted; order-dependent artifacts may appear.
- Texture array budget: up to six Texture2DArrays are bound; adding more requires extending shader and feature bindings.

Setup and usage

Prerequisites

- Unity with URP and RenderGraph (Unity 2023.3+ / Unity 6 recommended).
- A GPU with compute shader support (Shader Model 5.0+).

Steps

1) Open the project in Unity.
2) Ensure your URP Renderer has `GPURendererFeature` added. Asset path example: `Settings/New Universal Render Pipeline Asset_Renderer.asset`.
3) In the `GPURendererFeature` component, assign:
   - `modelData`: one of the prebuilt assets in `Assets/Resources/ModelData/`.
   - `cullingSettings.cullingComputeShader`: `Resources/Shaders/TriangleCulling`.
   - `opaqueDrawingSettings.renderMaterial`: `Resources/Shaders/TriangleDrawerForwardOpaque`.
   - `trasnparentDrawingSettings.renderMaterial`: `Resources/Shaders/TriangleDrawerForwardTransparent`.
4) Press Play. Use WASD to move and Arrow Keys to look (`Scripts/CameraController.cs`).

Folder highlights

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

Future work
- Emission: support emission per material (texture + HDR color multiplier).
- Shadowing: add a procedural shadow caster path or screen-space shadowing (SSS/SSCS).


Credits and legal

- Data conversion: arx-convert by Lajos Mészáros (`https://github.com/arx-tools/arx-convert`).
- Arx Fatalis is a game by Arkane Studios. Any extracted assets remain the property of their respective owners. If you intend to publish this repository publicly, ensure you have the right to distribute included assets. Consider providing an extraction script and instructions instead of bundling game data.

License

Provide a license for your code. If unsure, MIT is a common choice for prototypes. Do not assume redistribution rights for original game assets.


