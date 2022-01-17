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
    private bool _isMaterialInputShown   = true;
    private bool _isMaterialOutputShown  = true;
    private bool _isObjectSelectionShown = true;
    private bool _isBlendShapesShown     = true;
    private bool _isDecimationShown      = true;
    private bool _isMeshCombinerShown    = true;

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
 ./  \. . ._ .  ._ . . ._. .  . .  . . . ._ .
 |    | |)|_ |) |_ |-| |_| |\/| |\/| |_| |_ |)
 |____| | |_ |\ |  | | | | |  | |  | | | |_ |\
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

        CustomUI.Section("Material Input", ref _isMaterialInputShown, () => {
            if (GUILayout.Button("Refresh materials")) {
                Atlasser.DiscoverMaterials(_selectedObject);
            }
            GUILayout.Space(5);


            // Display UI for each Atlas
            EditorGUI.indentLevel++;
            Stack<int> removedAtlasIndices = new Stack<int>();
            for (int a_i = 0; a_i < Atlasser.Atlasses.Count; a_i++) {
                var a = Atlasser.Atlasses[a_i];
                if (a_i > 0) {
                    GUILayout.Space(5);
                    EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
                    GUILayout.Space(5);
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();

                a.PropertyName = EditorGUILayout.TextField(a.PropertyName);

                var a_name_int = EditorGUILayout.Popup(Array.IndexOf(property_preset_names, a.PropertyName), property_preset_names);
                if (a_name_int >= 0)
                    a.PropertyName = property_preset_names[a_name_int];

                if (a_i > 0)
                    if (GUILayout.Button("Remove"))
                        removedAtlasIndices.Push(a_i);

                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical();
                for (int m_i = 0; m_i < a.Mapping.Count; m_i++) {
                    var m_kvp = a.Mapping.ElementAt(m_i);
                    var m_mat = m_kvp.Key;
                    var m_tex = m_kvp.Value;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField(m_mat, typeof(Material), false);
                    EditorGUI.EndDisabledGroup();
                    a.Mapping[m_mat] = (Texture2D)EditorGUILayout.ObjectField(m_tex, typeof(Texture2D), false);
                    EditorGUILayout.EndHorizontal();
                }

                // Update
                Atlasser.Atlasses[a_i] = a;

                if (GUILayout.Button("Auto-fill")) {
                    Atlasser.AutoFillProperty(a.PropertyName);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                if (a_i == 0) {
                    EditorGUILayout.HelpBox("This is your main texture atlas (diffuse).", MessageType.Info);
                    GUILayout.Space(5);
                }

            }
            foreach (var index in removedAtlasIndices) {
                Atlasser.Atlasses.RemoveAt(index);
            }

            EditorGUI.indentLevel--;
            if (GUILayout.Button("Add atlas")) {
                Atlasser.AddAtlas();
            }
        });

        // =============== //
        // Material Output //
        // =============== //

        CustomUI.Section("Material Output", ref _isMaterialOutputShown, () => {

            // Display UI for each output group
            EditorGUI.indentLevel++;
            for (int g_i = 0; g_i < Atlasser.Groups.Count; g_i++) {
                var g = Atlasser.Groups[g_i];
                if (g_i > 0) {
                    GUILayout.Space(5);
                    EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
                    GUILayout.Space(5);
                }

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical(); 
                float labelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 75;

                g.Name = EditorGUILayout.TextField(g.Name);
                g.ReferenceMaterial = (Material)EditorGUILayout.ObjectField("Material", g.ReferenceMaterial, typeof(Material), false);

                if (g.ReferenceMaterial != null) {
                    g.ReferenceShader = MaterialUtils.GetCommonShader(new Material[] { g.ReferenceMaterial });
                }

                EditorGUI.BeginDisabledGroup(g.ReferenceMaterial != null);
                g.ReferenceShader = (Shader)EditorGUILayout.ObjectField("Shader", g.ReferenceShader, typeof(Shader), false);
                EditorGUI.EndDisabledGroup();

                EditorGUIUtility.labelWidth = labelWidth;
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical();

                for (int g_m_i = 0; g_m_i < g.Materials.Count; g_m_i++) {
                    var material = g.Materials.ElementAt(g_m_i);
                    EditorGUILayout.BeginHorizontal();

                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField(material, typeof(Material), false);
                    EditorGUI.EndDisabledGroup();


                    EditorGUI.BeginDisabledGroup(g_i == 0);
                    if (GUILayout.Button("Up")) {
                        Atlasser.MoveMaterial(material, g_i - 1);
                    }
                    EditorGUI.EndDisabledGroup();

                    if (GUILayout.Button("Down")) {
                        Atlasser.MoveMaterial(material, g_i + 1);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        });

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