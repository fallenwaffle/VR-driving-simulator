using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Optional editor helper. The experiment already starts automatically in any scene with a vehicle
/// (see ExperimentBootstrap). Use this menu only if you want an [Experiment_Manager] object saved in
/// the SUV and Sports Car scenes so you can edit participant id / fog density / run length in the Inspector.
/// </summary>
public static class SetupExperiment
{
    static readonly string[] ScenePaths = { "Assets/Scenes/SUV.unity", "Assets/Scenes/Sports Car.unity" };

    [MenuItem("Tools/Work Sampling/Add Experiment Manager To SUV + Sports Car Scenes")]
    public static void SetupAllScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        foreach (string path in ScenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var existing = Object.FindAnyObjectByType<ExperimentManager>();
            if (existing == null)
            {
                var go = new GameObject("[Experiment_Manager]");
                go.AddComponent<ExperimentManager>();
                Debug.Log("[SetupExperiment] Added [Experiment_Manager] to " + path);
            }

            // remove the obsolete single sample zone created by the old version of this tool
            var old = GameObject.Find("[Sample_SignalZone]");
            if (old != null) Object.DestroyImmediate(old);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        EditorUtility.DisplayDialog("Work Sampling",
            "Done. Press Play in either scene:\n1 = Clear run, 2 = Fog run, Z = record a zone at the car.", "OK");
    }

    [MenuItem("Tools/Work Sampling/Open Exports Folder")]
    public static void OpenExports()
    {
        string folder = CSVExporter.ExportRoot();
        System.IO.Directory.CreateDirectory(folder);
        EditorUtility.RevealInFinder(folder);
    }

    [MenuItem("Tools/Work Sampling/Open Recorded Zones Folder")]
    public static void OpenZones()
    {
        string folder = System.IO.Path.Combine(Application.streamingAssetsPath, "ExperimentZones");
        System.IO.Directory.CreateDirectory(folder);
        EditorUtility.RevealInFinder(folder);
    }
}
