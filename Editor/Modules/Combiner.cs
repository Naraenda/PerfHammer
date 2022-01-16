using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PerfHammer.Utils;

namespace PerfHammer
{
    public class Combiner : IModule
    {
        public enum BoneMergeMode
        {
            Keep,
            ToParent,
        }

        public class MergeableBone
        {
            public Transform Bone = null;
            public IList<MergeableBone> Children = new List<MergeableBone>();
            public BoneMergeMode Mode = BoneMergeMode.Keep;
            public bool ShowChildren = true;

            public void OnGUI(int indent = 0) {

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20 * indent);
                ShowChildren = EditorGUILayout.Foldout(ShowChildren || Children.Count == 0, GUIContent.none, false, GUIStyles.FoldIcon);
                EditorGUILayout.ObjectField(Bone.gameObject, typeof(GameObject), true);
                Mode = (BoneMergeMode)EditorGUILayout.EnumPopup(Mode, GUILayout.Width(90));
                EditorGUILayout.EndHorizontal();

                if (Children == null || !ShowChildren)
                    return;

                foreach (var child in Children) {
                    child.OnGUI(indent + 1);
                }
            }

            public IEnumerable<MergeableBone> Traverse() {
                return TreeHelper.Traverse(this, node => node.Children);
            }

            public void MergeAsProxy(Transform proxyRoot, Transform targetRoot, SkinnedMeshCombiner.BoneMergeBuilder merger, Transform parent = null) {
                var path = AnimationUtility.CalculateTransformPath(Bone, proxyRoot);
                var trueBone = targetRoot.Find(path);

                foreach (var child in Children)
                    child.MergeAsProxy(proxyRoot, targetRoot, merger, trueBone);

                if (parent != null && Mode == BoneMergeMode.ToParent) {
                    Debug.Log($"Merging {trueBone.name} to {parent.name}");
                    merger.Merge(trueBone, parent);
                }
            }

            public void Merge(SkinnedMeshCombiner.BoneMergeBuilder merger, Transform parent = null) {
                foreach (var child in Children)
                    child.Merge(merger, Bone);

                if (parent != null && Mode == BoneMergeMode.ToParent)
                    merger.Merge(Bone, parent);
            }
        }

        public class Combineable
        {
            public GameObject GameObject;
            public bool Combine = true;

            public override bool Equals(object obj) {
                return GameObject.Equals((obj as Combineable)?.GameObject);
            }

            public override int GetHashCode() {
                return GameObject.GetHashCode();
            }
        }

        public Transform SourceTransformRoot = null;
        public List<MergeableBone> Armatures = new List<MergeableBone>();

        public IEnumerable<MergeableBone> GetMergeableBones()
            => Armatures.SelectMany(a => a.Traverse());

        public HashSet<Combineable> Combineables = new HashSet<Combineable>();

        public string Name => "Combiner";

        public void Discover(GameObject go) {
            SourceTransformRoot = go.transform;
            var sms = go.GetComponentsInChildren<SkinnedMeshRenderer>().Select(c => new Combineable() {
                GameObject = c.gameObject,
                Combine    = true,
            });
            var mfs = go.GetComponentsInChildren<MeshFilter>().Select(c => new Combineable() {
                GameObject = c.gameObject,
                Combine    = false,
            });

            var discovered = new HashSet<Combineable>(sms.Union(mfs));

            foreach (var item in Combineables.Except(discovered))
                Combineables.Remove(item);

            foreach (var item in discovered.Except(Combineables))
                Combineables.Add(item);

            var bones = Combineables
                .Select(c => c.GameObject.GetComponent<SkinnedMeshRenderer>())
                .Where(c => c != null)
                .SelectMany(c => c.bones)
                .ToList();

            Armatures = bones.Select(b => new MergeableBone() {
                Bone = b
            }).ToList();

            Debug.Log($"Detected {Armatures.Count} bones.");

            for (int i = 0; i < Armatures.Count; i++) {
                var arm = Armatures[i];
                var parents = arm.Bone.GetComponentsInParent<Transform>();

                // Find parent
                MergeableBone parent = null;
                foreach (var candidate in parents) {
                    var found = GetMergeableBones().FirstOrDefault(x => x.Bone == candidate);
                    if (found != null && found != arm) {
                        parent = found;
                        break;
                    }
                }
                if (parent != null) {
                    parent.Children.Add(arm);
                    Armatures.RemoveAt(i--);
                }
            }

            foreach (var a in Armatures.SelectMany(a => a.Children.SelectMany(b => b.Children))) {
                a.ShowChildren = false;
            }
        }

        public GameObject Run(Exporter e, GameObject obj) {
            var components = obj.GetComponentsInChildren<SkinnedMeshRenderer>();
            var targetComponent = components.First();

            var merges = components.SelectMany(r =>
                Enumerable.Range(0, r.sharedMesh.subMeshCount).Select(s =>
                    new SkinnedMeshInstance {
                        Mesh = r.sharedMesh,
                        SMR = r,
                        SubMeshIndex = s,
                        Transform = Matrix4x4.identity,
                        Material = r.sharedMaterials[s],
                    }
                )
            ).ToList();

            SkinnedMeshCombiner.CombineMeshes(merges, targetComponent);

            foreach (var c in components.Except(new[] { targetComponent }))
                Object.DestroyImmediate(c.gameObject);

            targetComponent.sharedMesh.name = $"{targetComponent.gameObject.name}";

            // Do bone merging
            var merger = new SkinnedMeshCombiner.BoneMergeBuilder(targetComponent);
            foreach (var arm in Armatures)
                arm.MergeAsProxy(SourceTransformRoot, obj.transform, merger);
            merger.Apply();

            return obj;
        }

        public void SetBoneMergeMode(Combineable c, BoneMergeMode m) {
            var bones = c.GameObject.GetComponent<SkinnedMeshRenderer>()?.bones;
            if (bones == null || bones.Length == 0)
                return;

            foreach (var b in Armatures.SelectMany(a => a.Traverse())) {
                if (bones.Contains(b.Bone))
                    b.Mode = m;
            }
        }

        bool _isMeshesShown = true;
        bool _isBonesShown = true;

        public void OnGUI(GameObject selectedObject) {
            CustomUI.Section("Meshes", ref _isMeshesShown, () => {
                if (GUILayout.Button("Reload Meshes & Bones"))
                    Discover(selectedObject);

                foreach (var item in Combineables) {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(item.GameObject, typeof(GameObject), true);
                    EditorGUILayout.BeginVertical();

                    if (GUILayout.Button("Merge bones to parents"))
                        SetBoneMergeMode(item, BoneMergeMode.ToParent);

                    if (GUILayout.Button("Keep bones"))
                        SetBoneMergeMode(item, BoneMergeMode.Keep);

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                }
            });

            CustomUI.Section("Bones", ref _isBonesShown, () => {
                if (GUILayout.Button("Reload Meshes & Bones"))
                    Discover(selectedObject);
                foreach (var armature in Armatures) {
                    armature.OnGUI();
                }
            });
        }
    }
}
