using System;

namespace CasaCreta.GHSync
{
    internal sealed class GHSyncLink
    {
        public GHSyncLink(
            Guid ownerId,
            Guid targetComponentId,
            string filePath,
            bool autoRecompute)
        {
            OwnerId = ownerId;
            TargetComponentId = targetComponentId;
            FilePath = filePath ?? string.Empty;
            AutoRecompute = autoRecompute;
        }

        public Guid OwnerId { get; }

        public Guid TargetComponentId { get; }

        public string FilePath { get; }

        public bool AutoRecompute { get; }
    }
}
