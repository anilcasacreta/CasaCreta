using System;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.UI;
using Font = Eto.Drawing.Font;
using FontStyle = Eto.Drawing.FontStyle;

namespace CasaCreta.EtoForms
{
    /// <summary>
    /// Modeless Eto Form demonstrating geometry selection and clearing in Rhino.
    /// </summary>
    public class GeometryPickerForm : Form
    {
        private static GeometryPickerForm _instance;

        private readonly Button _btnPick;
        private readonly Button _btnClear;
        private readonly Label _lblStatus;

        /// <summary>
        /// Show or bring to front the singleton instance of the form.
        /// </summary>
        public static void ShowForm()
        {
            if (_instance == null || _instance.IsDisposed)
            {
                _instance = new GeometryPickerForm();
                _instance.Owner = RhinoEtoApp.MainWindow;
                _instance.Show();
            }
            else
            {
                _instance.BringToFront();
            }
        }

        public GeometryPickerForm()
        {
            Title = "Geometry Picker";
            ClientSize = new Size(380, 200);
            MinimumSize = new Size(340, 180);
            Padding = new Padding(20);
            Resizable = false;
            BackgroundColor = Colors.DarkGray;

            // 1. Geometry Picker Button
            _btnPick = new Button
            {
                Text = "Geometry Picker",
                Size = new Size(140, 42),
                Font = SystemFonts.Bold(10)
            };
            _btnPick.Click += OnPickGeometry;

            // 2. Clear Selection Button
            _btnClear = new Button
            {
                Text = "Clear Selection",
                Size = new Size(140, 42),
                Font = SystemFonts.Bold(10)
            };
            _btnClear.Click += OnClearSelection;

            // 3. Status Label
            _lblStatus = new Label
            {
                Text = "Click 'Geometry Picker' to select objects in Rhino viewport.",
                TextAlignment = TextAlignment.Center,
                TextColor = Colors.White,
                Font = SystemFonts.Default(9)
            };

            // 4. Layout
            var buttonsLayout = new TableLayout
            {
                Spacing = new Size(16, 0),
                Rows =
                {
                    new TableRow(
                        new TableCell(_btnPick, true),
                        new TableCell(_btnClear, true)
                    )
                }
            };

            var mainLayout = new DynamicLayout
            {
                Padding = new Padding(10),
                Spacing = new Size(10, 16)
            };

            mainLayout.Add(buttonsLayout);
            mainLayout.Add(null); // Spring spacer
            mainLayout.Add(_lblStatus);

            Content = mainLayout;

            Closed += (s, e) => _instance = null;
        }

        private void OnPickGeometry(object sender, EventArgs e)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                _lblStatus.Text = "Error: No active Rhino document.";
                return;
            }

            _lblStatus.Text = "Selecting in Rhino viewport...";

            // Use Rhino's interactive GetObject to allow selecting any geometry in the viewport
            var go = new GetObject();
            go.SetCommandPrompt("Select geometry in Rhino viewport (Press Enter when done)");
            go.GeometryFilter = ObjectType.AnyObject;
            go.SubObjectSelect = false;
            go.EnablePreSelect(true, true);
            go.DeselectAllBeforePostSelect = false;

            GetResult res = go.GetMultiple(1, 0);

            if (res == GetResult.Object)
            {
                int count = go.ObjectCount;
                // Highlight/select them in the Rhino document
                for (int i = 0; i < count; i++)
                {
                    RhinoObject obj = go.Object(i).Object();
                    if (obj != null)
                    {
                        doc.Objects.Select(obj.Id, true, true);
                    }
                }
                doc.Views.Redraw();
                _lblStatus.Text = $"Selected {count} object(s) successfully.";
            }
            else
            {
                _lblStatus.Text = "Selection finished / cancelled.";
            }
        }

        private void OnClearSelection(object sender, EventArgs e)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                _lblStatus.Text = "Error: No active Rhino document.";
                return;
            }

            int count = doc.Objects.UnselectAll();
            doc.Views.Redraw();
            _lblStatus.Text = $"Cleared selection ({count} object(s) unselected).";
        }
    }
}
