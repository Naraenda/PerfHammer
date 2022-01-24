using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Linq;
using System;
using System.IO;

using PerfHammer;
using PerfHammer.Utils;
using UnityEditor.SceneManagement;

public class PerfHammerWindow : EditorWindow 
{
    static EditorWindow _window;

    GameObject _selectedObject = null;

    // ======= //
    // Modules //
    // ======= //

    OptimizationFlow _flow;
    Atlasser Atlasser => _flow.Get<Atlasser>();
    Decimator Decimator => _flow.Get<Decimator>();
    BlendShapeCleaner BlendShapes => _flow.Get<BlendShapeCleaner>();

    // ======== //
    // UI State //
    // ======== //

    private Vector2 _scrollPosition = Vector2.zero;
    private bool _isObjectSelectionShown = true;
    private bool _isBlendShapesShown     = true;
    private bool _isDecimationShown      = true;

    private string _assetPath = "";
    private string _assetDir  = "";
    private string _assetName = "";
    private string _outputDir = "";

    [MenuItem("Window/Nara/PerfHammer", false, 900000)]
    public static void ShowWindow() {
        if (!_window) {
            _window = GetWindow(typeof(PerfHammerWindow));
            _window.autoRepaintOnSceneChange = true;
        }
        _window.titleContent = new GUIContent("PerfHammer");
        _window.Show();
    }

    void OnGUI() {
        _flow = _flow ?? new OptimizationFlow();
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        var property_preset_names = Atlasser.ShaderPropertyFallback.Keys.ToArray();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label(@"
        _,
      ,/ ]
    ,/  /'
   /  /'
 ,/   \                             By Nara
 |    |__________________,,,,,---------======,
/|    |                  | / / / / / / / / / |
\|    |__________________|/ / / / / / / / / /|
 |____|'                 `````---------======'
 ./  \.  . ._ .  ._ . . ._. .  . .  . ._ .
 |    |  |)|_ |) |_ |-| |_| |\/| |\/| |_ |)
 |____|  | |_ |\ |  | | | | |  | |  | |_ |\
", GUIStyles.AsciiArt);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        // ================ //
        // Object Selection //
        // ================ //

        CustomUI.Section("Object Selection", ref _isObjectSelectionShown, () =>
        {
            var newSelection = (GameObject)EditorGUILayout.ObjectField($"Selected Object", _selectedObject, typeof(GameObject), true);
            var doRediscover = newSelection != _selectedObject;
            if (GUILayout.Button("Select from scene")) {
                newSelection = Selection.activeObject as GameObject;
                _flow = new OptimizationFlow();
                doRediscover = true;
            }

            _selectedObject = newSelection;
            Animator animator = null; 
            if (_selectedObject)
                animator = _selectedObject.GetComponent<Animator>();

            if (animator == null) {
                EditorGUILayout.HelpBox($"Asset without an animator selected. This can result in unexpected behaviour.", MessageType.Warning);
            }

            if (doRediscover && _selectedObject) {
                // Update paths and names
                _assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(_selectedObject);
                if (_assetPath == "") {
                    _assetPath = AssetDatabase.GetAssetPath(animator);
                }
                if (_assetPath == "") {
                    _assetPath = EditorSceneManager.GetActiveScene().path;
                }
                if (_assetPath != "") {
                    _assetDir  = Path.GetDirectoryName(_assetPath);
                    _assetName = _selectedObject.name;
                }

                _outputDir = $@"{_assetDir}\OptimizedModels\{_assetName}\";

                // Update discover materials
                Atlasser.DiscoverMaterials(_selectedObject);
                Atlasser.AutoFillProperty("_MainTex");
                Atlasser.AutoFill();
                _flow.Get<Combiner>().Discover(_selectedObject);
            }

            EditorGUILayout.HelpBox($"Will generate files under {_outputDir}", MessageType.Info);
        });

        // ========== //
        // Mesh Combiner //
        // ========== //

        _flow.Get<Combiner>().OnGUI(_selectedObject);

        // ============== //
        // Material Input //
        // ============== //

        _flow.Get<Atlasser>().OnGUI(_selectedObject);

        // ========= //
        // ShapeKeys //
        // ========= //

        CustomUI.Section("Blend Shapes", ref _isBlendShapesShown, () => {
            EditorGUILayout.HelpBox("Blendshape cleaning is currently disabled.", MessageType.Warning);

            EditorGUI.BeginDisabledGroup(true);
            BlendShapes.ApplyNonZero   = GUILayout.Toggle(BlendShapes.ApplyNonZero, "Apply non-zero blend shapes to mesh");
            BlendShapes.RemoveNonAscii = GUILayout.Toggle(BlendShapes.RemoveNonAscii, "Remove non-ascii blendshapes");
            BlendShapes.KeepVRCShapes  = GUILayout.Toggle(BlendShapes.KeepVRCShapes, "Keep shapes containing \"vrc\"");
            EditorGUI.EndDisabledGroup();
        });

        // ========== //
        // Decimation //
        // ========== //

        CustomUI.Section("Decimation", ref _isDecimationShown, () => {

            EditorGUILayout.HelpBox("Decimation is currently untested.", MessageType.Warning);

            Decimator.Mode = (Decimator.DecimationMode)
                EditorGUILayout.EnumPopup("Decimation", Decimator.Mode);

            EditorGUI.BeginDisabledGroup(Decimator.Mode != Decimator.DecimationMode.Lossy);
            
            Decimator.Quality = EditorGUILayout.Slider("Quality", Decimator.Quality, 0, 1);
            if (GUILayout.Toggle(Decimator.TargetTris > 0, "Use tris target instead of quality.")) {
                if (Decimator.TargetTris < 1)
                    Decimator.TargetTris = 65000;
            } else {
                Decimator.TargetTris = -1;
            }
            Decimator.TargetTris = EditorGUILayout.IntField("Target Triangles", Decimator.TargetTris);

            EditorGUI.EndDisabledGroup();
        });

        GUILayout.Space(20);
        EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
        GUILayout.Space(20);

        // ======== //
        // Optimize //
        // ======== //

        EditorGUI.BeginDisabledGroup(_selectedObject == null);
        if (GUILayout.Button("Optimize")) {
            var exporter = new Exporter(_outputDir, _assetName);
            _flow.Optimize(_selectedObject, exporter);
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndScrollView();
    }
}