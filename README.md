<p align="center">
  <img src="assets/brand/lichen-icon-512.png" width="128" alt="Lichen logo">
</p>

# Lichen

Modular facade generation tools for Rhino 8.

Lichen applies reusable facade modules to simple building massing models with planar vertical facade faces. It is intended for quickly testing facade systems on architectural massing while keeping the generated facade, slab, corner, and master geometry editable in Rhino.

## Demo

<p align="center">
  <img src="assets/demo/functionexample.gif" alt="Lichen demo" width="900">
</p>

## Installation

1. Open **Rhino Package Manager**
2. Enable **Include pre-releases**
3. Search for **Lichen**
4. Install

## Commands

### LichenApply

`LichenApply` is the main Lichen command. It opens the facade library, lets you choose how the facade should be fitted to the building, and then applies the selected module to one or more building massing Breps.

#### Basic workflow

1. Run `LichenApply`.
2. Select a facade module from the library.
3. Choose the vertical and horizontal fitting options.
4. Optionally enable corner placeholders, floor slabs, and/or ceiling slabs.
5. Click **Apply**.
6. Select one or more building massing Breps with planar vertical facade faces.

Lichen imports the selected facade as a Rhino block definition, places facade block instances on each supported facade face, and creates the associated Lichen layers and master geometry.

#### Vertical Stretch

**Stretch modules vertically to fit facade** — **default**

Lichen determines how many complete module rows best fit the facade height, then scales the modules vertically so those rows exactly fill the full height of the facade.

**Repeat modules vertically if space permits**

Lichen preserves the module's original height and places as many complete rows as will fit. The modules are not vertically stretched. If the facade height is not an exact multiple of the module height, unused space can remain above the final row.

At least one row is placed, even when the facade is shorter than the selected module.

#### Horizontal Stretch

**Stretch modules horizontally to fit facade** — **default**

Lichen determines how many modules best fit the facade width, then scales them horizontally so the row exactly fills the facade from end to end.

When horizontal stretching is enabled, horizontal alignment options are not used because the stretched modules already fill the available width.

**Preserve module width**

Lichen keeps the original module width and places as many complete modules as will fit across the facade. The remaining width is handled using the selected **Horizontal Alignment** option.

At least one module is placed, even when the facade is narrower than the selected module.

#### Horizontal Alignment

These options are available when **Preserve module width** is selected.

**Even spacing** — **default**

The unused width is divided into equal gaps before, between, and after the facade modules.

**Left**

Modules begin at the left side of the facade and any unused width remains at the right.

**Centre**

The row of modules is centred on the facade, dividing the unused width equally between both ends.

**Right**

Modules finish at the right side of the facade and any unused width remains at the left.

#### Corner placeholders

**Generate corner placeholders** — **off by default**

When enabled, Lichen generates editable block placeholders wherever two placed facade runs meet at a vertical corner.

Each placeholder is helper linework rather than finished corner geometry. In plan, it follows the two adjoining facade directions for **500 mm** from the corner and offsets **200 mm outward** from the building. The same closed outline is created at the bottom and top of the corresponding facade module span, with vertical helper lines connecting the two.

Corner placeholders:

- are generated only where both adjoining faces actually receive facade modules;
- are generated once per overlapping vertical facade-module span;
- distinguish between convex and concave corners;
- create separate definitions for different corner angles;
- create separate definitions where the required corner height differs;
- are associated with the selected facade module;
- are not stretched after creation — the block definition is created at the required height;
- reuse an existing matching corner definition if one already exists.

Because matching corners are block instances, editing a corner master updates all instances using that same corner definition.

#### Floor slabs

**Generate floor slabs** — **off by default**

When enabled, Lichen creates slab geometry from the lowest suitable horizontal face or faces of each selected volume.

The slab boundary follows the building footprint but is inset to account for any part of the selected facade module that extends inward from the facade line. This prevents generated slabs from unnecessarily overlapping the inward depth of the facade module.

**Floor thickness** controls the slab thickness in millimetres.

Default: **500 mm**

The floor slab is extruded downward from the original bottom face elevation.

#### Ceiling slabs

**Generate ceiling slabs** — **off by default**

When enabled, Lichen creates slab geometry from the highest suitable horizontal face or faces of each selected volume.

As with floor slabs, the slab boundary is inset by the inward depth of the selected facade module.

**Ceiling thickness** controls the slab thickness in millimetres.

Default: **500 mm**

The ceiling slab is extruded downward from the original top face elevation.

#### Facade masters

The first time a facade module is imported into a Rhino document, Lichen creates a master instance away from the building geometry.

The master area provides a reference copy of the facade block together with:

- the facade module name;
- a marked facade line;
- labels indicating the **outside** and **inside** directions.

Facade masters are arranged in **10 m × 10 m** cells, with subsequent facade masters placed in rows below the previous masters.

If the same facade module already exists in the document, Lichen reuses the existing block definition rather than importing another copy or creating another facade master.

#### Corner masters

When corner placeholders are enabled, each newly required corner definition receives its own master beside the associated facade master.

Corner masters are placed sequentially to the right of the facade master. Existing matching corner definitions and masters are reused rather than overwritten, allowing a placeholder master to be edited into a project-specific corner design without losing those edits when `LichenApply` is run again.

#### Generated layers

Lichen organises generated geometry under a top-level `Lichen` layer:

- `Lichen::Facades` — placed facade block instances
- `Lichen::Corners` — placed corner placeholder instances and their helper geometry
- `Lichen::Slabs::Floor` — generated floor slabs
- `Lichen::Slabs::Ceiling` — generated ceiling slabs
- `Lichen::Masters` — facade and corner master instances
- `Lichen::Masters::Admin` — master-area boxes, labels, and guide geometry

Imported facade block geometry retains its source layer structure beneath the Lichen layer hierarchy.

#### Input geometry

Lichen is designed for simple building massing Breps with **planar vertical facade faces**.

- Facade faces must be planar and within approximately **1° of vertical**.
- Curved facade faces are not currently supported.
- Sloped facade faces are not treated as facade faces.
- If a selected volume contains an unsupported curved facade face, the entire volume is skipped rather than partially processed.
- If no planar vertical facade faces are found, the volume is skipped.
- The footprint does not need to be rectangular. Polygonal, chamfered, stepped, and other straight-sided massing forms can be used as long as their facade faces meet the requirements above.

### LichenList

`LichenList` opens the facade library in browse-only mode and lists the facade modules currently installed with Lichen. No geometry is generated.

## Included Facades

Lichen includes a small library of example facade modules. The library is intended to grow over time as additional modules are released.

Each module has an authored width and height that Lichen uses when calculating repetition and stretching in `LichenApply`.

## Facade Library

*This section is automatically generated.*

<!-- FACADE_LIBRARY_START -->

<table>
  <tr>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_000.png" width="100%"><br>
      <strong>Lichen_Facade_000</strong><br>
      <sub>3600 x 3500 mm</sub><br>
      <sub>Basic placeholder facade.</sub>
    </td>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_001.png" width="100%"><br>
      <strong>Lichen_Facade_001</strong><br>
      <sub>3100 x 3000 mm</sub><br>
      <sub>Grey brick facade with a metal handrail.</sub>
    </td>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_002.png" width="100%"><br>
      <strong>Lichen_Facade_002</strong><br>
      <sub>3500 x 3000 mm</sub><br>
      <sub>Beige brick facade with complex window.</sub>
    </td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_003.png" width="100%"><br>
      <strong>Lichen_Facade_003</strong><br>
      <sub>3500 x 3000 mm</sub><br>
      <sub>Red brick facade with surface articulation, a handrail, and a loggia.</sub>
    </td>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_004_a.png" width="100%"><br>
      <strong>Lichen_Facade_004_a</strong><br>
      <sub>3600 x 4000 mm</sub><br>
      <sub>Two tone timber facade with a concrete base and a large door/window. Suitable for a public ground floor.</sub>
    </td>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_004_b.png" width="100%"><br>
      <strong>Lichen_Facade_004_b</strong><br>
      <sub>3600 x 4000 mm</sub><br>
      <sub>Two tone timber facade with a concrete base and a large window. Suitable for a public ground floor.</sub>
    </td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_004_c.png" width="100%"><br>
      <strong>Lichen_Facade_004_c</strong><br>
      <sub>3600 x 3000 mm</sub><br>
      <sub>Two tone timber facade with a handrail/french balcony and a large window. Suitable for residential.</sub>
    </td>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_005_a.png" width="100%"><br>
      <strong>Lichen_Facade_005_a</strong><br>
      <sub>3600 x 4000 mm</sub><br>
      <sub>Two tone brick facade with a very large window/door. Suitable for a public ground floor or main entrance.</sub>
    </td>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_005_b.png" width="100%"><br>
      <strong>Lichen_Facade_005_b</strong><br>
      <sub>3600 x 4000 mm</sub><br>
      <sub>Two tone brick facade with a very large window. Window is raised making it suitable for a residential ground floor.</sub>
    </td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_005_c.png" width="100%"><br>
      <strong>Lichen_Facade_005_c</strong><br>
      <sub>3600 x 3000 mm</sub><br>
      <sub>Two tone brick facade with window/door and french balcony/handrail with a zig-zag pattern. Suitable for residential.</sub>
    </td>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_005_d.png" width="100%"><br>
      <strong>Lichen_Facade_005_d</strong><br>
      <sub>3600 x 3000 mm</sub><br>
      <sub>Two tone brick facade with window/door and deep loggia balcony/handrail with a zig-zag pattern. Suitable for residential.</sub>
    </td>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_006.png" width="100%"><br>
      <strong>Lichen_Facade_006</strong><br>
      <sub>2980 x 3000 mm</sub><br>
      <sub>Blue alu facade with a lowered metal facade expression and three-part window.</sub>
    </td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_007_a.png" width="100%"><br>
      <strong>Lichen_Facade_007_a</strong><br>
      <sub>2980 x 3500 mm</sub><br>
      <sub>Yellow brick facade with an awning and a 2-part window. Includes timber columns on the interior.</sub>
    </td>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_007_b.png" width="100%"><br>
      <strong>Lichen_Facade_007_b</strong><br>
      <sub>2980 x 3500 mm</sub><br>
      <sub>Yellow brick facade with an awning and a full-height 2-part window. Includes timber columns on the interior.</sub>
    </td>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_008.png" width="100%"><br>
      <strong>Lichen_Facade_008</strong><br>
      <sub>6000 x 4500 mm</sub><br>
      <sub>Red brick facade with a wide, 4-part window/door and a zig-zag pattern expression above. Suitable for a public ground floor.</sub>
    </td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_009_a.png" width="100%"><br>
      <strong>Lichen_Facade_009_a</strong><br>
      <sub>3400 x 3200 mm</sub><br>
      <sub>Blue metal profile facade, with a 3-part window. Suitable for residential.</sub>
    </td>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_009_b.png" width="100%"><br>
      <strong>Lichen_Facade_009_b</strong><br>
      <sub>3400 x 3200 mm</sub><br>
      <sub>Blue metal profile facade, with an off-center tall single window. Suitable for residential.</sub>
    </td>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_010_a.png" width="100%"><br>
      <strong>Lichen_Facade_010_a</strong><br>
      <sub>6000 x 8500 mm</sub><br>
      <sub>Tan stone facade with a very large glass window/door opening and a zig-zag pattern detail. Very wide and high, suitable for a public ground floor.</sub>
    </td>
  </tr>
  <tr>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_010_b.png" width="100%"><br>
      <strong>Lichen_Facade_010_b</strong><br>
      <sub>4000 x 8500 mm</sub><br>
      <sub>Tan stone facade with a very large glass window/door opening and a zig-zag pattern detail. High, suitable for a public ground floor.</sub>
    </td>
    <td align="center" valign="top" width="33%">
      <img src="assets/facade-library/Lichen_Facade_010_c.png" width="100%"><br>
      <strong>Lichen_Facade_010_c</strong><br>
      <sub>2000 x 3730 mm</sub><br>
      <sub>Tan stone facade with glass window/door opening and a zig-zag pattern detail. Suitable for a public or residential.</sub>
    </td>
  </tr>
</table>
<!-- FACADE_LIBRARY_END -->

## Roadmap

- Additional facade modules
- Toolbar

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.