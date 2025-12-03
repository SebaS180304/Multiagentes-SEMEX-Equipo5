public static class TrafficRoutes
{
    // =========================
    // ENTRADA 1 (E1)
    // =========================

    public static readonly string[][] FromE1 = new string[][]
    {
        new[] { "E1", "P2", "P3", "S2" },
        new[] { "E1", "P2", "P6", "P5", "P4", "S3" },
        new[] { "E1", "P2", "P6", "P8", "P10", "P11", "P13", "P17", "P21", "P22", "S4" },
        new[] { "E1", "P2", "P6", "P8", "P10", "P11", "P13", "P17", "P21", "P25", "P29", "P33", "S6" },
        new[] { "E1", "P2", "P6", "P8", "P10", "P11", "P13", "P17", "P21", "P25", "P29", "P28", "P27", "S5" }
    };

    // =========================
    // ENTRADA 2 (E2)
    // =========================

    public static readonly string[][] FromE2 = new string[][]
    {
        new[] { "E2", "P14", "P13", "P12", "P9", "P7", "P5", "P4", "S3" },
        new[] { "E2", "P14", "P13", "P12", "P9", "P7", "P5", "P1", "S1" },
        new[] { "E2", "P14", "P13", "P17", "P21", "P22", "S4" },
        new[] { "E2", "P14", "P13", "P17", "P21", "P25", "P29", "P33", "S6" },
        new[] { "E2", "P14", "P13", "P17", "P21", "P25", "P29", "P28", "P27", "S5" }
    };

    // =========================
    // ENTRADA 3 (E3)
    // =========================

    public static readonly string[][] FromE3 = new string[][]
    {
        new[] { "E3", "P18", "P23", "P27", "S5" },
        new[] { "E3", "P18", "P19", "P20", "P16", "P15", "P12", "P9", "P7", "P5", "P4", "S3" },
        new[] { "E3", "P18", "P19", "P20", "P16", "P15", "P12", "P9", "P7", "P5", "P1", "S1" },
        new[] { "E3", "P18", "P19", "P20", "P16", "P15", "P12", "P9", "P7", "P5", "P1", "P2", "P3", "S2" },
        new[] { "E3", "P18", "P19", "P20", "P21", "P22", "S4" },
        new[] { "E3", "P18", "P19", "P20", "P21", "P25", "P29", "P33", "S6" }
    };

    // =========================
    // ENTRADA 4 (E4)
    // =========================

    public static readonly string[][] FromE4 = new string[][]
    {
        new[] { "E4", "P31", "P26", "P22", "S4" },
        new[] { "E4", "P31", "P30", "P29", "P33", "S6" },
        new[] { "E4", "P31", "P30", "P29", "P28", "P27", "S5" },
        new[] { "E4", "P31", "P30", "P29", "P28", "P24", "P20", "P16", "P15", "P12", "P9", "P7", "P5", "P4", "S3" },
        new[] { "E4", "P31", "P30", "P29", "P28", "P24", "P20", "P16", "P15", "P12", "P9", "P7", "P5", "P1", "P2", "P3", "S2" },
        new[] { "E4", "P31", "P30", "P29", "P28", "P24", "P20", "P16", "P15", "P12", "P9", "P7", "P5", "P1", "S1" }
    };

    // =========================
    // ENTRADA 5 (E5)
    // =========================

    public static readonly string[][] FromE5 = new string[][]
    {
        new[] { "E5", "P34", "P32", "P28", "P27", "S5" },
        new[] { "E5", "P34", "P32", "P28", "P24", "P20", "P16", "P15", "P12", "P9", "P7", "P5", "P4", "S3" },
        new[] { "E5", "P34", "P32", "P28", "P24", "P20", "P16", "P15", "P12", "P9", "P7", "P5", "P1", "S1" },
        new[] { "E5", "P34", "P32", "P28", "P24", "P20", "P16", "P15", "P12", "P9", "P7", "P5", "P1", "P2", "P3", "S2" },
        new[] { "E5", "P34", "P32", "P28", "P24", "P20", "P21", "P22", "S4" }
    };

    // Helper para obtener rutas según la entrada
    public static string[][] GetRoutesFromEntry(string entryId)
    {
        switch (entryId)
        {
            case "E1": return FromE1;
            case "E2": return FromE2;
            case "E3": return FromE3;
            case "E4": return FromE4;
            case "E5": return FromE5;
            default:
                UnityEngine.Debug.LogError($"TrafficRoutes: entrada desconocida '{entryId}'");
                return null;
        }
    }
}
