using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class FixUrdfAssimpPlugins : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        FixAssimpPlugins();
    }

    [MenuItem("Tools/Fix URDF Assimp Plugins For Android Build")]
    public static void FixAssimpPlugins()
    {
        string[] guids = AssetDatabase.FindAssets("assimp");

        int fixedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (!path.EndsWith("assimp.dll"))
                continue;

            if (!path.Contains("com.unity.robotics.urdf-importer"))
                continue;

            PluginImporter importer = AssetImporter.GetAtPath(path) as PluginImporter;

            if (importer == null)
            {
                Debug.LogWarning("Found assimp.dll but it is not a PluginImporter: " + path);
                continue;
            }

            Debug.Log("Fixing URDF assimp plugin: " + path);

            importer.SetCompatibleWithAnyPlatform(false);

            // Ovaj DLL je Windows native plugin i ne smije ići u Android build.
            importer.SetCompatibleWithPlatform(BuildTarget.Android, false);

            // Za Unity Editor na Windowsima je dovoljan x86_64.
            if (path.Contains("/win/x86_64/") || path.Contains("\\win\\x86_64\\"))
            {
                importer.SetCompatibleWithEditor(true);
                importer.SetEditorData("CPU", "x86_64");
            }
            else if (path.Contains("/win/x86/") || path.Contains("\\win\\x86\\"))
            {
                importer.SetCompatibleWithEditor(false);
                importer.SetEditorData("CPU", "x86");
            }
            else
            {
                importer.SetCompatibleWithEditor(false);
            }

            importer.SaveAndReimport();
            fixedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("URDF assimp plugin fix finished. Fixed plugins: " + fixedCount);
    }
}