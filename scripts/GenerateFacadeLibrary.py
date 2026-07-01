import os
import Rhino
import rhinoscriptsyntax as rs
import scriptcontext as sc
import System.Drawing


IMAGE_WIDTH = 1600
IMAGE_HEIGHT = 1200
ZOOM_SCALE = 0.92
GRID_COLUMNS = 3

SOURCE_RELATIVE = os.path.join("src", "Lichen", "Lichen", "facades")
OUTPUT_RELATIVE = os.path.join("assets", "facade-library")
README_RELATIVE = "README.md"

FACADE_LIBRARY_START = "<!-- FACADE_LIBRARY_START -->"
FACADE_LIBRARY_END = "<!-- FACADE_LIBRARY_END -->"


def find_repo_root(start_dir):
    current = os.path.abspath(start_dir)

    while True:
        candidate = os.path.join(current, SOURCE_RELATIVE)
        if os.path.isdir(candidate):
            return current

        parent = os.path.dirname(current)
        if parent == current:
            return None

        current = parent


def get_script_dir():
    try:
        return os.path.dirname(os.path.abspath(__file__))
    except:
        return os.getcwd()


def get_facade_files(folder):
    files = []

    for name in os.listdir(folder):
        if name.lower().endswith(".3dm"):
            files.append(os.path.join(folder, name))

    files.sort()
    return files


def get_generation_mode():
    mode = os.environ.get("LICHEN_FACADE_MODE", "SkipExisting")

    if mode not in ["SkipExisting", "RedoAll"]:
        mode = "SkipExisting"

    return mode


def get_document_bounding_box():
    bbox = Rhino.Geometry.BoundingBox.Empty

    for obj in sc.doc.Objects:
        if obj is None or obj.IsHidden or obj.IsDeleted:
            continue

        obj_bbox = obj.Geometry.GetBoundingBox(True)
        if obj_bbox.IsValid:
            bbox.Union(obj_bbox)

    return bbox


def padded_bounding_box(bbox):
    center = bbox.Center
    factor = 1.0 / ZOOM_SCALE
    points = []

    for corner in bbox.GetCorners():
        vector = corner - center
        points.append(center + vector * factor)

    return Rhino.Geometry.BoundingBox(points)


def set_catalogue_view():
    view = sc.doc.Views.ActiveView
    if not view:
        return False

    viewport = view.ActiveViewport

    shaded = Rhino.Display.DisplayModeDescription.FindByName("Shaded")
    if shaded:
        viewport.DisplayMode = shaded

    ok = rs.Command("_-Isometric _NE", False)
    if not ok:
        print("Failed to set NE Isometric view.")
        return False

    rs.Command("_Zoom _Extents", False)

    bbox = get_document_bounding_box()
    if bbox.IsValid:
        viewport.ZoomBoundingBox(padded_bounding_box(bbox))

    sc.doc.Views.Redraw()
    return True


def capture_view(output_path):
    view = sc.doc.Views.ActiveView
    if not view:
        return False

    capture = Rhino.Display.ViewCapture()
    capture.Width = IMAGE_WIDTH
    capture.Height = IMAGE_HEIGHT
    capture.ScaleScreenItems = False
    capture.DrawAxes = False
    capture.DrawGrid = False
    capture.DrawGridAxes = False
    capture.TransparentBackground = False

    bitmap = capture.CaptureToBitmap(view)
    if not bitmap:
        return False

    bitmap.Save(output_path, System.Drawing.Imaging.ImageFormat.Png)
    bitmap.Dispose()

    return True


def process_file(rhino_file, output_file):
    sc.doc.Modified = False

    rs.Command("_-New _None", False)
    sc.doc.Modified = False

    ok = rs.Command('_-Import "{}" _Enter'.format(rhino_file), False)
    if not ok:
        print("Failed to import file:")
        print(rhino_file)
        return False

    rs.UnselectAllObjects()
    sc.doc.Views.Redraw()

    if not set_catalogue_view():
        return False

    rs.UnselectAllObjects()
    sc.doc.Views.Redraw()

    if not capture_view(output_file):
        return False

    sc.doc.Modified = False
    return True


def generate_markdown_catalogue(repo_root):
    image_folder = os.path.join(repo_root, OUTPUT_RELATIVE)
    readme_path = os.path.join(repo_root, README_RELATIVE)

    images = []

    for name in os.listdir(image_folder):
        if name.lower().endswith(".png"):
            images.append(name)

    images.sort()

    catalogue = []
    catalogue.append("")
    catalogue.append('<table>')

    for i in range(0, len(images), GRID_COLUMNS):
        row = images[i:i + GRID_COLUMNS]
        catalogue.append("  <tr>")

        for image in row:
            title = os.path.splitext(image)[0]
            src = "assets/facade-library/{}".format(image)

            catalogue.append('    <td align="center" width="{}%">'.format(int(100 / GRID_COLUMNS)))
            catalogue.append('      <img src="{}" width="100%"><br>'.format(src))
            catalogue.append('      <sub>{}</sub>'.format(title))
            catalogue.append("    </td>")

        catalogue.append("  </tr>")

    catalogue.append("</table>")
    catalogue.append("")

    if not os.path.isfile(readme_path):
        print("README.md not found:")
        print(readme_path)
        return

    with open(readme_path, "r") as f:
        readme = f.read()

    start_index = readme.find(FACADE_LIBRARY_START)
    end_index = readme.find(FACADE_LIBRARY_END)

    if start_index == -1 or end_index == -1 or end_index < start_index:
        print("Could not find facade library markers in README.md")
        return

    before = readme[:start_index + len(FACADE_LIBRARY_START)]
    after = readme[end_index:]

    updated = before + "\n" + "\n".join(catalogue) + after

    with open(readme_path, "w") as f:
        f.write(updated)

    print("Updated README facade library section:")
    print(readme_path)


def main():
    script_dir = get_script_dir()
    repo_root = find_repo_root(script_dir)

    if not repo_root:
        repo_root = rs.BrowseForFolder(
            None,
            "Could not auto-find repo. Select the root Lichen repository folder."
        )

    if not repo_root:
        print("No repo folder selected.")
        return

    source_folder = os.path.join(repo_root, SOURCE_RELATIVE)
    output_folder = os.path.join(repo_root, OUTPUT_RELATIVE)

    if not os.path.isdir(source_folder):
        print("Could not find source folder:")
        print(source_folder)
        return

    if not os.path.isdir(output_folder):
        os.makedirs(output_folder)

    facade_files = get_facade_files(source_folder)

    if not facade_files:
        print("No .3dm facade files found.")
        return

    mode = get_generation_mode()
    redo_all = mode == "RedoAll"

    print("Generation mode: {}".format(mode))
    print("Source folder:")
    print(source_folder)
    print("Output folder:")
    print(output_folder)
    print("Found {} facade files.".format(len(facade_files)))

    generated = 0
    skipped = 0
    failed = 0

    for rhino_file in facade_files:
        base_name = os.path.splitext(os.path.basename(rhino_file))[0]
        output_file = os.path.join(output_folder, base_name + ".png")

        if os.path.exists(output_file) and not redo_all:
            print("Skipping existing: {}".format(base_name))
            skipped += 1
            continue

        print("Generating: {}".format(base_name))

        if process_file(rhino_file, output_file):
            generated += 1
        else:
            failed += 1
            print("FAILED: {}".format(base_name))

    generate_markdown_catalogue(repo_root)

    print("")
    print("Done.")
    print("Generated: {}".format(generated))
    print("Skipped: {}".format(skipped))
    print("Failed: {}".format(failed))

    if os.environ.get("LICHEN_CLOSE_RHINO", "0") == "1":
        sc.doc.Modified = False
        rs.Command("_-Exit _No", False)


if __name__ == "__main__":
    main()