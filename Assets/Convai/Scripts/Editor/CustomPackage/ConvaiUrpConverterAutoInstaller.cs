#if !CONVAI_URP_CONVERTER_INSTALLED
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Convai.Scripts.Editor.CustomPackage
{
    /// <summary>
/// Ensures the Convai URP Converter is installed for URP projects after the package import.
/// </summary>
    [InitializeOnLoad]
    public static class ConvaiUrpConverterAutoInstaller
    {
        private const string DEFINE_SYMBOL = "CONVAI_URP_CONVERTER_INSTALLED";

        static ConvaiUrpConverterAutoInstaller()
        {
            // Runs once after scripts are loaded (per domain load).
            EditorApplication.delayCall += RunOnceIfUrp;
        }

        private static void RunOnceIfUrp()
        {
            // Extra safety: If someone manually added the symbol mid-session, skip.
            if (HasDefineSymbolAnyTarget())
                return;

            if (!IsUrpProject())
                return;

            try
            {
                var installer = new ConvaiCustomPackageInstaller();
                installer.InstallConvaiURPConverter();

                AddDefineSymbolAllTargets(DEFINE_SYMBOL);

                Debug.Log($"[Convai] URP Converter installed. Added define symbol: {DEFINE_SYMBOL}");
                // Define change will trigger a recompile automatically.
            }
            catch (Exception e)
            {
                Debug.LogError($"[Convai] Failed to install URP Converter. Error: {e}");
            }
        }

        private static bool IsUrpProject()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp == null)
                return false;

            return rp.GetType().Name == "UniversalRenderPipelineAsset";
        }

        private static bool HasDefineSymbolAnyTarget()
        {
            foreach (BuildTarget target in Enum.GetValues(typeof(BuildTarget)))
            {
                BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
                if (group == BuildTargetGroup.Unknown)
                    continue;

                var namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);
                var symbols = PlayerSettings.GetScriptingDefineSymbols(namedTarget)
                    .Split(';')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

                if (symbols.Contains(DEFINE_SYMBOL))
                    return true;
            }

            return false;
        }

        private static void AddDefineSymbolAllTargets(string symbol)
        {
            foreach (BuildTarget target in Enum.GetValues(typeof(BuildTarget)))
            {
                BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
                if (group == BuildTargetGroup.Unknown)
                    continue;

                var namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);

                List<string> symbols = PlayerSettings.GetScriptingDefineSymbols(namedTarget)
                    .Split(';')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

                if (!symbols.Contains(symbol))
                    symbols.Add(symbol);

                PlayerSettings.SetScriptingDefineSymbols(namedTarget, string.Join(";", symbols));
            }
        }
    }
}
#endif