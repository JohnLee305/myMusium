using System.Collections.Generic;
using UnityEngine;

namespace FamilyMuseum
{
    [CreateAssetMenu(menuName="Museum/Visitors/Catalog")]
    public sealed class VisitorCatalog : ScriptableObject
    {
        public List<VisitorAppearance> appearances=new List<VisitorAppearance>();
        public List<VisitorAppearance> GetAvailable()
        {
            var result=new List<VisitorAppearance>(); var ids=new HashSet<string>();
            foreach(var appearance in appearances)
            {
                if(appearance==null || !appearance.IsUsable || !ids.Add(appearance.id)) continue;
                result.Add(appearance);
            }
            return result;
        }
        public VisitorAppearance Pick(System.Random random)
        {
            var entries=GetAvailable(); float sum=0;
            foreach(var entry in entries) sum+=entry.weight;
            double pick=random.NextDouble()*sum;
            foreach(var entry in entries) { pick-=entry.weight; if(pick<=0) return entry; }
            return entries.Count>0?entries[entries.Count-1]:null;
        }
    }
}
