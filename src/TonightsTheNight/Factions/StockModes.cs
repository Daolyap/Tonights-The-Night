using System.Collections.Generic;

namespace TonightsTheNight.Factions
{
    /// <summary>
    /// The riot modes that ship with the mod, stored as the same JSON a user would write.
    /// They are written to disk on startup so they double as worked examples: copy one, rename
    /// it, and it is a new mode with no code involved.
    /// </summary>
    public static class StockModes
    {
        /// <summary>
        /// Presence of this marker means the file is still ours to update. Delete it (or edit
        /// the file at all) and updates stop overwriting your version.
        /// </summary>
        public const string Marker = "\"_stock\": true";

        public static Dictionary<string, string> All()
        {
            return new Dictionary<string, string>
            {
                { "pedestrians", Pedestrians }
            };
        }

        private const string Pedestrians = @"{
  // Stock mode - remove the _stock line below to stop updates overwriting your edits.
  ""_stock"": true,

  ""name"": ""Pedestrian Riot"",
  ""description"": ""Two mobs form out of the crowd and go at each other. You are fair game."",
  ""enabled"": true,

  // How the world treats you when you have not picked a side.
  // neutral | dislike | hate
  ""playerRelationship"": ""hate"",

  ""factions"": {

    ""mob_red"": {
      ""name"": ""Red Mob"",
      ""reaction"": ""Fight"",
      ""recruits"": ""civilian"",     // pulled from ambient peds rather than spawned
      ""share"": 0.45,              // relative size among recruits
      ""armedChance"": 0.8,
      ""ammo"": 150,
      ""armour"": 0,
      ""weapons"": [ ""Bat"", ""Crowbar"", ""Hammer"", ""Machete"", ""Pistol"", ""SNSPistol"" ],
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Red"" }
    },

    ""mob_blue"": {
      ""name"": ""Blue Mob"",
      ""reaction"": ""Fight"",
      ""recruits"": ""civilian"",
      ""share"": 0.45,
      ""armedChance"": 0.8,
      ""ammo"": 150,
      ""armour"": 0,
      ""weapons"": [ ""Bat"", ""GolfClub"", ""Knife"", ""Pistol"", ""MicroSMG"" ],
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Blue"" }
    },

    ""bystanders"": {
      ""name"": ""Bystanders"",
      // Mixed rolls per ped, so the crowd splits instead of behaving as one bloc.
      ""reaction"": ""Mixed"",
      ""fightBackChance"": 0.15,
      ""recruits"": ""civilian"",
      // A small seasoning of panic. Most non-fighters are simply never recruited - an
      // untouched ped reacts to gunfire on its own and looks better doing it than one we
      // explicitly told to run.
      ""share"": 0.1,
      ""armedChance"": 0.1,
      ""ammo"": 40,
      ""weapons"": [ ""Bat"", ""Bottle"" ],
      ""blip"": { ""enabled"": false }
    }
  },

  // Relationships are directional in the game; ""mutual"" writes both directions for you.
  ""relations"": [
    { ""from"": ""mob_red"",  ""to"": ""mob_blue"",   ""value"": ""hate"",    ""mutual"": true },
    { ""from"": ""mob_red"",  ""to"": ""bystanders"", ""value"": ""dislike"", ""mutual"": false },
    { ""from"": ""mob_blue"", ""to"": ""bystanders"", ""value"": ""dislike"", ""mutual"": false }
  ],

  // Layer 3 of the config stack: applies only while this mode is running.
  ""config"": {
    ""combat"": { ""accuracy"": 15 }
  }
}
";
    }
}
