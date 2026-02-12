using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EliteExplorerTool
{
    public static class EliteLogic
    {
        // --- DEFINICIÓN DE RECETAS JUMPONIUM ---
        // Usamos HashSet para búsqueda ultra-rápida
        private static readonly HashSet<string> BasicIng = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Carbon", "Vanadium", "Germanium" };
        private static readonly HashSet<string> StdIng = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Carbon", "Vanadium", "Germanium", "Cadmium", "Niobium" };
        private static readonly HashSet<string> PremIng = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Carbon", "Germanium", "Arsenic", "Niobium", "Yttrium", "Polonium" };

        public enum JumpLevel { None, Basic, Standard, Premium }

        // Calcula el nivel más alto disponible
        public static JumpLevel GetJumponiumLevel(List<string> planetMaterials)
        {
            if (planetMaterials == null || planetMaterials.Count == 0) return JumpLevel.None;

            // Verificamos de mayor a menor calidad
            // IMPORTANTE: .IsSubsetOf verifica si TODOS los ingredientes están en el planeta
            bool hasPremium = PremIng.IsSubsetOf(planetMaterials);
            if (hasPremium) return JumpLevel.Premium;

            bool hasStandard = StdIng.IsSubsetOf(planetMaterials);
            if (hasStandard) return JumpLevel.Standard;

            bool hasBasic = BasicIng.IsSubsetOf(planetMaterials);
            if (hasBasic) return JumpLevel.Basic;

            return JumpLevel.None;
        }

        // Genera el texto para el Tooltip (Menú contextual al pasar el mouse)
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
            foreach (var mat in StdIng)
            {
                // Solo listamos los extra o todos si prefieres. Listamos todos para claridad.
                sb.AppendLine($"  [{(planetMaterials.Contains(mat) ? "✔" : " ")}] {mat}");
            }

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