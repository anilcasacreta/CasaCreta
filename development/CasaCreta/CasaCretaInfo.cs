using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace CasaCreta
{
    public class CasaCretaInfo : GH_AssemblyInfo
    {
        public override string Name => "CasaCreta";
        public override Bitmap Icon => null;
        public override string Description => "Curve ordering for G-code generation";
        public override Guid Id => new Guid("ea1216fd-2971-4bbb-856e-5f1d92f991e4");
        public override string AuthorName => "";
        public override string AuthorContact => "";
        public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
    }
}
