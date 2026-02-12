using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EliteExplorerTool
{
    public static class EliteLogic
    {
        // --- JUMPONIUM ---
        private static readonly HashSet<string> BasicIng = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Carbon", "Vanadium", "Germanium" };
        private static readonly HashSet<string> StdIng = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Carbon", "Vanadium", "Germanium", "Cadmium", "Niobium" };
        private static readonly HashSet<string> PremIng = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Carbon", "Germanium", "Arsenic", "Niobium", "Yttrium", "Polonium" };

        public enum JumpLevel { None, Basic, Standard, Premium }

        // --- CLASE PARA RUTA SPANSH ---
        public class SpanshJump
        {
            public string SystemName { get; set; }
            public double Distance { get; set; }
            public bool IsNeutron { get; set; }
            public bool IsDone { get; set; }
        }

        // --- ESTRELLAS RECARGABLES (SCOOPABLE) ---
        // Tipos: O, B, A, F, G, K, M
        private static readonly HashSet<string> ScoopableTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "O", "B", "A", "F", "G", "K", "M" };

        public static bool IsScoopable(string starType)
        {
            if (string.IsNullOrEmpty(starType)) return false;

            // Tomamos la primera letra por si viene con subclase (ej: "G2 V")
            string mainType = starType.Substring(0, 1).ToUpper();
            return ScoopableTypes.Contains(mainType);
        }

        public static JumpLevel GetJumponiumLevel(List<string> planetMaterials)
        {
            if (planetMaterials == null || planetMaterials.Count == 0) return JumpLevel.None;
            if (PremIng.IsSubsetOf(planetMaterials)) return JumpLevel.Premium;
            if (StdIng.IsSubsetOf(planetMaterials)) return JumpLevel.Standard;
            if (BasicIng.IsSubsetOf(planetMaterials)) return JumpLevel.Basic;
            return JumpLevel.None;
        }

        public static string GetMaterialTooltip(List<string> planetMaterials)
        {
            if (planetMaterials == null || planetMaterials.Count == 0) return "No materials detected.";
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("FSD INJECTION MATERIALS FOUND:");
            sb.AppendLine("--------------------------------");
            sb.AppendLine("BASIC:");
            foreach (var mat in BasicIng) sb.AppendLine($"  [{(planetMaterials.Contains(mat) ? "✔" : " ")}] {mat}");
            sb.AppendLine("");
            sb.AppendLine("STANDARD:");
            foreach (var mat in StdIng) sb.AppendLine($"  [{(planetMaterials.Contains(mat) ? "✔" : " ")}] {mat}");
            sb.AppendLine("");
            sb.AppendLine("PREMIUM:");
            foreach (var mat in PremIng) sb.AppendLine($"  [{(planetMaterials.Contains(mat) ? "✔" : " ")}] {mat}");
            return sb.ToString();
        }

        public static string GetStarDescription(string code)
        {
            if (code == "O") return "O (Blue-White)";
            if (code == "B") return "B (Blue-White)";
            if (code == "A") return "A (Blue-White)";
            if (code == "F") return "F (White)";
            if (code == "G") return "G (White-Yellow)";
            if (code == "K") return "K (Yellow-Orange)";
            if (code == "M") return "M (Red Dwarf)";
            if (code == "L") return "L (Brown Dwarf)";
            if (code == "T") return "T (Brown Dwarf)";
            if (code == "Y") return "Y (Brown Dwarf)";
            if (code == "TTS") return "T Tauri";
            if (code == "AeBe") return "Herbig Ae/Be";
            if (code == "N" || code == "Neutron") return "Neutron Star";
            if (code == "H" || code == "BlackHole") return "Black Hole";
            if (code != null && code.StartsWith("D")) return $"{code} (White Dwarf)";
            return code ?? "Unknown";
        }

        public static long CalculateValue(string type, bool terraformable, bool isStar)
        {
            if (type == null) return 0;
            if (isStar)
            {
                if (type == "N" || type == "Neutron") return 50000;
                if (type == "H" || type == "BlackHole") return 60000;
                if (type.StartsWith("D")) return 14000;
                return 1200;
            }
            if (type == "Earthlike body") return 3200000;
            if (type == "Ammonia world") return 1700000;
            if (type == "Water world") return terraformable ? 2700000 : 1000000;
            if (type == "High metal content body") return terraformable ? 1800000 : 35000;
            if (terraformable) return 1000000;
            if (type.Contains("Gas giant") && type.Contains("Class II")) return 45000;
            return 1000;
        }
    }
}