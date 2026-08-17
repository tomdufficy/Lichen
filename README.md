<p align="center">
  <img src="assets/brand/lichen-icon-512.png" width="128" alt="Lichen logo">
</p>

# Lichen

Modular facade generation tools for Rhino 8.

Lichen applies reusable facade systems to simple building massing models with planar vertical facade faces. Use a facade from the included library with `LichenApply`, or create a project-specific wireframe module directly in Rhino with `LichenCustom`.

## Demo

<p align="center">
  <img src="assets/demo/functionexample.gif" alt="Lichen demo" width="900">
</p>

## Installation

1. Open **Rhino Package Manager**.
2. Enable **Include pre-releases**.
3. Search for **Lichen**.
4. Install.

## Commands

### LichenApply

`LichenApply` is the main library-based facade command.

1. Run `LichenApply`.
2. Choose a facade module from the library.
3. Choose how the module should repeat or stretch.
4. Optionally generate corner placeholders, gap fillers, floor slabs, and ceiling slabs.
5. Click **Apply**.
6. Select one or more building massing Breps.

Lichen imports the selected facade as a Rhino block definition, places block instances on every supported facade face, and creates editable master geometry away from the building model.

#### Vertical placement

**Stretch modules vertically to fit facade** - **default**

Lichen chooses a suitable number of rows and scales the modules vertically so those rows exactly fill the facade height.

**Repeat modules vertically if space permits**

The original module height is preserved. Lichen places as many complete rows as fit and leaves any remaining height above the final row empty. If the facade is shorter than one module, one full-height module is still placed.

#### Horizontal placement

**Stretch modules horizontally to fit facade** - **default**

Lichen chooses the module count by rounding `facade width / authored module width` to the nearest whole number, with a minimum of one module.

It then applies one common horizontal scale factor so that the complete row exactly fills the facade width:

`horizontal scale = facade width / (module count * authored module width)`

There are therefore no horizontal gaps in stretch mode. Fixed-width placement and gap filler options are not used.

**Preserve module width**

The facade module keeps its authored width exactly. Horizontal scale remains `1.0`.

Lichen first calculates:

`module count = floor(facade width / module width)`

with a minimum of one module.

For a facade wide enough to contain the modules, the unused width is:

`remaining width = facade width - (module count * module width)`

That remaining width is distributed according to the selected placement mode.

##### Centered

**Centered** is the default fixed-width placement mode.

The modules are distributed symmetrically, with equal edge gaps at the left and right ends.

Two different relationships between the edge gaps and internal gaps are available.

**Edge gaps = half internal gap** - **default**

The remaining width is divided by the number of modules. This gives the internal gap.

For `N` modules:

`internal gap = remaining width / N`

Each edge gap is half the internal gap:

`edge gap = internal gap / 2`

For example, consider a facade that is 10,000 mm wide using modules that are 3,000 mm wide.

Three modules fit:

`3 * 3,000 = 9,000 mm`

This leaves:

`10,000 - 9,000 = 1,000 mm`

With half-width edge gaps:

`internal gap = 1,000 / 3 = 333.3 mm`

`edge gap = 333.3 / 2 = 166.7 mm`

The resulting arrangement is therefore approximately:

`166.7 | MODULE | 333.3 | MODULE | 333.3 | MODULE | 166.7`

**Edge gaps = internal gap**

The remaining width is divided into `N + 1` equal gaps: one at each edge and one between every adjacent pair of modules.

For `N` modules:

`internal gap = edge gap = remaining width / (N + 1)`

Using the same 10,000 mm facade and three 3,000 mm modules:

`remaining width = 1,000 mm`

There are four gaps:

`gap = 1,000 / 4 = 250 mm`

The resulting arrangement is:

`250 | MODULE | 250 | MODULE | 250 | MODULE | 250`

##### End-to-end

When two or more fixed-width modules fit, the first module starts at one end of the facade and the last module finishes at the other.

There are no intentional edge gaps.

All remaining width is divided equally between the modules:

`internal gap = remaining width / (module count - 1)`

For example, with a 10,000 mm facade and three 3,000 mm modules:

`remaining width = 1,000 mm`

There are two internal gaps:

`internal gap = 1,000 / 2 = 500 mm`

The resulting arrangement is:

`MODULE | 500 | MODULE | 500 | MODULE`

When exactly one fixed-width module fits, end-to-end distribution is not possible between multiple modules, so Lichen centres that module and leaves equal space on both sides.

##### Facade narrower than one module

Lichen still places one full-width, unstretched module.

In **Centered** mode, the module is centred on the facade. If it is wider than the facade, it therefore overhangs equally at both ends.

In **End-to-end** mode, the module starts at the beginning of the facade run and extends past the far end.

No positive horizontal gap is recorded in this case.

#### Gap fillers

**Generate gap fillers** - **off by default**

Available only when module width is preserved.

Gap fillers are editable wireframe block placeholders generated from the horizontal gaps calculated by the fixed-width placement described above.

They are never generated between vertically stacked modules.

Each horizontal gap is repeated through every placed vertical module span. The filler height therefore matches the corresponding facade-module row height.

The filler depth is derived from the selected facade block, using its maximum extent inside and outside the facade line.

Separate gap definitions are created for different gap widths and heights, and matching definitions are reused rather than overwritten.

The placeholder wireframe geometry is stored on:

`Lichen::Gaps::Helplines`

Placed gap block instances are stored on:

`Lichen::Gaps`

**Include edge fillers** - **off by default**

Available when **Centered** placement and **Generate gap fillers** are both enabled.

When checked, fillers are generated in the two centred edge gaps as well as the internal gaps.

When unchecked, only gaps between adjacent facade modules receive fillers.

For **End-to-end** placement there are no intentional edge gaps, so this option is not used.

If only one module is placed, there are no internal gaps and therefore no internal gap filler. A filler can only be generated where a positive centred edge gap exists and edge fillers are enabled.

#### Corner placeholders

**Generate corner placeholders** - **off by default**

Creates editable block placeholders wherever two placed facade runs meet at a supported vertical corner.

Each placeholder uses helper linework rather than finished corner geometry.

In plan it follows both adjoining facade directions for **500 mm**, offsets **200 mm outward**, and closes the resulting shape.

The outline is repeated at the bottom and top of the corresponding facade-module span and connected with vertical helper lines.

Corner placeholders:

- are generated only where both adjoining faces receive facade modules;
- are created per overlapping vertical facade-module span;
- distinguish convex and concave corners;
- create separate definitions for different corner angles and required heights;
- are associated with the selected facade module;
- are created at their required height rather than vertically stretched;
- reuse existing matching definitions so edited corner masters are preserved.

Corner helper geometry is stored on:

`Lichen::Corners::Helplines`

Placed corner block instances are stored on:

`Lichen::Corners`

#### Floor slabs

**Generate floor slabs** - **off by default**

Creates slab geometry from the lowest suitable horizontal face or faces of each selected volume.

The slab boundary is inset by the facade module's inward depth so the slab does not unnecessarily overlap geometry extending inside the facade line.

**Floor thickness** defaults to **500 mm**.

Floor slabs extrude downward from the original bottom-face elevation.

#### Ceiling slabs

**Generate ceiling slabs** - **off by default**

Creates slab geometry from the highest suitable horizontal face or faces of each selected volume, using the same inward facade-depth offset as floor slabs.

**Ceiling thickness** defaults to **500 mm**.

Ceiling slabs extrude downward from the original top-face elevation.

### LichenCustom

`LichenCustom` bypasses the facade library and creates a project-specific facade module directly in the current Rhino document.

The custom module is a wireframe block that represents its width, height, and depth relative to the facade line.

Once created, it uses the same placement, corner, gap, slab, and master systems as a library facade.

#### Custom facade settings

**Name**

Required.

Lichen sanitises the entered name and creates a document-specific block named:

`Lichen_Custom_<Name>`

If a custom facade with the same name already exists and its original dimensions match, the existing definition is reused.

If the dimensions differ, Lichen requires a different name.

Custom facades are not added to the installed facade library.

**Width** - default **3000 mm**

Must be greater than 0 mm.

**Height** - default **3500 mm**

Must be greater than 0 mm.

**Depth inside** - default **300 mm**

Extends from the facade line into the building. May be 0 mm.

**Depth outside** - default **0 mm**

Extends from the facade line toward the exterior. May be 0 mm.

The combined inside and outside depth must be greater than 0 mm.

#### Custom facade placement

Custom modules are always placed at their specified width and height.

They are not horizontally or vertically stretched.

Vertically, Lichen repeats as many complete modules as fit and leaves any remaining space at the top empty.

If the facade is shorter than one module, one full module is still placed.

Horizontally, custom modules use the same fixed-width placement system described above.

Choose between:

- **Centered**, with either half-width or equal-width edge gaps;
- **End-to-end**, with equal internal gaps and no intentional edge gaps.

This means the authored custom module width is preserved and any remaining facade width becomes explicitly calculated spacing between or around modules.

`LichenCustom` also provides the same optional **gap fillers**, **edge fillers**, **corner placeholders**, **floor slabs**, and **ceiling slabs** described above.

Floor and ceiling thickness controls are enabled only when their corresponding slab option is checked.

The custom facade definition is created only after at least one valid building volume has been selected.

Cancelling or selecting only unsupported geometry does not leave an unused custom facade block in the document.

### Masters

Lichen creates an editable master area away from the building geometry.

#### Facade masters

The first time a facade definition is created or imported, Lichen places a master instance inside a **10 m x 10 m** master cell.

The cell includes:

- the facade module name;
- a dotted facade line;
- **outside** and **inside** labels.

Facade masters are arranged vertically, with each new facade occupying the next cell downward.

#### Corner and gap masters

New corner and gap definitions receive their own **10 m x 10 m** master cells to the right of their associated facade master.

They are added sequentially across the row.

Gap masters include the same dotted facade datum and **outside** / **inside** labels as facade masters.

Corner masters include two dotted datum rays meeting at the corner vertex and following the stored corner angle.

The **inside** and **outside** labels are placed on the appropriate sides according to whether the corner definition is convex or concave.

Existing matching definitions and masters are reused.

This allows the generated helper-line placeholders to be edited into project-specific facade, corner, or gap designs without those edits being overwritten on later runs.

### Generated layers

Lichen organises generated geometry beneath the top-level `Lichen` layer:

- `Lichen::Facades` - placed facade instances;
- `Lichen::Facades::Helplines` - pink helper/wireframe geometry for generated custom facades;
- `Lichen::Corners` - placed corner placeholder instances;
- `Lichen::Corners::Helplines` - pink helper/wireframe geometry used by generated corner definitions;
- `Lichen::Gaps` - placed horizontal gap filler instances;
- `Lichen::Gaps::Helplines` - pink helper/wireframe geometry used by generated gap definitions;
- `Lichen::Slabs::Floor` - generated floor slabs;
- `Lichen::Slabs::Ceiling` - generated ceiling slabs;
- `Lichen::Masters` - facade, corner, and gap master instances;
- `Lichen::Masters::Admin` - pink master cells, labels, dotted datum lines, and guide geometry.

Imported library facade geometry retains its source layer structure beneath the Lichen hierarchy.

### Input geometry

Both `LichenApply` and `LichenCustom` are designed for simple building massing Breps with **planar vertical facade faces**.

- Facade faces must be planar and within approximately **1 degree of vertical**.
- Curved facade faces are not supported.
- A selected volume containing an unsupported curved facade face is skipped rather than partially processed.
- Sloped faces are not treated as facade faces.
- A volume with no planar vertical facade faces is skipped.
- The footprint does not need to be rectangular. Polygonal, chamfered, stepped, and other straight-sided massing forms can be used as long as their facade faces meet the requirements above.

### LichenList

`LichenList` opens the installed facade library in browse-only mode.

It does not generate geometry and does not list document-specific facades created with `LichenCustom`.

## Included Facades

Lichen includes a small library of example facade modules.

The library is intended to grow over time as additional modules are released.

Each library module has an authored width and height used by `LichenApply` when calculating repetition and stretching.

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