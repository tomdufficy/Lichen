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
        private readonly RadioButton _alignmentEvenRadio;
        private readonly RadioButton _alignmentLeftRadio;
        private readonly RadioButton _alignmentCentreRadio;
        private readonly RadioButton _alignmentRightRadio;
        private readonly DynamicLayout _alignmentLayout;

        public FacadeModule SelectedModule { get; private set; }
        public bool WasApplied { get; private set; }

        public bool StretchVertically => _verticalStretchRadio.Checked;
        public bool StretchHorizontally => _horizontalStretchRadio.Checked;

        public HorizontalAlignmentMode HorizontalAlignment
        {
            get
            {
                if (_alignmentLeftRadio.Checked)
                    return HorizontalAlignmentMode.Left;

                if (_alignmentCentreRadio.Checked)
                    return HorizontalAlignmentMode.Centre;

                if (_alignmentRightRadio.Checked)
                    return HorizontalAlignmentMode.Right;

                return HorizontalAlignmentMode.EvenSpacing;
            }
        }

        public FacadeBrowserDialog(
            IReadOnlyList<FacadeModule> modules,
            bool browseOnly = true)
        {
            Title = "Lichen Facade Library";
            ClientSize = new Size(1100, 720);
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

            _alignmentEvenRadio = new RadioButton
            {
                Text = "Even spacing",
                Checked = true
            };

            _alignmentLeftRadio = new RadioButton(_alignmentEvenRadio)
            {
                Text = "Left"
            };

            _alignmentCentreRadio = new RadioButton(_alignmentEvenRadio)
            {
                Text = "Centre"
            };

            _alignmentRightRadio = new RadioButton(_alignmentEvenRadio)
            {
                Text = "Right"
            };

            _alignmentLayout = new DynamicLayout
            {
                Spacing = new Size(5, 4)
            };

            _alignmentLayout.Add(CreateHeadingLabel("Horizontal Alignment"));
            _alignmentLayout.Add(_alignmentEvenRadio);
            _alignmentLayout.Add(_alignmentLeftRadio);
            _alignmentLayout.Add(_alignmentCentreRadio);
            _alignmentLayout.Add(_alignmentRightRadio);

            _horizontalStretchRadio.CheckedChanged += delegate
            {
                UpdateAlignmentEnabledState();
            };

            _horizontalPreserveRadio.CheckedChanged += delegate
            {
                UpdateAlignmentEnabledState();
            };

            _applyButton = new Button
            {
                Text = "Apply",
                Enabled = false
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
                selectedPanel.Add(_alignmentLayout);
            }

            selectedPanel.Add(null);

            Button closeButton = new Button
            {
                Text = browseOnly ? "Close" : "Cancel"
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
            mainLayout.Add(selectedPanel, false, true);
            mainLayout.EndHorizontal();

            mainLayout.BeginHorizontal();
            mainLayout.Add(null, true);
            mainLayout.Add(closeButton);

            if (!browseOnly)
                mainLayout.Add(_applyButton);

            mainLayout.EndHorizontal();

            Content = mainLayout;
            UpdateAlignmentEnabledState();
        }

        private static Label CreateHeadingLabel(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font(SystemFont.Bold, 11)
            };
        }

        private void UpdateAlignmentEnabledState()
        {
            bool enabled = _horizontalPreserveRadio.Checked;

            _alignmentEvenRadio.Enabled = enabled;
            _alignmentLeftRadio.Enabled = enabled;
            _alignmentCentreRadio.Enabled = enabled;
            _alignmentRightRadio.Enabled = enabled;
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