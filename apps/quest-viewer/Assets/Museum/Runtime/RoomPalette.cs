using System.Collections.Generic;
using UnityEngine;

namespace FamilyMuseum
{
    // Own every recoloured material so neither scene templates nor other rooms are mutated.
    public sealed class RoomPalette : MonoBehaviour
    {
        readonly Dictionary<Renderer, Material> materials = new Dictionary<Renderer, Material>();
        public void Paint(GameObject target, Color color)
        {
            if (target == null) return;
            var renderer = target.GetComponent<Renderer>();
            if (renderer == null) return;
            if (!materials.TryGetValue(renderer, out var material))
            {
                material = new Material(renderer.sharedMaterial);
                renderer.sharedMaterial = material;
                materials.Add(renderer, material);
            }
            material.color = color;
        }
        public void ApplyRoom(RoomTheme theme)
        {
            Paint(GameObject.Find("Floor"), theme.Color("floor"));
            Paint(GameObject.Find("Back wall"), theme.Color("backWall"));
            Paint(GameObject.Find("Front wall"), theme.Color("frontWall"));
            Paint(GameObject.Find("Left wall"), theme.Color("leftWall"));
            Paint(GameObject.Find("Right wall"), theme.Color("rightWall"));
            Paint(GameObject.Find("Wall accent"), theme.Color("accent"));
            GameObject.Find("Gallery light").GetComponent<Light>().color = theme.Color("light");
            RenderSettings.ambientLight = theme.Color("ambient");
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                camera.backgroundColor = theme.Color("background");
        }
        void OnDestroy()
        {
            foreach (var material in materials.Values) if (material != null) Destroy(material);
            materials.Clear();
        }
    }
}
