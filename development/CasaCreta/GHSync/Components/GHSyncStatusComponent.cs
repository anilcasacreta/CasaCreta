using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Grasshopper.Kernel;

using GH_IO.Serialization;

namespace CasaCreta.GHSync.Components
{
    public sealed class GHSyncStatusComponent : GH_Component
    {
        private Guid _targetComponentId = Guid.Empty;
        private string _filePath = string.Empty;
        private bool _autoRecompute = true;

        public GHSyncStatusComponent()
            : base(
                "C# File Sync",
                "C# Sync",
                "Links one Rhino 8 C# Script component to one external .cs file.",
                "CasaCreta",
                "Utility")
        {
        }

        protected override void RegisterInputParams(
            GH_InputParamManager parameters)
        {
            parameters.AddTextParameter(
                "File", "F", "Path to the linked C# source file.",
                GH_ParamAccess.item, string.Empty);
            parameters.AddBooleanParameter(
                "Enabled", "E", "Watch and update the target component.",
                GH_ParamAccess.item, true);
            parameters.AddBooleanParameter(
                "Auto Recompute", "R",
                "Recompute Grasshopper after applying new source.",
                GH_ParamAccess.item, true);
            parameters[0].Optional = true;
        }

        protected override void RegisterOutputParams(
            GH_OutputParamManager parameters)
        {
            parameters.AddTextParameter(
                "Status", "S", "Current synchronization status.",
                GH_ParamAccess.item);
            parameters.AddTextParameter(
                "Target ID", "ID",
                "Instance GUID of the linked C# Script component.",
                GH_ParamAccess.item);
            parameters.AddTextParameter(
                "Linked File", "Path", "Resolved source-file path.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess access)
        {
            string inputPath = string.Empty;
            bool enabled = true;
            bool autoRecompute = true;

            access.GetData(0, ref inputPath);
            access.GetData(1, ref enabled);
            access.GetData(2, ref autoRecompute);
            _autoRecompute = autoRecompute;

            if (!string.IsNullOrWhiteSpace(inputPath))
                _filePath = inputPath.Trim();

            string status;
            if (!enabled)
            {
                GHSyncManager.Instance.Unregister(InstanceGuid);
                status = "Disabled";
            }
            else if (_targetComponentId == Guid.Empty)
            {
                GHSyncManager.Instance.Unregister(InstanceGuid);
                status = "No target; capture a selected C# Script component";
            }
            else if (string.IsNullOrWhiteSpace(_filePath))
            {
                GHSyncManager.Instance.Unregister(InstanceGuid);
                status = "No source file linked";
            }
            else
            {
                GH_Document document = OnPingDocument();
                string resolvedPath = ResolvePath(document, _filePath);
                bool registrationChanged =
                    GHSyncManager.Instance.Register(
                    document,
                    new GHSyncLink(
                        InstanceGuid,
                        _targetComponentId,
                        resolvedPath,
                        autoRecompute));

                // Re-enabling or changing the linked file creates a fresh
                // watcher. Push once immediately instead of waiting for a
                // second VS Code save event.
                if (registrationChanged && File.Exists(resolvedPath))
                    GHSyncManager.Instance.PushNow(InstanceGuid);

                status = GHSyncManager.Instance.GetStatus(InstanceGuid);
            }

            Message = ShortStatus(status);
            access.SetData(0, status);
            access.SetData(
                1,
                _targetComponentId == Guid.Empty
                    ? string.Empty
                    : _targetComponentId.ToString());
            access.SetData(2, _filePath);
        }

        protected override void AppendAdditionalComponentMenuItems(
            ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendSeparator(menu);
            Menu_AppendItem(
                menu,
                "Capture selected C# Script component",
                CaptureSelectedTarget);
            Menu_AppendItem(
                menu,
                "Link existing .cs file (file → component)",
                LinkExistingFile);
            Menu_AppendItem(
                menu,
                "Export component to new .cs file",
                ExportComponentToFile);
            Menu_AppendSeparator(menu);
            Menu_AppendItem(
                menu,
                "Push file to component now",
                PushNow,
                true,
                CanSynchronize());
            Menu_AppendItem(
                menu,
                "Pull component to file now",
                PullNow,
                true,
                CanSynchronize());
            Menu_AppendItem(
                menu,
                "Unlink",
                Unlink,
                true,
                _targetComponentId != Guid.Empty
                    || !string.IsNullOrWhiteSpace(_filePath));
        }

        private void CaptureSelectedTarget(object sender, EventArgs eventArgs)
        {
            IGH_DocumentObject selected = FindSelectedScriptComponent();
            if (selected == null)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Warning,
                    "Select exactly one modern Rhino 8 C# Script component.");
                ExpireSolution(true);
                return;
            }

            RecordUndoEvent("Capture GHSync target");
            _targetComponentId = selected.InstanceGuid;
            NickName = "Sync: " + selected.NickName;
            ExpireSolution(true);
        }

        private void LinkExistingFile(object sender, EventArgs eventArgs)
        {
            if (_targetComponentId == Guid.Empty)
            {
                CaptureSelectedTarget(sender, eventArgs);
                if (_targetComponentId == Guid.Empty) return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Link C# source file";
                dialog.Filter =
                    "C# source files (*.cs)|*.cs|All files (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog() != DialogResult.OK) return;

                RecordUndoEvent("Link GHSync source file");
                _filePath = dialog.FileName;
            }

            EnsureRegistered();
            GHSyncManager.Instance.PushNow(InstanceGuid);
            ExpireSolution(true);
        }

        private void ExportComponentToFile(
            object sender,
            EventArgs eventArgs)
        {
            if (_targetComponentId == Guid.Empty)
            {
                CaptureSelectedTarget(sender, eventArgs);
                if (_targetComponentId == Guid.Empty) return;
            }

            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Export C# Script component";
                dialog.Filter = "C# source files (*.cs)|*.cs";
                dialog.AddExtension = true;
                dialog.DefaultExt = "cs";
                dialog.OverwritePrompt = true;
                if (dialog.ShowDialog() != DialogResult.OK) return;

                RecordUndoEvent("Export and link GHSync source file");
                _filePath = dialog.FileName;
            }

            EnsureRegistered();
            if (!GHSyncManager.Instance.PullNow(
                InstanceGuid,
                out string error))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error);
            }

            ExpireSolution(true);
        }

        private void PushNow(object sender, EventArgs eventArgs)
        {
            EnsureRegistered();
            GHSyncManager.Instance.PushNow(InstanceGuid);
        }

        private void PullNow(object sender, EventArgs eventArgs)
        {
            DialogResult answer = MessageBox.Show(
                "Replace the linked file with the component's current source?",
                "CasaCreta GHSync",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;

            EnsureRegistered();
            if (!GHSyncManager.Instance.PullNow(
                InstanceGuid,
                out string error))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error);
            }

            ExpireSolution(true);
        }

        private void Unlink(object sender, EventArgs eventArgs)
        {
            RecordUndoEvent("Unlink GHSync source");
            GHSyncManager.Instance.Unregister(InstanceGuid);
            _targetComponentId = Guid.Empty;
            _filePath = string.Empty;
            NickName = "C# Sync";
            ExpireSolution(true);
        }

        private void EnsureRegistered()
        {
            if (!CanSynchronize()) return;

            GH_Document document = OnPingDocument();
            GHSyncManager.Instance.Register(
                document,
                new GHSyncLink(
                    InstanceGuid,
                    _targetComponentId,
                    ResolvePath(document, _filePath),
                    _autoRecompute));
        }

        private IGH_DocumentObject FindSelectedScriptComponent()
        {
            GH_Document document = OnPingDocument();
            if (document == null) return null;

            IGH_DocumentObject[] selected = document.Objects
                .Where(
                    item =>
                        item.InstanceGuid != InstanceGuid
                        && item.Attributes != null
                        && item.Attributes.Selected
                        && GHSyncScriptAdapter.IsEditableScript(item))
                .ToArray();

            return selected.Length == 1 ? selected[0] : null;
        }

        private bool CanSynchronize()
        {
            return _targetComponentId != Guid.Empty
                && !string.IsNullOrWhiteSpace(_filePath);
        }

        private static string ResolvePath(
            GH_Document document,
            string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);

            string documentPath = document?.FilePath;
            if (!string.IsNullOrWhiteSpace(documentPath))
            {
                string directory = Path.GetDirectoryName(documentPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    return Path.GetFullPath(Path.Combine(directory, path));
            }

            return Path.GetFullPath(path);
        }

        private static string ShortStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return "Idle";
            if (status.StartsWith("Updated at")) return status;
            return status.Length <= 24
                ? status
                : status.Substring(0, 21) + "...";
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetGuid("GHSyncTargetComponentId", _targetComponentId);
            writer.SetString("GHSyncFilePath", _filePath ?? string.Empty);
            writer.SetBoolean("GHSyncAutoRecompute", _autoRecompute);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            _targetComponentId =
                reader.ItemExists("GHSyncTargetComponentId")
                    ? reader.GetGuid("GHSyncTargetComponentId")
                    : Guid.Empty;
            _filePath = reader.ItemExists("GHSyncFilePath")
                ? reader.GetString("GHSyncFilePath")
                : string.Empty;
            _autoRecompute = !reader.ItemExists("GHSyncAutoRecompute")
                || reader.GetBoolean("GHSyncAutoRecompute");
            return base.Read(reader);
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            GHSyncManager.Instance.Unregister(InstanceGuid);
            base.RemovedFromDocument(document);
        }

        protected override Bitmap Icon => null;

        public override Guid ComponentGuid =>
            new Guid("7D624ED3-36DD-4E5A-A64A-C046614835D4");
    }
}
