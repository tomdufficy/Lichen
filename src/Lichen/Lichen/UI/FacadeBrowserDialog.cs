using System.Collections.Generic;
using System.IO;
using Eto.Drawing;
using Eto.Forms;
using Lichen.Core;

namespace Lichen.UI
{
    public class FacadeBrowserDialog : Dialog
    {
        private readonly ImageView _selectedPreview;
        private readonly Label _selectedName;
        private readonly Label _selectedDescription;
        private readonly Label _selectedDimensions;
        private readonly Button _applyButton;

        private readonly RadioButton _verticalStretchRadio;
        private readonly RadioButton _verticalRepeatRadio;
        private readonly RadioButton _horizontalStretchRadio;
        private readonly RadioButton _horizontalPreserveRadio;
        private readonly RadioButton _placementCenteredRadio;
        private readonly RadioButton _placementEndToEndRadio;
        private readonly RadioButton _edgeHalfRadio;
        private readonly RadioButton _edgeEqualRadio;
        private readonly DynamicLayout _fixedWidthLayout;
        private readonly CheckBox _generateGapFillersCheckBox;
        private readonly CheckBox _includeEdgeFillersCheckBox;
        private readonly CheckBox _generateFloorSlabsCheckBox;
        private readonly CheckBox _generateCeilingSlabsCheckBox;
        private readonly CheckBox _generateCornerPlaceholdersCheckBox;
        private readonly NumericStepper _floorThicknessStepper;
        private readonly NumericStepper _ceilingThicknessStepper;

        public FacadeModule SelectedModule { get; private set; }
        public bool WasApplied { get; private set; }

        public bool StretchVertically => _verticalStretchRadio.Checked;
        public bool StretchHorizontally => _horizontalStretchRadio.Checked;
        public bool GenerateGapFillers =>
            _horizontalPreserveRadio.Checked &&
            _generateGapFillersCheckBox.Checked == true;
        public bool IncludeEdgeGapFillers =>
            GenerateGapFillers &&
            _placementCenteredRadio.Checked &&
            _includeEdgeFillersCheckBox.Checked == true;
        public bool GenerateFloorSlabs => _generateFloorSlabsCheckBox.Checked == true;
        public bool GenerateCeilingSlabs => _generateCeilingSlabsCheckBox.Checked == true;
        public bool GenerateCornerPlaceholders => _generateCornerPlaceholdersCheckBox.Checked == true;
        public double FloorSlabThicknessMm => _floorThicknessStepper.Value;
        public double CeilingSlabThicknessMm => _ceilingThicknessStepper.Value;

        public HorizontalPlacementMode HorizontalPlacement =>
            _placementEndToEndRadio.Checked
                ? HorizontalPlacementMode.EndToEnd
                : HorizontalPlacementMode.Centered;

        public CenteredEdgeGapMode CenteredEdgeGaps =>
            _edgeEqualRadio.Checked
                ? CenteredEdgeGapMode.EqualToInternalGap
                : CenteredEdgeGapMode.HalfInternalGap;

        public FacadeBrowserDialog(
            IReadOnlyList<FacadeModule> modules,
            bool browseOnly = true)
        {
            Title = "Lichen Facade Library";
            ClientSize = new Size(1200, 1000);
            Padding = new Padding(10);
            Resizable = true;

            WasApplied = false;

            _selectedPreview = new ImageView
            {
                Size = new Size(300, 300)
            };

            _selectedName = new Label
            {
                Text = "Select a facade",
                TextAlignment = TextAlignment.Center,
                Wrap = WrapMode.Word,
                Font = new Font(SystemFont.Bold, 14)
            };

            _selectedDescription = new Label
            {
                Text = string.Empty,
                TextAlignment = TextAlignment.Left,
                Wrap = WrapMode.Word
            };

            _selectedDimensions = new Label
            {
                Text = string.Empty,
                TextAlignment = TextAlignment.Center
            };

            _verticalStretchRadio = new RadioButton
            {
                Text = "Stretch modules vertically to fit facade",
                Checked = true
            };

            _verticalRepeatRadio = new RadioButton(_verticalStretchRadio)
            {
                Text = "Repeat modules vertically if space permits"
            };

            _horizontalStretchRadio = new RadioButton
            {
                Text = "Stretch modules horizontally to fit facade",
                Checked = true
            };

            _horizontalPreserveRadio = new RadioButton(_horizontalStretchRadio)
            {
                Text = "Preserve module width"
            };

            _placementCenteredRadio = new RadioButton
            {
                Text = "Centered",
                Checked = true
            };

            _placementEndToEndRadio = new RadioButton(_placementCenteredRadio)
            {
                Text = "End-to-end"
            };

            _edgeHalfRadio = new RadioButton
            {
                Text = "Edge gaps = half internal gap",
                Checked = true
            };

            _edgeEqualRadio = new RadioButton(_edgeHalfRadio)
            {
                Text = "Edge gaps = internal gap"
            };

            _generateGapFillersCheckBox = new CheckBox
            {
                Text = "Generate gap fillers",
                Checked = false
            };

            _includeEdgeFillersCheckBox = new CheckBox
            {
                Text = "Include edge fillers",
                Checked = false
            };

            _generateFloorSlabsCheckBox = new CheckBox
            {
                Text = "Generate floor slabs",
                Checked = false
            };

            _generateCeilingSlabsCheckBox = new CheckBox
            {
                Text = "Generate ceiling slabs",
                Checked = false
            };

            _generateCornerPlaceholdersCheckBox = new CheckBox
            {
                Text = "Generate corner placeholders",
                Checked = false
            };

            _floorThicknessStepper = CreateThicknessStepper();
            _ceilingThicknessStepper = CreateThicknessStepper();

            _fixedWidthLayout = new DynamicLayout
            {
                Spacing = new Size(5, 4)
            };
            _fixedWidthLayout.Add(CreateHeadingLabel("Horizontal Placement"));
            _fixedWidthLayout.Add(_placementCenteredRadio);
            _fixedWidthLayout.Add(_edgeHalfRadio);
            _fixedWidthLayout.Add(_edgeEqualRadio);
            _fixedWidthLayout.Add(_placementEndToEndRadio);
            _fixedWidthLayout.Add(_generateGapFillersCheckBox);
            _fixedWidthLayout.Add(_includeEdgeFillersCheckBox);

            _horizontalStretchRadio.CheckedChanged += delegate { UpdateHorizontalEnabledState(); };
            _horizontalPreserveRadio.CheckedChanged += delegate { UpdateHorizontalEnabledState(); };
            _placementCenteredRadio.CheckedChanged += delegate { UpdateHorizontalEnabledState(); };
            _placementEndToEndRadio.CheckedChanged += delegate { UpdateHorizontalEnabledState(); };
            _generateGapFillersCheckBox.CheckedChanged += delegate { UpdateHorizontalEnabledState(); };

            _generateFloorSlabsCheckBox.CheckedChanged += delegate { UpdateSlabEnabledState(); };
            _generateCeilingSlabsCheckBox.CheckedChanged += delegate { UpdateSlabEnabledState(); };

            _applyButton = new Button
            {
                Text = "Apply",
                Enabled = false,
                Size = new Size(100, 28)
            };

            _applyButton.Click += delegate
            {
                if (SelectedModule == null)
                    return;

                WasApplied = true;
                Close();
            };

            Control facadeGrid = CreateFacadeGrid(modules);

            Scrollable scrollableGrid = new Scrollable
            {
                Content = facadeGrid,
                ExpandContentWidth = false,
                Border = BorderType.None
            };

            DynamicLayout selectedPanel = new DynamicLayout
            {
                Padding = new Padding(10),
                Spacing = new Size(5, 10),
                Width = 330,
                MinimumSize = new Size(330, 0)
            };

            selectedPanel.AddCentered(_selectedPreview);
            selectedPanel.AddCentered(_selectedName);
            selectedPanel.AddCentered(_selectedDimensions);
            selectedPanel.Add(_selectedDescription);

            if (!browseOnly)
            {
                selectedPanel.Add(CreateHeadingLabel("Vertical Stretch"));
                selectedPanel.Add(_verticalStretchRadio);
                selectedPanel.Add(_verticalRepeatRadio);

                selectedPanel.Add(CreateHeadingLabel("Horizontal Stretch"));
                selectedPanel.Add(_horizontalStretchRadio);
                selectedPanel.Add(_horizontalPreserveRadio);
                selectedPanel.Add(_fixedWidthLayout);

                selectedPanel.Add(CreateHeadingLabel("Corners"));
                selectedPanel.Add(_generateCornerPlaceholdersCheckBox);

                selectedPanel.Add(CreateHeadingLabel("Slabs"));
                selectedPanel.Add(_generateFloorSlabsCheckBox);
                selectedPanel.Add(CreateThicknessRow("Floor thickness", _floorThicknessStepper));
                selectedPanel.Add(_generateCeilingSlabsCheckBox);
                selectedPanel.Add(CreateThicknessRow("Ceiling thickness", _ceilingThicknessStepper));
            }

            selectedPanel.Add(null);

            Scrollable selectedScrollable = new Scrollable
            {
                Content = selectedPanel,
                ExpandContentWidth = true,
                Border = BorderType.None,
                Width = 350,
                MinimumSize = new Size(350, 0)
            };

            Button closeButton = new Button
            {
                Text = browseOnly ? "Close" : "Cancel",
                Size = new Size(100, 28)
            };

            closeButton.Click += delegate
            {
                WasApplied = false;
                Close();
            };

            AbortButton = closeButton;

            if (!browseOnly)
                DefaultButton = _applyButton;

            DynamicLayout mainLayout = new DynamicLayout
            {
                Spacing = new Size(10, 10)
            };

            mainLayout.BeginHorizontal();
            mainLayout.Add(scrollableGrid, true, true);
            mainLayout.Add(selectedScrollable, false, true);
            mainLayout.EndHorizontal();

            mainLayout.BeginHorizontal();
            mainLayout.Add(null, true);

            if (!browseOnly)
                mainLayout.Add(_applyButton);

            mainLayout.Add(closeButton);
            mainLayout.EndHorizontal();

            Content = mainLayout;
            UpdateHorizontalEnabledState();
            UpdateSlabEnabledState();
        }

        private static NumericStepper CreateThicknessStepper()
        {
            return new NumericStepper
            {
                Value = 500.0,
                MinValue = 1.0,
                MaxValue = 10000.0,
                Increment = 50.0,
                DecimalPlaces = 0,
                Width = 100
            };
        }

        private static Control CreateThicknessRow(string labelText, NumericStepper stepper)
        {
            return new TableLayout
            {
                Spacing = new Size(8, 0),
                Rows =
                {
                    new TableRow(
                        new Label { Text = labelText },
                        stepper,
                        new Label { Text = "mm" })
                }
            };
        }

        private static Label CreateHeadingLabel(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font(SystemFont.Bold, 11)
            };
        }

        private void UpdateHorizontalEnabledState()
        {
            bool fixedWidth = _horizontalPreserveRadio.Checked;
            bool centered = fixedWidth && _placementCenteredRadio.Checked;
            bool gaps = fixedWidth && _generateGapFillersCheckBox.Checked == true;

            _placementCenteredRadio.Enabled = fixedWidth;
            _placementEndToEndRadio.Enabled = fixedWidth;
            _edgeHalfRadio.Enabled = centered;
            _edgeEqualRadio.Enabled = centered;
            _generateGapFillersCheckBox.Enabled = fixedWidth;
            _includeEdgeFillersCheckBox.Enabled = gaps && centered;
        }

        private void UpdateSlabEnabledState()
        {
            _floorThicknessStepper.Enabled = _generateFloorSlabsCheckBox.Checked == true;
            _ceilingThicknessStepper.Enabled = _generateCeilingSlabsCheckBox.Checked == true;
        }

        private Control CreateFacadeGrid(
            IReadOnlyList<FacadeModule> modules)
        {
            const int columns = 3;

            TableLayout table = new TableLayout
            {
                Spacing = new Size(4, 4)
            };

            for (int index = 0;
                 index < modules.Count;
                 index += columns)
            {
                TableRow row = new TableRow();

                for (int column = 0;
                     column < columns;
                     column++)
                {
                    int moduleIndex = index + column;

                    if (moduleIndex < modules.Count)
                    {
                        row.Cells.Add(
                            new TableCell(
                                CreateFacadeTile(
                                    modules[moduleIndex]),
                                false));
                    }
                    else
                    {
                        row.Cells.Add(new TableCell());
                    }
                }

                table.Rows.Add(row);
            }

            return table;
        }

        private Control CreateFacadeTile(FacadeModule module)
        {
            ImageView preview = new ImageView
            {
                Size = new Size(180, 180)
            };

            if (!string.IsNullOrWhiteSpace(module.PreviewPath) &&
                File.Exists(module.PreviewPath))
            {
                preview.Image = new Bitmap(module.PreviewPath);
            }

            Panel tilePanel = new Panel
            {
                Content = preview,
                Padding = new Padding(0),
                MinimumSize = new Size(180, 180)
            };

            tilePanel.MouseDown += delegate
            {
                SelectModule(module);
            };

            preview.MouseDown += delegate
            {
                SelectModule(module);
            };

            return tilePanel;
        }

        private void SelectModule(FacadeModule module)
        {
            SelectedModule = module;
            _selectedName.Text = module.Name;
            _selectedDescription.Text = module.Description ?? string.Empty;
            _selectedDimensions.Text = FormatDimensions(module);
            _applyButton.Enabled = true;

            if (!string.IsNullOrWhiteSpace(module.PreviewPath) &&
                File.Exists(module.PreviewPath))
            {
                _selectedPreview.Image =
                    new Bitmap(module.PreviewPath);
            }
            else
            {
                _selectedPreview.Image = null;
            }
        }

        private static string FormatDimensions(FacadeModule module)
        {
            if (module.WidthMm <= 0 || module.HeightMm <= 0)
                return string.Empty;

            return string.Format(
                "{0:0} mm wide x {1:0} mm high",
                module.WidthMm,
                module.HeightMm);
        }
    }
}
