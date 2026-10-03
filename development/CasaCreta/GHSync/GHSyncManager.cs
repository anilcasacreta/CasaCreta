using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

using Grasshopper.Kernel;

using Rhino;

namespace CasaCreta.GHSync
{
    internal sealed class GHSyncManager
    {
        private sealed class Registration : IDisposable
        {
            public Registration(
                GHSyncLink link,
                GH_Document document)
            {
                Link = link;
                Document = document;
            }

            public GHSyncLink Link { get; }
            public GH_Document Document { get; }
            public GHSyncFileWatcher Watcher { get; set; }
            public string LastHash { get; set; } = string.Empty;
            public string Status { get; set; } = "Connected";

            public void Dispose()
            {
                Watcher?.Dispose();
                Watcher = null;
            }
        }

        private readonly object _gate = new object();
        private readonly Dictionary<Guid, Registration> _registrations =
            new Dictionary<Guid, Registration>();

        private GHSyncManager()
        {
        }

        public static GHSyncManager Instance { get; } =
            new GHSyncManager();

        public bool Register(
            GH_Document document,
            GHSyncLink link)
        {
            if (document == null || link == null) return false;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(link.FilePath);
            }
            catch
            {
                SetDetachedStatus(
                    link.OwnerId,
                    "Invalid source-file path");
                return false;
            }

            GHSyncLink normalizedLink = new GHSyncLink(
                link.OwnerId,
                link.TargetComponentId,
                fullPath,
                link.AutoRecompute);

            lock (_gate)
            {
                if (_registrations.TryGetValue(
                    link.OwnerId,
                    out Registration existing))
                {
                    bool unchanged =
                        existing.Document == document
                        && existing.Link.TargetComponentId
                            == normalizedLink.TargetComponentId
                        && string.Equals(
                            existing.Link.FilePath,
                            normalizedLink.FilePath,
                            StringComparison.OrdinalIgnoreCase)
                        && existing.Link.AutoRecompute
                            == normalizedLink.AutoRecompute;

                    if (unchanged) return false;

                    existing.Dispose();
                    _registrations.Remove(link.OwnerId);
                }

                Registration registration =
                    new Registration(normalizedLink, document);

                try
                {
                    registration.Watcher = new GHSyncFileWatcher(
                        fullPath,
                        () => QueuePush(link.OwnerId, false));

                    registration.Status = File.Exists(fullPath)
                        ? "Connected"
                        : "Waiting for source file";
                }
                catch (Exception exception)
                {
                    registration.Status =
                        "Watcher error: " + exception.Message;
                }

                _registrations[link.OwnerId] = registration;
            }

            return true;
        }

        public void Unregister(Guid ownerId)
        {
            lock (_gate)
            {
                if (!_registrations.TryGetValue(
                    ownerId,
                    out Registration registration))
                {
                    return;
                }

                registration.Dispose();
                _registrations.Remove(ownerId);
            }
        }

        public string GetStatus(Guid ownerId)
        {
            lock (_gate)
            {
                return _registrations.TryGetValue(
                    ownerId,
                    out Registration registration)
                    ? registration.Status
                    : "Disabled";
            }
        }

        public void PushNow(Guid ownerId)
        {
            QueuePush(ownerId, true);
        }

        public bool PullNow(
            Guid ownerId,
            out string error)
        {
            error = string.Empty;
            Registration registration = GetRegistration(ownerId);

            if (registration == null)
            {
                error = "This link is not active.";
                return false;
            }

            IGH_DocumentObject target = registration.Document.FindObject(
                registration.Link.TargetComponentId,
                true);

            if (!GHSyncScriptAdapter.TryGetSource(
                target,
                out string source,
                out error))
            {
                SetStatus(ownerId, registration, error);
                return false;
            }

            try
            {
                string directory =
                    Path.GetDirectoryName(registration.Link.FilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(
                    registration.Link.FilePath,
                    source,
                    new UTF8Encoding(false));

                registration.LastHash = ComputeHash(source);
                SetStatus(
                    ownerId,
                    registration,
                    "Pulled component source to file");
                return true;
            }
            catch (Exception exception)
            {
                error = "Could not write source file: "
                    + exception.Message;
                SetStatus(ownerId, registration, error);
                return false;
            }
        }

        private void QueuePush(
            Guid ownerId,
            bool force)
        {
            ThreadPool.QueueUserWorkItem(
                _ => ReadAndApply(ownerId, force));
        }

        private void ReadAndApply(
            Guid ownerId,
            bool force)
        {
            Registration registration = GetRegistration(ownerId);
            if (registration == null) return;

            if (!TryReadSource(
                registration.Link.FilePath,
                out string source,
                out string error))
            {
                SetStatus(ownerId, registration, error);
                ExpireOwner(registration);
                return;
            }

            if (source.Length == 0)
            {
                SetStatus(
                    ownerId,
                    registration,
                    "Empty source file; update skipped");
                ExpireOwner(registration);
                return;
            }

            string hash = ComputeHash(source);
            if (!force
                && string.Equals(
                    hash,
                    registration.LastHash,
                    StringComparison.Ordinal))
            {
                return;
            }

            RhinoApp.InvokeOnUiThread(
                (Action)(() => ApplyOnUiThread(
                    ownerId,
                    registration,
                    source,
                    hash)));
        }

        private void ApplyOnUiThread(
            Guid ownerId,
            Registration expectedRegistration,
            string source,
            string hash)
        {
            Registration registration = GetRegistration(ownerId);
            if (!ReferenceEquals(registration, expectedRegistration)) return;

            IGH_DocumentObject target = registration.Document.FindObject(
                registration.Link.TargetComponentId,
                true);

            if (!GHSyncScriptAdapter.TrySetSource(
                target,
                source,
                registration.Link.AutoRecompute,
                out string error))
            {
                SetStatus(ownerId, registration, error);
                ExpireOwner(registration);
                return;
            }

            registration.LastHash = hash;
            SetStatus(
                ownerId,
                registration,
                registration.Link.AutoRecompute
                    ? "Updated at " + DateTime.Now.ToString("HH:mm:ss")
                    : "Updated; recompute pending");

            if (!registration.Link.AutoRecompute
                && target is IGH_ActiveObject activeTarget)
            {
                activeTarget.ExpireSolution(false);
            }

            ExpireOwner(registration);
        }

        private void ExpireOwner(Registration registration)
        {
            if (!IsCurrent(registration)) return;

            Action expire = () =>
            {
                if (!IsCurrent(registration)) return;

                IGH_DocumentObject owner =
                    registration.Document.FindObject(
                        registration.Link.OwnerId,
                        true);

                if (owner is IGH_ActiveObject activeOwner)
                {
                    activeOwner.ExpireSolution(false);
                }
            };

            if (RhinoApp.InvokeRequired)
            {
                RhinoApp.InvokeOnUiThread(expire);
            }
            else
            {
                expire();
            }
        }

        private Registration GetRegistration(Guid ownerId)
        {
            lock (_gate)
            {
                _registrations.TryGetValue(
                    ownerId,
                    out Registration registration);
                return registration;
            }
        }

        private void SetStatus(
            Guid ownerId,
            Registration expectedRegistration,
            string status)
        {
            lock (_gate)
            {
                if (_registrations.TryGetValue(
                    ownerId,
                    out Registration registration)
                    && ReferenceEquals(
                        registration,
                        expectedRegistration))
                {
                    registration.Status = status;
                }
            }
        }

        private void SetDetachedStatus(
            Guid ownerId,
            string status)
        {
            lock (_gate)
            {
                if (_registrations.TryGetValue(
                    ownerId,
                    out Registration registration))
                {
                    registration.Status = status;
                }
            }
        }

        private bool IsCurrent(Registration expectedRegistration)
        {
            lock (_gate)
            {
                if (_registrations.TryGetValue(
                    expectedRegistration.Link.OwnerId,
                    out Registration registration))
                    return ReferenceEquals(
                        registration,
                        expectedRegistration);

                return false;
            }
        }

        private static bool TryReadSource(
            string path,
            out string source,
            out string error)
        {
            source = string.Empty;
            error = string.Empty;

            for (int attempt = 0;
                attempt < GHSyncSettings.FileReadRetryCount;
                attempt++)
            {
                try
                {
                    if (!File.Exists(path))
                    {
                        error = "Source file not found";
                        return false;
                    }

                    source = File.ReadAllText(path);
                    return true;
                }
                catch (IOException exception)
                {
                    error = "Source file is busy: " + exception.Message;
                    Thread.Sleep(
                        GHSyncSettings.FileReadRetryDelayMilliseconds);
                }
                catch (UnauthorizedAccessException exception)
                {
                    error = "Source file cannot be read: "
                        + exception.Message;
                    return false;
                }
            }

            return false;
        }

        private static string ComputeHash(string source)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(
                    source ?? string.Empty);
                byte[] hash = algorithm.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }
    }
}
