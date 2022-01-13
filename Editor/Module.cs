using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PerfHammer
{
    public interface Module
    {
        public string Name { get; }

        public void OnGUI();

        public GameObject Run(Exporter e, GameObject obj);
    }
}

