using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using UnityEngine;
using UnityEditor;
using PerfHammer.Utils;
using System;
using UnityEditor.Presets;

namespace PerfHammer
{
    public class Atlasser : IModule
    {
        public string Name => "Atlasser";

        public Dictionary<string, Texture2D> ShaderPropertyFallback = new Dictionary<string, Texture2D>() {
            // Texture, default color
            { "_MainTex"         , Texture2D.whiteTexture },
            { "_BumpMap"         , Texture2D.normalTexture },
            { "_MetallicGlossMap", Texture2D.blackTexture },
            { "_OcclusionMap"    , Texture2D.whiteTexture },
            { "_EmissionMap"     , Texture2D.blackTexture },
        };

        public class MaterialGroup
        {
            public HashSet<Material> Materials = new HashSet<Material>();
            public string Name = "Atlas";
            public Material ReferenceMaterial;
            public Shader   ReferenceShader;
        }

        public class Map
        {
            public Texture2D Texture = null;
            public Color     Color   = Color.white;
        }

        /// <summary>
        /// Descriptor of a texture atlas
        /// </summary>
        public class Atlas
        {
            public Atlas(string name = "") {
                PropertyName = name;
            }

            public string PropertyName = "";
            public Color  DefaultColor = Color.black;
            public int    UVChannel    = 0;
            public Dictionary<Material, Map> Mapping = new Dictionary<Material, Map>();
        }

        public List<MaterialGroup> Groups = new List<MaterialGroup>() { new MaterialGroup() };

        /// <summary>
        /// Texture atlas sets by name. For example: _MainTex.
        /// </summary>
        public List<Atlas> Atlasses = new List<Atlas>() {
            new Atlas("_MainTex")
        };

        public GameObject Run(Exporter e, GameObject obj, GameObject reference) {
            var rs = obj.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var x in rs) {
                MakeAtlas(x.sharedMesh, x, e);
            }
            return obj;
        }

        bool _isMaterialInputShown  = true;
        bool _isMaterialOutputShown = true;

        public void OnGUI(GameObject _selectedObject) {
            string[] property_preset_names = ShaderPropertyFallback.Keys.ToArray();

            CustomUI.Section("Material Input", ref _isMaterialInputShown, () => {
                if (GUILayout.Button("Refresh materials")) {
                    DiscoverMaterials(_selectedObject);
                }
                GUILayout.Space(5);


                // Display UI for each Atlas
                EditorGUI.indentLevel++;
                Stack<int> removedAtlasIndices = new Stack<int>();
                for (int a_i = 0; a_i < Atlasses.Count; a_i++) {
                    var a = Atlasses[a_i];
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
                        var m_map = m_kvp.Value;

                        EditorGUILayout.BeginHorizontal();
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.ObjectField(m_mat, typeof(Material), false);
                        EditorGUI.EndDisabledGroup();
                        m_map.Texture = EditorGUILayout.ObjectField(m_map.Texture, typeof(Texture2D), false) as Texture2D;
                        m_map.Color   = EditorGUILayout.ColorField(m_map.Color, GUILayout.Width(52));
                        EditorGUILayout.EndHorizontal();
                    }

                    // Update
                    Atlasses[a_i] = a;

                    if (GUILayout.Button("Auto-fill")) {
                        AutoFillProperty(a.PropertyName);
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                    if (a_i == 0) {
                        EditorGUILayout.HelpBox("This is your main texture atlas (diffuse).", MessageType.Info);
                        GUILayout.Space(5);
                    }

                }
                foreach (var index in removedAtlasIndices) {
                    Atlasses.RemoveAt(index);
                }

                EditorGUI.indentLevel--;
                if (GUILayout.Button("Add atlas")) {
                    AddAtlas();
                }
            });

            CustomUI.Section("Material Output", ref _isMaterialOutputShown, () => {

                // Display UI for each output group
                EditorGUI.indentLevel++;
                for (int g_i = 0; g_i < Groups.Count; g_i++) {
                    var g = Groups[g_i];
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
                            MoveMaterial(material, g_i - 1);
                        }
                        EditorGUI.EndDisabledGroup();

                        if (GUILayout.Button("Down")) {
                            MoveMaterial(material, g_i + 1);
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            });
        }

        public void DiscoverMaterials(GameObject go) {
            var detected = new HashSet<Material>(
                go.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .SelectMany(r => r.sharedMaterials));

            detected.UnionWith(
                go.GetComponentsInChildren<MeshRenderer>()
                    .SelectMany(r => r.sharedMaterials));

            var contained = new HashSet<Material>(GetAllMaterials());

            foreach (var removed in contained.Except(detected)) {
                foreach (var group in Groups) {
                    group.Materials.Remove(removed);
                }
            }

            foreach (var added in detected.Except(contained)) {
                foreach (var group in Groups) {
                    group.Materials.Add(added);
                }
            }

            var materials = GetAllMaterials().ToList();
            Debug.Log($"Discovered {GetAllMaterials().Count()} materials.");

            // Add discovered materials
            foreach (var atlas in Atlasses) {
                atlas.Mapping = atlas.Mapping.Where(kvp => materials.Contains(kvp.Key)).ToDictionary(x => x.Key, x => x.Value);

                foreach (var m in materials)
                    if (!atlas.Mapping.ContainsKey(m))
                        atlas.Mapping.Add(m, new Map());
            }

            foreach (var group in Groups) {
                group.ReferenceShader = group.ReferenceShader != null ? 
                    group.ReferenceShader : 
                    MaterialUtils.GetCommonShader(group.Materials);
            }
        }

        public void AutoFill() {
            var properties = MaterialUtils.GetCommonTextureProperties(GetAllMaterials());
            foreach (var p in properties.Where(p => !Atlasses.Select(a => a.PropertyName).Contains(p))) {
                var a = AddAtlas(p);
                AutoFillProperty(p);
                if (a.Mapping.Select(m => m.Value).All(m => m.Texture == null && m.Color == Color.white))
                    Atlasses.Remove(a);
            }
        }

        public void AutoFillProperty(string name) {
            var mats = GetAtlas(name);
            try {
                for (int m_i = 0; m_i < mats.Count; m_i++) {
                    var kvp = mats.ElementAt(m_i);
                    var mat = kvp.Key;
                    var tex = kvp.Value.Texture;
                    if (mat.HasProperty(name))
                        tex = mat.GetTexture(name) as Texture2D;
                    mats[mat].Texture = tex;
                    mats[mat].Color = Color.white;
                }
            } catch(Exception e) {
                Debug.LogWarning($"Error while filling in property for atlas {name}:\n" + e);
            }
        }

        public Dictionary<Material, Map> GetAtlas(string name)
            => Atlasses.Find(x => x.PropertyName == name)?.Mapping;

        public Atlas AddAtlas(string name = "") {
            var atlas = new Atlas() {
                PropertyName = name,
                Mapping = GetAllMaterials().ToDictionary(k => k, v => new Map())
            };

            Atlasses.Add(atlas);
            return atlas;
        }

        public Texture2D GetTex(string name, Material mat)
            => GetAtlas(name)[mat].Texture;

        public Texture2D GetMainTex(Material mat)
            => Atlasses.First().Mapping[mat].Texture;

        public IEnumerable<Material> GetAllMaterials()
            => Groups.SelectMany(g => g.Materials);

        public int FindGroup(Material m)
            => Groups.FindIndex(g => g.Materials.Contains(m));

        public MaterialGroup GroupOf(Material m)
            => Groups[FindGroup(m)];

        public void MoveMaterial(Material m, int newGroup) {
            int oldGroup = FindGroup(m);

            if (oldGroup < 0)
                throw new IndexOutOfRangeException();

            Groups[oldGroup].Materials.Remove(m);

            if (newGroup < Groups.Count) {
                Groups[newGroup].Materials.Add(m);
            } else {
                Groups.Add(new MaterialGroup() {
                    Name = $"Atlas {newGroup}",
                    Materials = new HashSet<Material>() { m }
                });
            }


            if (Groups[oldGroup].Materials.Count <= 0)
                Groups.RemoveAt(oldGroup);
        }

        public static int DEFAULT_SIZE = 32;
        public static int PADDING = 0;

        /// <summary>
        /// Merges same materials
        /// </summary>
        static void RemoveDuplicateMaterials(SkinnedMeshRenderer renderer) {
            var mesh = renderer.sharedMesh;
            var materials = renderer.sharedMaterials;

            var toMerge = new List<int>[mesh.subMeshCount];
            for (int m_i = 0; m_i < mesh.subMeshCount; m_i++) {
                toMerge[m_i] = new List<int> { m_i };

                if (materials[m_i] == null || materials[m_i].mainTexture == null) continue;

                for (int m_j = m_i + 1; m_j < mesh.subMeshCount; m_j++) {
                    if (materials[m_j] == null || materials[m_j].mainTexture == null) continue;

                    if (materials[m_i].mainTexture == materials[m_j].mainTexture)
                        toMerge[m_i].Add(m_j);
                }
            }

            // Reconstruct submeshes
            var newSubmeshes = new List<List<int>>();
            var newMaterials = new List<Material>();

            int new_m = 0;
            for (int old_m = 0; old_m < mesh.subMeshCount; old_m++) {
                // Skip already merged
                if (toMerge[old_m].Count == 0)
                    continue;

                // Collect materials
                newMaterials.Add(materials[old_m]);

                // Merge submeshes
                newSubmeshes.Add(new List<int>());
                foreach (var merge_m in toMerge[old_m]) {

                    if (merge_m != old_m) {
                        toMerge[merge_m].Clear();
                    }
                    Debug.Log($"Merging {new_m} <- {merge_m}");
                    newSubmeshes.Last().AddRange(mesh.GetTriangles(merge_m));
                }
                new_m++;
            }

            Debug.Assert(newSubmeshes.Count == new_m);

            // Assign new submeshes
            for (int m_i = 0; m_i < newSubmeshes.Count; m_i++) {
                mesh.SetTriangles(newSubmeshes[m_i], m_i);
            }
            mesh.subMeshCount = newSubmeshes.Count;

            // Update materials

            renderer.sharedMaterials = newMaterials.ToArray();
        }

        /// <summary>
        /// Atlasses the textures and generates the proper UV maps
        /// </summary>
        void MakeAtlas(Mesh mesh, Renderer renderer, Exporter e) {
            int uvChannel = 0;
            int materialCount = mesh.subMeshCount;
            var materials = renderer.sharedMaterials;
            Debug.Log($"Found {materialCount} materials:");

            var uvs = new List<Vector2>();
            mesh.GetUVs(uvChannel, uvs);

            var fBounds = new Rect[materialCount];
            var pBounds = new RectInt[materialCount];
            var indices = new int[materialCount][];

            var mainTextures = new Texture[materialCount];
            for (int m_i = 0; m_i < materialCount; m_i++) {
                var m_mat = materials[m_i];
                mainTextures[m_i] = GetSourceQuality(GetMainTex(m_mat));
            }

            var atlas_mainTexName  = Atlasses[0].PropertyName;

            // Collect texture bounds of each material
            for (int m_i = 0; m_i < materialCount; m_i++) {
                var m_tex = mainTextures[m_i];
                var m_mat = materials[m_i];
                var m_texpath = AssetDatabase.GetAssetPath(GetMainTex(m_mat));
                var m_indices = mesh.GetIndices(m_i);
                indices[m_i] = m_indices;

                // Calculate [0, 1] bounds of material, flip y for proper texture copy
                var f_min_x = m_indices.Select(i => uvs[i].x).Min();
                var f_max_x = m_indices.Select(i => uvs[i].x).Max();
                var f_min_y = m_indices.Select(i => 1 - uvs[i].y).Min();
                var f_max_y = m_indices.Select(i => 1 - uvs[i].y).Max();

                var m_fBound = new Rect(f_min_x, f_min_y, f_max_x - f_min_x, f_max_y - f_min_y);

                // Calculate pixel bounds on texture
                var m_pBound = new RectInt(0, 0, 16, 16);

                if (m_tex != null) {
                    var t_width  = m_tex.width;
                    var t_height = m_tex.height;

                    int p_min_x = Mathf.FloorToInt(m_fBound.xMin * t_width);
                    int p_max_x = Mathf.CeilToInt (m_fBound.xMax * t_width);
                    int p_min_y = Mathf.FloorToInt(m_fBound.yMin * t_height);
                    int p_max_y = Mathf.CeilToInt (m_fBound.yMax * t_height);

                    int p_width  = p_max_x - p_min_x;
                    int p_height = p_max_y - p_min_y;

                    m_pBound = new RectInt(p_min_x, p_min_y, p_width, p_height);
                }

                pBounds[m_i] = m_pBound;
                fBounds[m_i] = m_fBound;

                Debug.Log($"Material {m_i}: {m_mat.name}\n{m_indices.Length} vertices\nTrimmed texture bounds: {pBounds[m_i]}.\nUV bounds: {m_fBound}.\n{m_texpath}");
            }

            // ================== //
            // Main Texture Atlas //
            // ================== //

            Debug.Log($"Generating {atlas_mainTexName} atlas.");
            var a_texture = new Texture2D(32,32);
            var a_textures = new Texture2D[materialCount];
            for (int m_i = 0; m_i < materialCount; m_i++) {
                var m_mat    = materials[m_i];
                var m_tex    = mainTextures[m_i];
                var m_pBound = pBounds[m_i];
                var m_fBound = fBounds[m_i];
                var m_path   = AssetDatabase.GetAssetPath(m_tex);

                if (m_tex == null) {
                    a_textures[m_i] = CreateDummyTexture(materials[m_i].color);
                    continue;
                }
    
                var tmp_rt = RenderTexture.GetTemporary(m_pBound.width, m_pBound.height);
                RenderTexture.active = tmp_rt;

                // UV space vs texture space is pain T_T
                var offset = new Vector2(
                    m_fBound.x,
                    1 - m_fBound.y - m_fBound.height
                );

                Graphics.Blit(m_tex, tmp_rt, m_fBound.size, offset);

                var partial_atlas = new Texture2D(m_pBound.width, m_pBound.height, TextureFormat.RGBA32, false);
                partial_atlas.ReadPixels(new Rect(0, 0, tmp_rt.width, tmp_rt.height), 0, 0);
                partial_atlas.Apply();

                a_textures[m_i] = partial_atlas;
                Debug.Log($"{m_path}\nResized from {m_tex.width}x{m_tex.height} to {partial_atlas.width}x{partial_atlas.height}");

                // Clean up, restore old render target
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(tmp_rt);
            }

            var atlas_packing = a_texture.PackTextures(a_textures, PADDING, 8192, false);

            // Save and export main atlas
            var atlas_exportedTexture = e.ExportTexture(a_texture, atlas_mainTexName);
            
            Dictionary<string, Texture> generatedTextures = new Dictionary<string, Texture> {
                { atlas_mainTexName, atlas_exportedTexture }
            };

            // =================== //
            // Pack Other Textures //
            // =================== //

            for (int a_i = 1; a_i < Atlasses.Count; a_i++) {
                var a = Atlasses[a_i];
                Debug.Log($"Generating {a.PropertyName} atlas.");

                var a_layer = new Texture2D(a_texture.width, a_texture.height, TextureFormat.RGBA32, false);
                var a_fallback = Texture2D.blackTexture;
                ShaderPropertyFallback.TryGetValue(a.PropertyName, out a_fallback);

                for (int m_i = 0; m_i < materialCount; m_i++) {
                    var m_mat  = materials[m_i];
                    var m_mtex = mainTextures[m_i];
                    var m_tex  = GetSourceQuality(GetTex(a.PropertyName, m_mat));
                    m_tex = m_tex != null ? m_tex : a_fallback;
                    var m_path   = AssetDatabase.GetAssetPath(m_tex);
                    var m_pBound = pBounds[m_i];
                    var m_fBound = fBounds[m_i];
                    var dst_rect = atlas_packing[m_i];

                    var tmp_rt = RenderTexture.GetTemporary(m_pBound.width, m_pBound.height);
                    RenderTexture.active = tmp_rt;

                    var offset = new Vector2(
                        m_fBound.x,
                        1 - m_fBound.y - m_fBound.height
                    );

                    Graphics.Blit(m_tex, tmp_rt, m_fBound.size, offset);

                    var partial_atlas = new Texture2D(m_pBound.width, m_pBound.height, TextureFormat.RGBA32, false);
                    partial_atlas.ReadPixels(new Rect(0, 0, tmp_rt.width, tmp_rt.height), 0, 0);
                    partial_atlas.Apply();

                    a_textures[m_i] = partial_atlas;
                    Debug.Log($"{m_path}\nResized from {m_tex.width}x{m_tex.height} to {partial_atlas.width}x{partial_atlas.height}");

                    Graphics.CopyTexture(
                        partial_atlas, 0, 0, 0, 0, partial_atlas.width, partial_atlas.height,
                        a_layer, 0, 0, (int)(dst_rect.xMin * a_layer.width), (int)(dst_rect.yMin * a_layer.height));
                    
                    // Clean up, restore old render target
                    RenderTexture.active = null;
                    RenderTexture.ReleaseTemporary(tmp_rt);
                }

                // Export
                var a_exportedTexture = e.ExportTexture(a_layer, a.PropertyName.TrimStart('_'));
                generatedTextures.Add(a.PropertyName, a_exportedTexture);
            }
            AssetDatabase.Refresh();

            // ========= //
            // Update UV //
            // ========= //

            for (int m_i = 0; m_i < materialCount; m_i++) {
                var src_rect = fBounds[m_i];

                // Flip y of src_rect back
                var src_rect_yMin = src_rect.yMin;
                var src_rect_yMax = src_rect.yMax;
                src_rect.yMin = 1 - src_rect_yMax;
                src_rect.yMax = 1 - src_rect_yMin;

                var dst_rect = atlas_packing[m_i];

                var tris = mesh.GetTriangles(m_i);

                Debug.Log($"Mapping UV from {src_rect} to {dst_rect}.");

                // For every unique vertex index in the triangle list of this mesh
                foreach (var index in new HashSet<int>(tris)) {
                    var uv = uvs[index];

                    var uv_in_src = (uv - src_rect.min) / src_rect.size;

                    var uv_in_dst = uv_in_src * dst_rect.size + dst_rect.min;

                    uvs[index] = uv_in_dst;
                }
            }
            mesh.SetUVs(uvChannel, uvs);

            // ================ //
            // Update submeshes //
            // ================ //

            List<List<int>> submeshes = new List<List<int>>();
            for (int g = 0; g < Groups.Count; g++) {
                submeshes.Add(new List<int>());
                Debug.Log($"Group {g}: {Groups[g].Name}");
            }
            var bounds = mesh.bounds;

            for (int s = 0; s < mesh.subMeshCount; s++) {
                var m = renderer.sharedMaterials[s];
                submeshes[FindGroup(m)].AddRange(mesh.GetTriangles(s));
            }

            for (int g = 0; g < Groups.Count; g++) {
                mesh.SetTriangles(submeshes[g], g, false);
            }

            mesh.subMeshCount = Groups.Count;
            mesh.bounds = bounds;
            mesh.UploadMeshData(false);
            Debug.Log($"Reduced {mesh.name} to {mesh.subMeshCount} submesh(es)");

            // ================ //
            // Update materials //
            // ================ //

            var newMaterials = new Material[mesh.subMeshCount];
            for (int g = 0; g < Groups.Count; g++) {
                var group = Groups[g];

                // Get new material
                Material newMaterial = group.ReferenceMaterial;
                if (newMaterial == null) {
                    if (group.ReferenceMaterial != null) {
                        newMaterial = new Material(group.ReferenceMaterial);
                    } else {
                        var shader = group.ReferenceShader;
                        if (shader != null) {
                            newMaterial = new Material(group.ReferenceShader);
                        } else {
                            newMaterial = new Material(Shader.Find("Standard"));
                        }
                    }
                }
                newMaterials[g] = newMaterial;

                foreach (var item in generatedTextures) {
                    newMaterial.SetTexture(item.Key, item.Value);
                }

                e.ExportMaterial(newMaterial, group.Name);
            }

            renderer.sharedMaterials = newMaterials;

            RestoreTexureImports();
            AssetDatabase.Refresh();
        }

        public static Texture2D CreateDummyTexture(Color color) {
            var texture = new Texture2D(DEFAULT_SIZE, DEFAULT_SIZE);
            var pixels = new Color32[texture.width * texture.height];
            var color32 = (Color32)color;

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color32;

            texture.SetPixels32(pixels);
            return texture;
        }

        readonly Dictionary<TextureImporter, Preset> textureImporters = new Dictionary<TextureImporter, Preset>();

        Texture2D GetSourceQuality(Texture2D tex) {
            var path = AssetDatabase.GetAssetPath(tex);

            if (path == null || path == "")
                return tex;

            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            var impOldSettings = new Preset(imp);

            imp.textureType         = TextureImporterType.Default;
            imp.npotScale           = TextureImporterNPOTScale.None;
            imp.textureCompression  = TextureImporterCompression.Uncompressed;
            imp.crunchedCompression = false;

            imp.SaveAndReimport();
            AssetDatabase.Refresh();
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            textureImporters[imp] = impOldSettings;

            return tex;
        }
        void RestoreTexureImports() {
            foreach (var item in textureImporters) {
                item.Value.ApplyTo(item.Key);
                item.Key.SaveAndReimport();
            }
        }
    }

}