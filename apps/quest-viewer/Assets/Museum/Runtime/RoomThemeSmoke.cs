using System.Collections;
using System.Linq;
using UnityEngine;

namespace FamilyMuseum
{
    public sealed class RoomThemeSmoke : MonoBehaviour
    {
        public bool Passed { get; private set; } = true;
        static bool Same(Color a, Color b) => Mathf.Abs(a.r-b.r)<.005f && Mathf.Abs(a.g-b.g)<.005f && Mathf.Abs(a.b-b.b)<.005f;
        bool Check(GalleryDisplay gallery)
        {
            var theme = gallery.CurrentTheme;
            bool result = true;
            string[] objects = {"Floor", "Back wall", "Front wall", "Left wall", "Right wall", "Wall accent"};
            string[] keys = {"floor", "backWall", "frontWall", "leftWall", "rightWall", "accent"};
            for (int i=0; i<keys.Length; i++) result &= Same(GameObject.Find(objects[i]).GetComponent<Renderer>().sharedMaterial.color, theme.Color(keys[i]));
            result &= Same(RenderSettings.ambientLight, theme.Color("ambient"));
            result &= Same(GameObject.Find("Gallery light").GetComponent<Light>().color, theme.Color("light"));
            result &= Same(Camera.main.backgroundColor, theme.Color("background"));
            foreach (var slot in FindObjectsByType<ArtifactDisplay>(FindObjectsSortMode.None).Where(s=>s.managedSlot))
            {
                result &= Same(slot.titleLabel.color, theme.Color("plaqueText"));
                result &= Same(slot.descriptionLabel.color, theme.Color("plaqueText"));
                foreach (var renderer in slot.GetComponentsInChildren<Renderer>())
                {
                    string key = renderer.name == "Display cap(Clone)" ? "cap" : renderer.name == "Display plinth(Clone)" ? "plinth" :
                        renderer.name == "Green face" ? "plaqueBackground" : renderer.name == "Brass frame" ? "plaqueBorder" : null;
                    if (key != null) result &= Same(renderer.sharedMaterial.color, theme.Color(key));
                }
            }
            result &= Same(GameObject.Find("Museum title").GetComponent<TextMesh>().color, theme.Color("signText"));
            result &= ArtifactDisplay.ActiveImports == gallery.LoadedCount;
            Debug.Log($"THEME_CHECK: room={gallery.CurrentRoom+1} wall={ColorUtility.ToHtmlStringRGB(theme.Color("backWall"))} floor={ColorUtility.ToHtmlStringRGB(theme.Color("floor"))} passed={result}");
            return result;
        }
        public IEnumerator Run(GalleryDisplay gallery)
        {
            Passed = gallery.RoomCount >= 2 && Check(gallery);
            if (gallery.RoomCount >= 2)
            {
                foreach (int target in new[] {1,0,1,0})
                {
                    var transition=gallery.SwitchRoomAsync(target);
                    while (!transition.IsCompleted) yield return null;
                    Passed &= !transition.IsFaulted && gallery.CurrentRoom==target && Check(gallery);
                }
            }
            Debug.Log(Passed ? "THEME_SMOKE_PASS" : "THEME_SMOKE_FAIL");
        }
    }
}
