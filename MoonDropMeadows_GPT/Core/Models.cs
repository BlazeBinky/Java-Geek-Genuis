namespace MoonDropMeadows.Core;

public sealed class CharacterData
{
    public string Name { get; set; } = "Rowan";
    public int Age { get; set; } = 18;
    public int Money { get; set; } = 500;
    public int Energy { get; set; } = 100;
    public int Seeds { get; set; } = 8;
    public int Knowledge { get; set; }
    public string Career { get; set; } = "Undecided";
    public string Outfit { get; set; } = "Everyday";
    public bool HasSibling { get; set; } = true;
}
public sealed class RelationshipData
{
    public string CharacterId { get; set; } = "";
    public int Affection { get; set; }
    public bool IsDating { get; set; }
    public bool IsPartner { get; set; }
    public bool IsMarried { get; set; }
    public int Dates { get; set; }
}
public sealed class CropData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int DaysGrown { get; set; }
    public bool WateredToday { get; set; }
}
public sealed class ChildData
{
    public string Name { get; set; } = "Little Sprout";
    public int Age { get; set; }
    public string Personality { get; set; } = "Curious";
}
public sealed class BusinessData
{
    public string Name { get; set; } = "MoonDrop Workshop";
    public string Type { get; set; } = "Workshop";
    public int Level { get; set; } = 1;
    public int DailyRevenue { get; set; } = 45;
    public List<string> Employees { get; set; } = new();
}
public sealed class GameSave
{
    public CharacterData Character { get; set; } = new();
    public int Year { get; set; } = 1;
    public int DayOfYear { get; set; } = 1;
    public int TimeMinutes { get; set; } = 8 * 60;
    public int TotalDaysLivedHere { get; set; }
    public int StoryStep { get; set; }
    public int MoonPetals { get; set; }
    public int SchoolDays { get; set; }
    public bool VoiceActingEnabled { get; set; }
    public List<RelationshipData> Relationships { get; set; } = new();
    public List<CropData> Crops { get; set; } = new();
    public List<ChildData> Children { get; set; } = new();
    public BusinessData? Business { get; set; }
    public List<string> GatheredItems { get; set; } = new();
    public List<string> OwnedProperties { get; set; } = new();
    public List<string> PendingResidents { get; set; } = new();
    public List<string> Mail { get; set; } = new();
}
