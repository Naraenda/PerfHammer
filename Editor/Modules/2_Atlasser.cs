using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using UnityEngine;
using UnityEditor;
using PerfHammer.Utils;
using System;

namespace PerfHammer
{
    public static class Atlasser
    {
        public static Dictionary<string, Texture2D> ShaderPropertyFallback = new Dictionary<string, Texture2D>() {
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
            public int    UVChannel = 0;
            public Dictionary<Material, Texture2D> Mapping = new Dictionary<Material, Texture2D>();
        }

        public class Configuration
        {

            /// <summary>
            /// UV channels to optimize.
            /// </summary>
            public int[] UVChannels = new int[1] { 0 };

            public List<MaterialGroup> Groups = new List<MaterialGroup>() { new MaterialGroup() };
            //public HashSet<Material> Materials = new HashSet<Material>();

            /// <summary>
            /// Texture atlas sets by name. For example: _MainTex.
            /// </summary>
            public List<Atlas> Atlasses = new List<Atlas>() {
                new Atlas("_MainTex")
            };

            public void Clear() {
                Atlasses = new List<Atlas>() { new Atlas("_MainTex") };
                Groups = new List<MaterialGroup>() { new MaterialGroup() };
            }

            public Dictionary<Material, Texture2D> GetAtlas(string name) {
                return Atlasses.First((x => x.PropertyName == name)).Mapping;
            }

            public void DiscoverMaterials(GameObject go) {
                var rs = go.GetComponentsInChildren<SkinnedMeshRenderer>();
                var detected = new HashSet<Material>(rs.SelectMany(r => r.sharedMaterials));

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
                            atlas.Mapping.Add(m, null);
                }

                foreach (var group in Groups) {
                    group.ReferenceShader = group.ReferenceShader ?? MaterialUtils.GetCommonShader(group.Materials);
                }
            }

            public void AutoFill() {
                var properties = MaterialUtils.GetCommonTextureProperties(GetAllMaterials());
                foreach (var p in properties.Where(p => !Atlasses.Select(a => a.PropertyName).Contains(p))) {
                    var a = AddAtlas(p);
                    AutoFillProperty(p);
                    if (a.Mapping.Select(m => m.Value).All(t => t == null))
                        Atlasses.Remove(a);
                }
            }

            public void AutoFillProperty(string name) {
                var mats = GetAtlas(name);
                for (int m_i = 0; m_i < mats.Count; m_i++) {
                    var kvp = mats.ElementAt(m_i);
                    var mat = kvp.Key;
                    var tex = mat.GetTexture(name) as Texture2D ?? kvp.Value;
                    mats[mat] = tex;
                }
            }
            public Atlas AddAtlas(string name = "") {
                var atlas = new Atlas() {
                    PropertyName = name,
                    Mapping = GetAllMaterials().ToDictionary(k => k, v => (Texture2D)null)
                };

                Atlasses.Add(atlas);
                return atlas;
            }

            public Texture2D GetTex(string name, Material mat)
                => GetAtlas(name)[mat];

            public Texture2D GetMainTex(Material mat)
                => Atlasses.First().Mapping[mat];

            public IEnumerable<Material> GetAllMaterials()
                => Groups.SelectMany(g => g.Materials);

            public int FindGroup(Material m) {
                for (int i = 0; i < Groups.Count; i++) {
                    if (Groups[i].Materials.Contains(m))
                        return i;
                }
                return -1;
            }

            public MaterialGroup GetGroup(Material m)
                => Groups[FindGroup(m)];

            public void MoveMaterial(Material m, int newGroup) {
                int oldGroup = FindGroup(m);

                if (oldGroup < 0) {
                    throw new IndexOutOfRangeException();
                }
                Groups[oldGroup].Materials.Remove(m);

                if (newGroup < Groups.Count) {
                    Groups[newGroup].Materials.Add(m);
                } else {
                    Groups.Add(new MaterialGroup() {
                        Name = $"Atlas {newGroup}",
                        Materials = new HashSet<Material>() { m }
                    });
                }


                if (Groups[oldGroup].Materials.Count <= 0) {
                    Groups.RemoveAt(oldGroup);
                }
            }
        }

        public static int DEFAULT_SIZE = 32;
        public static int PADDING = 0;
        public static void Optimize(GameObject go, Configuration _config, Exporter e) {
            var rs = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var r in rs) {
                RemoveDuplicateMaterials(r);
                MakeAtlas(r.sharedMesh, r, _config, e);
            }
        }

        /// <summary>
        /// Merges same materials
        /// </summary>
        static void RemoveDuplicateMaterials(SkinnedMeshRenderer renderer) {
            var mesh = renderer.sharedMesh;
            var materials = renderer.sharedMaterials;

            var toMerge = new List<int>[mesh.subMeshCount];
            for (int m_i = 0; m_i < mesh.subMeshCount; m_i++) {
                toMerge[m_i] = new List<int> { m_i };

                if (materials[m_i]?.mainTexture == null) continue;

                for (int m_j = m_i + 1; m_j < mesh.subMeshCount; m_j++) {
                    if (materials[m_j]?.mainTexture == null) continue;

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
        static void MakeAtlas(Mesh mesh, Renderer renderer, Configuration c, Exporter e) {
            int uvChannel = 0;
            int materialCount = mesh.subMeshCount;
            var materials = renderer.sharedMaterials;
            Debug.Log($"Found {materialCount} materials:");

            var uvs = new List<Vector2>();
            mesh.GetUVs(uvChannel, uvs);

            var fBounds = new Rect[materialCount];
            var pBounds = new RectInt[materialCount];
            var indices = new int[materialCount][];

            // Collect texture bounds of each material
            for (int m_i = 0; m_i < materialCount; m_i++) {
                var m_mat = materials[m_i];
                var m_texpath = AssetDatabase.GetAssetPath(c.GetMainTex(m_mat));
                var m_indices = mesh.GetIndices(m_i);
                indices[m_i] = m_indices;

                // Calculate [0, 1] bounds of material, flip y for proper texture copy
                var f_min_x = m_indices.Select(i => uvs[i].x).Min();
                var f_max_x = m_indices.Select(i => uvs[i].x).Max();
                var f_min_y = m_indices.Select(i => 1 - uvs[i].y).Min();
                var f_max_y = m_indices.Select(i => 1 - uvs[i].y).Max();

                var m_fBound = new Rect(f_min_x, f_min_y, f_max_x - f_min_x, f_max_y - f_min_y);

                // Calculate pixel bounds on texture
                var t = GetSourceQuality(c.GetMainTex(m_mat));
                var t_width  = t?.width  ?? DEFAULT_SIZE;
                var t_height = t?.height ?? DEFAULT_SIZE;

                int p_min_x = Mathf.FloorToInt(m_fBound.xMin * t_width);
                int p_max_x = Mathf.CeilToInt (m_fBound.xMax * t_width);
                int p_min_y = Mathf.FloorToInt(m_fBound.yMin * t_height);
                int p_max_y = Mathf.CeilToInt (m_fBound.yMax * t_height);

                int p_width  = p_max_x - p_min_x;
                int p_height = p_max_y - p_min_y;

                if (p_width < DEFAULT_SIZE && p_height < DEFAULT_SIZE) {
                    p_width  = DEFAULT_SIZE;
                    p_height = DEFAULT_SIZE;
                }

                var m_pBound = new RectInt(p_min_x, p_min_y, p_width, p_height);
                pBounds[m_i] = m_pBound;
                fBounds[m_i] = m_fBound;

                Debug.Log($"Material {m_i}: {m_indices.Length} vertices\nTrimmed texture bounds: {pBounds[m_i]}.\nUV bounds: {m_fBound}.\n{m_texpath}");
            }

            // ================== //
            // Main Texture Atlas //
            // ================== //

            var atlas_mainTexName  = c.Atlasses[0].PropertyName;
            Debug.Log($"Generating {atlas_mainTexName} atlas.");
            var a_texture = new Texture2D(32,32);
            var a_textures = new Texture2D[materialCount];
            for (int m_i = 0; m_i < materialCount; m_i++) {
                var m_mat    = materials[m_i];
                var m_tex    = GetSourceQuality(c.GetMainTex(m_mat));
                var m_pBound = pBounds[m_i];
                var m_path   = AssetDatabase.GetAssetPath(m_tex);

                if (m_tex == null) {
                    a_textures[m_i] = CreateDummyTexture(materials[m_i].color);
                    continue;
                }

                // Save render target
                var old_target = RenderTexture.active;
                var tmp_rt = RenderTexture.GetTemporary(m_tex.width, m_tex.height, 0);
                Graphics.Blit(m_tex, tmp_rt);
                RenderTexture.active = tmp_rt;

                var partial_atlas = new Texture2D(m_pBound.width, m_pBound.height, TextureFormat.RGBA32, false);
                partial_atlas.ReadPixels(new Rect(m_pBound.position, m_pBound.size), 0, 0, false);
                partial_atlas.Apply();

                a_textures[m_i] = partial_atlas;
                Debug.Log($"{m_path}\nResized from {m_tex.width}x{m_tex.height} to {partial_atlas.width}x{partial_atlas.height}");

                // Clean up, restore old render target
                RenderTexture.active = old_target;
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

            for (int a_i = 1; a_i < c.Atlasses.Count; a_i++) {
                var a = c.Atlasses[a_i];
                Debug.Log($"Generating {a.PropertyName} atlas.");

                var a_layer = new Texture2D(a_texture.width, a_texture.height, TextureFormat.RGBA32, false);
                var a_fallback = Texture2D.blackTexture;
                ShaderPropertyFallback.TryGetValue(a.PropertyName, out a_fallback);

                for (int m_i = 0; m_i < materialCount; m_i++) {
                    var m_mat = materials[m_i];
                    var pBound = pBounds[m_i];
                    var mainTex  = GetSourceQuality(c.GetMainTex(m_mat));
                    //var m_tex  = c.GetTex(a.PropertyName, m_mat) ?? CreateDummyTexture(a_fallback);
                    var m_tex  = GetSourceQuality(c.GetTex(a.PropertyName, m_mat)) ?? a_fallback;
                    var dst_rect = atlas_packing[m_i];

                    var old_target = RenderTexture.active;
                    var tmp_rt = RenderTexture.GetTemporary(mainTex?.width ?? DEFAULT_SIZE, mainTex?.height ?? DEFAULT_SIZE, 0);
                    Graphics.Blit(m_tex, tmp_rt);
                    RenderTexture.active = tmp_rt;

                    var partial_atlas = new Texture2D(pBound.width, pBound.height, TextureFormat.RGBA32, false);
                    partial_atlas.ReadPixels(new Rect(pBound.position, pBound.size), 0, 0, false);
                    partial_atlas.Apply();

                    Graphics.CopyTexture(
                        partial_atlas, 0, 0, 0, 0, partial_atlas.width, partial_atlas.height,
                        a_layer, 0, 0, (int)(dst_rect.xMin * a_layer.width), (int)(dst_rect.yMin * a_layer.height));
                    // Clean up, restore old render target
                    RenderTexture.active = old_target;
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
            for (int g = 0; g < c.Groups.Count; g++) {
                submeshes.Add(new List<int>());
                Debug.Log("Added group!");
            }
            var bounds = mesh.bounds;

            for (int s = 0; s < mesh.subMeshCount; s++) {
                var m = renderer.sharedMaterials[s];

                Debug.Log($"Mapping material {m.name} to index {c.FindGroup(m)}");
                submeshes[c.FindGroup(m)].AddRange(mesh.GetTriangles(s));
            }

            for (int g = 0; g < c.Groups.Count; g++) {
                mesh.SetTriangles(submeshes[g], g, false);
            }
            mesh.subMeshCount = c.Groups.Count;
            mesh.bounds = bounds;
            mesh.UploadMeshData(false);
            Debug.Log($"Reduced {mesh.name} to {mesh.subMeshCount} submesh(es)");

            // ================ //
            // Update materials //
            // ================ //

            var newMaterials = new Material[mesh.subMeshCount];
            for (int g = 0; g < c.Groups.Count; g++) {
                var group = c.Groups[g];
                Material newMaterial = group.ReferenceMaterial != null ? 
                    new Material(group.ReferenceMaterial) : 
                    new Material(group.ReferenceShader ?? Shader.Find("Standard"));
                newMaterials[g] = newMaterial;

                foreach (var item in generatedTextures) {
                    newMaterial.SetTexture(item.Key, item.Value);
                }

                e.ExportMaterial(newMaterial, group.Name);
            }

            renderer.sharedMaterials = newMaterials;

            AssetDatabase.Refresh();
        }

        public static Texture2D CreateDummyTexture(Color color) {
            var texture = new Texture2D(DEFAULT_SIZE, DEFAULT_SIZE);
            var pixels = new Color[texture.width * texture.height];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;

            texture.SetPixels(pixels);
            return texture;
        }

        public static Texture2D GetSourceQuality(Texture2D tex) {
            var path = AssetDatabase.GetAssetPath(tex);
            if (path == null || path == "")
                return tex;
            var src = new Texture2D(0, 0);
            src.LoadImage(File.ReadAllBytes(path));
            return src;
        }
    }

}