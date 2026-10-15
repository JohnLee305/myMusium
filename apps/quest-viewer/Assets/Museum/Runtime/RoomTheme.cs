using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
namespace FamilyMuseum
{
    [Serializable] public sealed class RoomTheme
    {
        public string backWall;
        public string frontWall;
        public string leftWall;
        public string rightWall;
        public string floor;
        public string background;
        public string plinth;
        public string cap;
        public string accent;
        public string plaqueBackground;
        public string plaqueBorder;
        public string plaqueText;
        public string plaqueHint;
        public string signBackground;
        public string signBorder;
        public string signText;
        public string signAccent;
        public string signMount;
        public string detailBackground;
        public string detailText;
        public string detailAccent;
        public string light;
        public string ambient;
        public static readonly RoomTheme Default = new RoomTheme {
            backWall = "#C4BFAD",
            frontWall = "#C4BFAD",
            leftWall = "#C4BFAD",
            rightWall = "#C4BFAD",
            floor = "#2E3B40",
            background = "#1F262B",
            plinth = "#9C9E91",
            cap = "#A35C24",
            accent = "#A35C24",
            plaqueBackground = "#091612",
            plaqueBorder = "#A3753B",
            plaqueText = "#F7F0D4",
            plaqueHint = "#DBBD7D",
            signBackground = "#091814",
            signBorder = "#916129",
            signText = "#F0E8C9",
            signAccent = "#C7A361",
            signMount = "#090B0A",
            detailBackground = "#132621",
            detailText = "#E6E8DB",
            detailAccent = "#B89452",
            light = "#FFF0D6",
            ambient = "#8C9199",
        };
        public static RoomTheme For(MuseumRoom room) => room?.theme ?? Default;
        public void Validate()
        {
            foreach (var field in typeof(RoomTheme).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                var value = (string)field.GetValue(this);
                if (value != null && !Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$")) throw new InvalidDataException("ROOM_THEME_COLOR_INVALID: " + field.Name);
            }
        }
        public UnityEngine.Color Color(string key)
        {
            var field = typeof(RoomTheme).GetField(key);
            var value = (string)field.GetValue(this) ?? (string)field.GetValue(Default);
            ColorUtility.TryParseHtmlString(value, out var color);
            return color;
        }
    }
}
