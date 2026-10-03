using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

using Grasshopper.Kernel;

namespace CasaCreta.GHSync
{
    internal static class GHSyncScriptAdapter
    {
        private const string ScriptInterfaceName =
            "RhinoCodePlatform.GH.IScriptComponent";
        private const string ScriptObjectInterfaceName =
            "RhinoCodePlatform.GH.Context.IScriptObject";

        private static readonly ConcurrentDictionary<Type, ScriptBinding>
            Bindings = new ConcurrentDictionary<Type, ScriptBinding>();

        private sealed class ScriptBinding
        {
            public PropertyInfo TextProperty { get; set; }
            public MethodInfo SetSourceMethod { get; set; }
            public MethodInfo ApplyScriptMethod { get; set; }
            public FieldInfo ContextField { get; set; }
            public PropertyInfo EnforceParamsProperty { get; set; }
            public MethodInfo ExpireMethod { get; set; }
            public MethodInfo RebuildMethod { get; set; }
            public MethodInfo RecomputeMethod { get; set; }
            public string Error { get; set; }

            public bool IsValid => string.IsNullOrEmpty(Error);
        }

        public static bool IsEditableScript(
            IGH_DocumentObject documentObject)
        {
            return TryGetBinding(documentObject, out _, out _);
        }

        public static bool TryGetSource(
            IGH_DocumentObject documentObject,
            out string source,
            out string error)
        {
            source = string.Empty;

            if (!TryGetBinding(
                documentObject,
                out ScriptBinding binding,
                out error))
            {
                return false;
            }

            try
            {
                source = binding.TextProperty.GetValue(documentObject) as string
                    ?? string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "Could not read script source: "
                    + GetBaseMessage(exception);
                return false;
            }
        }

        public static bool TrySetSource(
            IGH_DocumentObject documentObject,
            string source,
            bool autoRecompute,
            out string error)
        {
            if (!TryGetBinding(
                documentObject,
                out ScriptBinding binding,
                out error))
            {
                return false;
            }

            try
            {
                string safeSource = EnsureCSharpDirective(source);
                object scriptContext =
                    binding.ContextField?.GetValue(documentObject);
                PropertyInfo enforceParamsProperty =
                    binding.EnforceParamsProperty
                    ?? scriptContext?.GetType().GetProperty(
                        "EnforceParamsOnCreate",
                        BindingFlags.Instance | BindingFlags.Public);

                object previousEnforceParams =
                    enforceParamsProperty?.CanRead == true
                        ? enforceParamsProperty.GetValue(scriptContext)
                        : null;

                try
                {
                    // This is the same sequence used by GHCodeSync. Without
                    // disabling this context flag, Rhino stores the new text
                    // but does not complete the editor's Apply transaction.
                    if (enforceParamsProperty?.CanWrite == true)
                    {
                        enforceParamsProperty.SetValue(
                            scriptContext,
                            false);
                    }

                    if (binding.SetSourceMethod != null)
                    {
                        binding.SetSourceMethod.Invoke(
                            documentObject,
                            new object[] { safeSource });
                    }
                    else
                    {
                        binding.TextProperty.SetValue(
                            documentObject,
                            safeSource);
                    }

                    binding.ApplyScriptMethod.Invoke(documentObject, null);
                    documentObject.Attributes?.ExpireLayout();

                    // Expire and ReBuild update Rhino's compiled script.
                    // ReCompute is intentionally conditional so a disabled
                    // Auto Recompute input leaves the target expired until
                    // the user starts the next Grasshopper solution.
                    InvokeScriptLifecycle(
                        documentObject,
                        binding,
                        autoRecompute);
                }
                finally
                {
                    if (previousEnforceParams != null
                        && enforceParamsProperty?.CanWrite == true)
                    {
                        enforceParamsProperty.SetValue(
                            scriptContext,
                            previousEnforceParams);
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                error = "Could not replace script source: "
                    + GetBaseMessage(exception);
                return false;
            }
        }

        private static bool TryGetBinding(
            IGH_DocumentObject documentObject,
            out ScriptBinding binding,
            out string error)
        {
            binding = null;
            error = string.Empty;

            if (documentObject == null)
            {
                error = "The target component no longer exists.";
                return false;
            }

            binding = Bindings.GetOrAdd(
                documentObject.GetType(),
                CreateBinding);

            if (!binding.IsValid)
            {
                error = binding.Error;
                return false;
            }

            return true;
        }

        private static ScriptBinding CreateBinding(Type componentType)
        {
            Type scriptInterface = null;
            Type scriptObjectInterface = null;

            foreach (Type interfaceType in componentType.GetInterfaces())
            {
                if (interfaceType.FullName == ScriptInterfaceName)
                    scriptInterface = interfaceType;
                else if (interfaceType.FullName == ScriptObjectInterfaceName)
                    scriptObjectInterface = interfaceType;
            }

            if (scriptInterface == null)
            {
                return new ScriptBinding
                {
                    Error =
                        "The target is not a modern Rhino 8 Script component."
                };
            }

            PropertyInfo textProperty = scriptInterface.GetProperty("Text");
            if (textProperty == null
                || !textProperty.CanRead
                || !textProperty.CanWrite)
            {
                return new ScriptBinding
                {
                    Error =
                        "Rhino's script source API is not writable in this version."
                };
            }

            MethodInfo applyScriptMethod = componentType.GetMethod(
                "SetParametersFromScript",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);

            if (applyScriptMethod == null || scriptObjectInterface == null)
            {
                return new ScriptBinding
                {
                    Error =
                        "Rhino's script rebuild API is unavailable in this version."
                };
            }

            MethodInfo expireMethod = scriptObjectInterface.GetMethod(
                "Expire",
                Type.EmptyTypes);
            MethodInfo rebuildMethod = scriptObjectInterface.GetMethod(
                "ReBuild",
                Type.EmptyTypes);
            MethodInfo recomputeMethod = scriptObjectInterface.GetMethod(
                "ReCompute",
                Type.EmptyTypes);

            if (expireMethod == null
                || rebuildMethod == null
                || recomputeMethod == null)
            {
                return new ScriptBinding
                {
                    Error =
                        "Rhino's script lifecycle API is incomplete in this version."
                };
            }

            FieldInfo contextField = FindField(componentType, "Context");

            return new ScriptBinding
            {
                TextProperty = textProperty,
                SetSourceMethod = componentType.GetMethod(
                    "SetSource",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(string) },
                    null),
                ApplyScriptMethod = applyScriptMethod,
                ContextField = contextField,
                EnforceParamsProperty = contextField?.FieldType.GetProperty(
                    "EnforceParamsOnCreate",
                    BindingFlags.Instance | BindingFlags.Public),
                ExpireMethod = expireMethod,
                RebuildMethod = rebuildMethod,
                RecomputeMethod = recomputeMethod,
                Error = string.Empty
            };
        }

        private static void InvokeScriptLifecycle(
            IGH_DocumentObject documentObject,
            ScriptBinding binding,
            bool autoRecompute)
        {
            InvokeInterfaceMethod(binding.ExpireMethod, documentObject);
            InvokeInterfaceMethod(binding.RebuildMethod, documentObject);

            if (autoRecompute)
                InvokeInterfaceMethod(
                    binding.RecomputeMethod,
                    documentObject);
        }

        private static void InvokeInterfaceMethod(
            MethodInfo method,
            object target)
        {
            object result = method.Invoke(target, null);
            if (result is Task task)
            {
                task.GetAwaiter().GetResult();
            }
        }

        private static FieldInfo FindField(
            Type componentType,
            string fieldName)
        {
            Type currentType = componentType;
            while (currentType != null)
            {
                FieldInfo contextField = currentType.GetField(
                    fieldName,
                    BindingFlags.Instance
                    | BindingFlags.NonPublic
                    | BindingFlags.Public
                    | BindingFlags.DeclaredOnly);

                if (contextField != null)
                    return contextField;

                currentType = currentType.BaseType;
            }

            return null;
        }

        private static string EnsureCSharpDirective(string source)
        {
            string value = source ?? string.Empty;
            string trimmed = value.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');

            if (trimmed.StartsWith("// #! csharp",
                StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("//#! csharp",
                    StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            return "// #! csharp" + Environment.NewLine + value;
        }

        private static string GetBaseMessage(Exception exception)
        {
            Exception current = exception;
            while (current.InnerException != null)
            {
                current = current.InnerException;
            }

            return current.Message;
        }
    }
}
