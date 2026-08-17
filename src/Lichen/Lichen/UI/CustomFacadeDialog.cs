using Eto.Drawing;
using Eto.Forms;
using Lichen.Core;
using Rhino;
using System.Text;

namespace Lichen.UI
{
    public class CustomFacadeDialog : Dialog
    {
        private readonly RhinoDoc _doc;
        private readonly TextBox _nameTextBox;
        private readonly Label _blockNameLabel;
        private readonly Label _validationLabel;
        private readonly NumericStepper _widthStepper;
        private readonly NumericStepper _heightStepper;
        private readonly NumericStepper _depthInsideStepper;
        private readonly NumericStepper _depthOutsideStepper;
        private readonly RadioButton _centeredRadio;
        private readonly RadioButton _endToEndRadio;
        private readonly RadioButton _halfEdgeRadio;
        private readonly RadioButton _equalEdgeRadio;
        private readonly CheckBox _generateGapFillersCheckBox;
        private readonly CheckBox _includeEdgeFillersCheckBox;
        private readonly CheckBox _generateCornersCheckBox;
        private readonly CheckBox _generateFloorSlabsCheckBox;
        private readonly CheckBox _generateCeilingSlabsCheckBox;
        private readonly NumericStepper _floorThicknessStepper;
        private readonly NumericStepper _ceilingThicknessStepper;

        public CustomFacadeDialog(RhinoDoc doc)
        {
            _doc = doc;

            Title = "Lichen Custom Facade";
            ClientSize = new Size(520, 700);
            Padding = new Padding(16);
            Resizable = false;
            WasApplied = false;

            _nameTextBox = new TextBox { PlaceholderText = "MyFacade" };
            _blockNameLabel = new Label { Text = "Lichen_Custom_" };
            _validationLabel = new Label
            {
                Text = string.Empty,
                TextColor = Colors.Red,
                Wrap = WrapMode.Word
            };

            _widthStepper = CreateDimensionStepper(3000.0, 1.0);
            _heightStepper = CreateDimensionStepper(3500.0, 1.0);
            _depthInsideStepper = CreateDimensionStepper(300.0, 0.0);
            _depthOutsideStepper = CreateDimensionStepper(0.0, 0.0);

            _centeredRadio = new RadioButton
            {
                Text = "Centered",
                Checked = true
            };
            _endToEndRadio = new RadioButton(_centeredRadio)
            {
                Text = "End-to-end"
            };

            _halfEdgeRadio = new RadioButton
            {
                Text = "Edge gaps = half internal gap",
                Checked = true
            };
            _equalEdgeRadio = new RadioButton(_halfEdgeRadio)
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
            _generateCornersCheckBox = new CheckBox
            {
                Text = "Generate corner placeholders",
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
            _floorThicknessStepper = CreateThicknessStepper();
            _ceilingThicknessStepper = CreateThicknessStepper();

            _nameTextBox.TextChanged += delegate { UpdateNamePreview(); };
            _centeredRadio.CheckedChanged += delegate { UpdateEnabledState(); };
            _endToEndRadio.CheckedChanged += delegate { UpdateEnabledState(); };
            _generateGapFillersCheckBox.CheckedChanged += delegate { UpdateEnabledState(); };
            _generateFloorSlabsCheckBox.CheckedChanged += delegate { UpdateEnabledState(); };
            _generateCeilingSlabsCheckBox.CheckedChanged += delegate { UpdateEnabledState(); };

            var applyButton = new Button
            {
                Text = "Apply",
                Size = new Size(100, 28)
            };
            var cancelButton = new Button
            {
                Text = "Cancel",
                Size = new Size(100, 28)
            };

            applyButton.Click += delegate
            {
                if (!ValidateInput())
                    return;

                WasApplied = true;
                Close();
            };
            cancelButton.Click += delegate
            {
                WasApplied = false;
                Close();
            };

            DefaultButton = applyButton;
            AbortButton = cancelButton;

            var layout = new DynamicLayout
            {
                Spacing = new Size(8, 8)
            };

            layout.Add(CreateHeading("Facade"));
            layout.AddRow(new Label { Text = "Name", Width = 135 }, _nameTextBox);
            layout.AddRow(new Label { Text = "Block name", Width = 135 }, _blockNameLabel);
            layout.Add(_validationLabel);

            layout.Add(CreateHeading("Dimensions"));
            layout.Add(CreateDimensionRow("Width", _widthStepper));
            layout.Add(CreateDimensionRow("Height", _heightStepper));
            layout.Add(CreateDimensionRow("Depth inside", _depthInsideStepper));
            layout.Add(CreateDimensionRow("Depth outside", _depthOutsideStepper));

            layout.Add(CreateHeading("Horizontal placement"));
            layout.Add(_centeredRadio);
            layout.Add(Indented(_halfEdgeRadio));
            layout.Add(Indented(_equalEdgeRadio));
            layout.Add(_endToEndRadio);
            layout.Add(_generateGapFillersCheckBox);
            layout.Add(Indented(_includeEdgeFillersCheckBox));

            layout.Add(CreateHeading("Corners"));
            layout.Add(_generateCornersCheckBox);

            layout.Add(CreateHeading("Slabs"));
            layout.Add(_generateFloorSlabsCheckBox);
            layout.Add(Indented(CreateDimensionRow("Floor thickness", _floorThicknessStepper)));
            layout.Add(_generateCeilingSlabsCheckBox);
            layout.Add(Indented(CreateDimensionRow("Ceiling thickness", _ceilingThicknessStepper)));

            layout.Add(null);
            layout.BeginHorizontal();
            layout.Add(null, true);
            layout.Add(applyButton);
            layout.Add(cancelButton);
            layout.EndHorizontal();

            Content = layout;
            UpdateNamePreview();
            UpdateEnabledState();
        }

        public bool WasApplied { get; private set; }
        public string BlockName => "Lichen_Custom_" + SanitizeName(_nameTextBox.Text);
        public double WidthMm => _widthStepper.Value;
        public double HeightMm => _heightStepper.Value;
        public double DepthInsideMm => _depthInsideStepper.Value;
        public double DepthOutsideMm => _depthOutsideStepper.Value;
        public HorizontalPlacementMode HorizontalPlacement =>
            _endToEndRadio.Checked
                ? HorizontalPlacementMode.EndToEnd
                : HorizontalPlacementMode.Centered;
        public CenteredEdgeGapMode CenteredEdgeGaps =>
            _equalEdgeRadio.Checked
                ? CenteredEdgeGapMode.EqualToInternalGap
                : CenteredEdgeGapMode.HalfInternalGap;
        public bool GenerateGapFillers => _generateGapFillersCheckBox.Checked == true;
        public bool IncludeEdgeGapFillers =>
            GenerateGapFillers &&
            _centeredRadio.Checked &&
            _includeEdgeFillersCheckBox.Checked == true;
        public bool GenerateCornerPlaceholders => _generateCornersCheckBox.Checked == true;
        public bool GenerateFloorSlabs => _generateFloorSlabsCheckBox.Checked == true;
        public bool GenerateCeilingSlabs => _generateCeilingSlabsCheckBox.Checked == true;
        public double FloorSlabThicknessMm => _floorThicknessStepper.Value;
        public double CeilingSlabThicknessMm => _ceilingThicknessStepper.Value;

        private bool ValidateInput()
        {
            string sanitized = SanitizeName(_nameTextBox.Text);
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                _validationLabel.Text = "Enter a facade name.";
                return false;
            }

            if (WidthMm <= 0.0 || HeightMm <= 0.0)
            {
                _validationLabel.Text = "Width and height must be greater than 0 mm.";
                return false;
            }

            if (DepthInsideMm < 0.0 || DepthOutsideMm < 0.0 ||
                DepthInsideMm + DepthOutsideMm <= 0.0)
            {
                _validationLabel.Text =
                    "Depth inside and depth outside may be 0 mm individually, but their total must be greater than 0 mm.";
                return false;
            }

            if (!BlockManager.CustomFacadeDimensionsMatch(
                _doc,
                BlockName,
                WidthMm,
                HeightMm,
                DepthInsideMm,
                DepthOutsideMm))
            {
                _validationLabel.Text =
                    "A custom facade with this name already exists with different dimensions. Choose another name.";
                return false;
            }

            _validationLabel.Text = string.Empty;
            return true;
        }

        private void UpdateNamePreview()
        {
            _blockNameLabel.Text = "Lichen_Custom_" + SanitizeName(_nameTextBox.Text);
            _validationLabel.Text = string.Empty;
        }

        private void UpdateEnabledState()
        {
            bool centered = _centeredRadio.Checked;
            bool gaps = _generateGapFillersCheckBox.Checked == true;

            _halfEdgeRadio.Enabled = centered;
            _equalEdgeRadio.Enabled = centered;
            _includeEdgeFillersCheckBox.Enabled = centered && gaps;
            _floorThicknessStepper.Enabled = _generateFloorSlabsCheckBox.Checked == true;
            _ceilingThicknessStepper.Enabled = _generateCeilingSlabsCheckBox.Checked == true;
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var builder = new StringBuilder();
            bool lastWasUnderscore = false;

            foreach (char c in value.Trim())
            {
                if (char.IsLetterOrDigit(c) || c == '-')
                {
                    builder.Append(c);
                    lastWasUnderscore = false;
                }
                else if (char.IsWhiteSpace(c) || c == '_')
                {
                    if (!lastWasUnderscore && builder.Length > 0)
                    {
                        builder.Append('_');
                        lastWasUnderscore = true;
                    }
                }
            }

            return builder.ToString().Trim('_');
        }

        private static NumericStepper CreateDimensionStepper(double value, double minValue)
        {
            return new NumericStepper
            {
                Value = value,
                MinValue = minValue,
                MaxValue = 100000.0,
                Increment = 50.0,
                DecimalPlaces = 0,
                Width = 110
            };
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
                Width = 110
            };
        }

        private static Control CreateDimensionRow(string labelText, NumericStepper stepper)
        {
            return new TableLayout
            {
                Spacing = new Size(8, 0),
                Rows =
                {
                    new TableRow(
                        new Label { Text = labelText, Width = 135 },
                        stepper,
                        new Label { Text = "mm" })
                }
            };
        }

        private static Label CreateHeading(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font(SystemFont.Bold, 11)
            };
        }

        private static Control Indented(Control control)
        {
            return new Panel
            {
                Padding = new Padding(20, 0, 0, 0),
                Content = control
            };
        }
    }
}