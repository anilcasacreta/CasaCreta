using System;
using Rhino;
using Rhino.Commands;

namespace CasaCreta.EtoForms
{
    /// <summary>
    /// Rhino command that allows typing 'CasaCretaPicker' directly in Rhino's command bar to open the Eto form anytime.
    /// </summary>
    public class GeometryPickerCommand : Command
    {
        public GeometryPickerCommand()
        {
            Instance = this;
        }

        public static GeometryPickerCommand Instance { get; private set; }

        public override string EnglishName => "CasaCretaPicker";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            GeometryPickerForm.ShowForm();
            return Result.Success;
        }
    }
}
