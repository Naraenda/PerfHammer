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

public class PerfHammerWindow : EditorWindow 
{
    static EditorWindow _window;

    GameObject _selectedObject;

    Atlasser.Configuration _config = new Atlasser.Configuration();

    Vector2 _scrollPosition = Vector2.zero;
    bool _isMaterialInputShown = true;
    bool _isMaterialOutputShown = true;

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
        var property_preset_names = Atlasser.ShaderPropertyFallback.Keys.ToArray();

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        _selectedObject = (GameObject)EditorGUILayout.ObjectField( $"Selected Object", _selectedObject, typeof(GameObject), true);
        
        if (GUILayout.Button("Select from scene")) {
            _selectedObject = Selection.activeObject as GameObject;
            _config = new Atlasser.Configuration();
            if (_selectedObject) {
                _config.DiscoverMaterials(_selectedObject);
                _config.AutoFillProperty("_MainTex");
                _config.AutoFill();
            }
        }

        string assetPath = "Assets";
        string assetDir  = "Assets";
        string assetName = "UnnamedAsset";
        if (_selectedObject) {
            assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(_selectedObject);
            if (assetPath == "") {
                assetPath = "Assets";
            } else {
                assetDir  = Path.GetDirectoryName(assetPath);
                assetName = _selectedObject.name;
            }
        }
        string outputDir = $"{assetDir}/OptimizedModels/{assetName}/";
        bool isFbxFile = Path.GetExtension(assetPath) == ".fbx";

        if (!isFbxFile) {
            EditorGUILayout.HelpBox($"No FBX file selected!", MessageType.Error);
        } else {
            EditorGUILayout.HelpBox($"Will generate files under {outputDir}_*", MessageType.Info);
        }

        
        GUILayout.Space(5);
        EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
        GUILayout.Space(5);

        // ============== //
        // Material Input //
        // ============== //

        CustomUI.Section("Material Input", ref _isMaterialInputShown, () => {
            if (GUILayout.Button("Refresh materials")) {
                _config.DiscoverMaterials(_selectedObject);
            }
            GUILayout.Space(5);

            EditorGUI.indentLevel++;
            Stack<int> removedAtlasIndices = new Stack<int>();
            for (int a_i = 0; a_i < _config.Atlasses.Count; a_i++) {
                var a = _config.Atlasses[a_i];
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
                _config.Atlasses[a_i] = a;

                if (GUILayout.Button("Auto-fill")) {
                    _config.AutoFillProperty(a.PropertyName);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                if (a_i == 0) {
                    EditorGUILayout.HelpBox("This is your main texture atlas (diffuse).", MessageType.Info);
                    GUILayout.Space(5);
                }

            }
            foreach (var index in removedAtlasIndices) {
                _config.Atlasses.RemoveAt(index);
            }

            EditorGUI.indentLevel--;
            if (GUILayout.Button("Add atlas")) {
                _config.AddAtlas();
            }
        });

        // =============== //
        // Material Output //
        // =============== //

        CustomUI.Section("Material Output", ref _isMaterialOutputShown, () => {
            EditorGUI.indentLevel++;
            for (int g_i = 0; g_i < _config.Groups.Count; g_i++) {
                var g = _config.Groups[g_i];
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
                        _config.MoveMaterial(material, g_i - 1);
                    }
                    EditorGUI.EndDisabledGroup();

                    if (GUILayout.Button("Down")) {
                        _config.MoveMaterial(material, g_i + 1);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        });

        GUILayout.Space(5);
        EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), Color.gray);
        GUILayout.Space(5);

        EditorGUI.BeginDisabledGroup(!isFbxFile);
        if (GUILayout.Button("Optimize")) {
            if (_selectedObject != null) {
                var exporter = new Exporter(outputDir, assetName);

                var copy = Duplicator.Duplicate(_selectedObject, $"{_selectedObject.name} (Optimized)");
                Atlasser.Optimize(copy, _config, exporter);
                exporter.ExportModel(copy);
                
            }
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndScrollView();
    }
}