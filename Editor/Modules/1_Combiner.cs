using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PerfHammer
{
    public static class Combiner
    {
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

        public class Config
        {
            public HashSet<Combineable> Combineables;

            public void Discover(GameObject go) {
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
            }
        }
    }
}
