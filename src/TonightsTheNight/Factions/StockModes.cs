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
                { "pedestrians", Pedestrians },
                { "criminals", Criminals },
                { "police", Police },
                { "military", Military },
                { "animals", Animals },
                { "aliens", Aliens },
                { "purge", Purge },
                { "everything", Everything }
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
      // Weighted. A riot fought with bats lasts; one fought with pistols kills off the
      // street faster than the game repopulates it and fizzles into an empty road.
      ""weapons"": [
        { ""name"": ""Bat"",       ""weight"": 4 },
        { ""name"": ""Crowbar"",   ""weight"": 3 },
        { ""name"": ""Hammer"",    ""weight"": 3 },
        { ""name"": ""Machete"",   ""weight"": 2 },
        { ""name"": ""Pistol"",    ""weight"": 1 },
        { ""name"": ""SNSPistol"", ""weight"": 1 }
      ],
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
      ""weapons"": [
        { ""name"": ""Bat"",      ""weight"": 4 },
        { ""name"": ""GolfClub"", ""weight"": 3 },
        { ""name"": ""Knife"",    ""weight"": 3 },
        { ""name"": ""Hammer"",   ""weight"": 2 },
        { ""name"": ""Pistol"",   ""weight"": 1 },
        { ""name"": ""MicroSMG"", ""weight"": 1 }
      ],
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
      ""weapons"": [ ""Bottle"", ""Bat"" ],
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

        private const string Criminals = @"{
  ""_stock"": true,
  ""name"": ""Gang War"",
  ""description"": ""Every gang in Los Santos settles up at once. Civilians are in the way."",
  ""enabled"": true,
  ""playerRelationship"": ""hate"",

  ""ambience"": { ""weather"": ""EXTRASUNNY"", ""hour"": 19, ""timecycle"": ""hud_def_desat_Neutral"", ""timecycleStrength"": 0.4 },

  ""escalation"": {
    ""phases"": [
      { ""name"": ""Turf Dispute"", ""durationSeconds"": 90 },
      { ""name"": ""Open War"", ""durationSeconds"": 180, ""fires"": true, ""killThreshold"": 20 },
      { ""name"": ""Nobody Left To Ask"", ""durationSeconds"": 0, ""fires"": true }
    ]
  },

  ""factions"": {
    ""ballas"": {
      ""name"": ""Ballas"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1,
      ""armedChance"": 1.0, ""ammo"": 200, ""armour"": 25, ""accuracy"": 30,
      ""weapons"": [
        { ""name"": ""MicroSMG"", ""weight"": 3 }, { ""name"": ""Pistol"", ""weight"": 3 },
        { ""name"": ""SawnOffShotgun"", ""weight"": 2 }, { ""name"": ""AssaultRifle"", ""weight"": 1 }
      ],
      ""spawn"": {
        ""models"": [ ""g_m_y_ballasout_01"", ""g_m_y_ballaeast_01"", ""g_f_y_ballas_01"" ],
        ""vehicles"": [ ""manana"", ""buccaneer"", ""tornado"" ],
        ""maxAlive"": 14, ""perWave"": 3, ""waveIntervalMs"": 14000, ""inVehicleChance"": 0.35, ""occupants"": 3
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Purple"" }
    },

    ""families"": {
      ""name"": ""Families"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1,
      ""armedChance"": 1.0, ""ammo"": 200, ""armour"": 25, ""accuracy"": 30,
      ""weapons"": [
        { ""name"": ""MicroSMG"", ""weight"": 3 }, { ""name"": ""Pistol"", ""weight"": 3 },
        { ""name"": ""PumpShotgun"", ""weight"": 2 }, { ""name"": ""AssaultRifle"", ""weight"": 1 }
      ],
      ""spawn"": {
        ""models"": [ ""g_m_y_famca_01"", ""g_m_y_famfor_01"", ""g_f_y_families_01"" ],
        ""vehicles"": [ ""peyote"", ""tornado"", ""emperor"" ],
        ""maxAlive"": 14, ""perWave"": 3, ""waveIntervalMs"": 14000, ""inVehicleChance"": 0.35, ""occupants"": 3
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Green"" }
    },

    ""vagos"": {
      ""name"": ""Vagos"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 1,
      ""armedChance"": 1.0, ""ammo"": 200, ""armour"": 25, ""accuracy"": 30,
      ""weapons"": [
        { ""name"": ""APPistol"", ""weight"": 3 }, { ""name"": ""MicroSMG"", ""weight"": 3 },
        { ""name"": ""AssaultRifle"", ""weight"": 2 }
      ],
      ""spawn"": {
        ""models"": [ ""g_m_y_mexgoon_01"", ""g_m_y_mexgoon_02"", ""g_m_y_mexgang_01"" ],
        ""vehicles"": [ ""vamos"", ""chino"", ""tornado"" ],
        ""maxAlive"": 12, ""perWave"": 3, ""waveIntervalMs"": 16000, ""inVehicleChance"": 0.35, ""occupants"": 3
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Yellow"" }
    },

    ""lost"": {
      ""name"": ""The Lost MC"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 2,
      ""armedChance"": 1.0, ""ammo"": 250, ""armour"": 50, ""accuracy"": 35,
      ""weapons"": [
        { ""name"": ""SawnOffShotgun"", ""weight"": 3 }, { ""name"": ""Machete"", ""weight"": 2 },
        { ""name"": ""AssaultRifle"", ""weight"": 2 }, { ""name"": ""Molotov"", ""weight"": 1 }
      ],
      ""spawn"": {
        ""models"": [ ""g_m_y_lost_01"", ""g_m_y_lost_02"", ""g_m_y_lost_03"" ],
        ""vehicles"": [ ""daemon"", ""hexer"", ""gburrito2"" ],
        ""maxAlive"": 12, ""perWave"": 3, ""waveIntervalMs"": 18000, ""inVehicleChance"": 0.5, ""occupants"": 2
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Orange"" }
    },

    ""civilians"": {
      ""name"": ""Caught In It"", ""reaction"": ""Mixed"", ""fightBackChance"": 0.1,
      ""recruits"": ""civilian"", ""share"": 1, ""armedChance"": 0.15, ""ammo"": 40,
      ""weapons"": [ ""Bottle"", ""Bat"" ],
      ""blip"": { ""enabled"": false }
    }
  },

  ""relations"": [
    { ""from"": ""ballas"",   ""to"": ""families"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""ballas"",   ""to"": ""vagos"",    ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""ballas"",   ""to"": ""lost"",     ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""families"", ""to"": ""vagos"",    ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""families"", ""to"": ""lost"",     ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""vagos"",    ""to"": ""lost"",     ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""ballas"",   ""to"": ""civilians"", ""value"": ""dislike"", ""mutual"": false },
    { ""from"": ""families"", ""to"": ""civilians"", ""value"": ""dislike"", ""mutual"": false },
    { ""from"": ""vagos"",    ""to"": ""civilians"", ""value"": ""dislike"", ""mutual"": false },
    { ""from"": ""lost"",     ""to"": ""civilians"", ""value"": ""dislike"", ""mutual"": false }
  ],

  ""config"": { ""combat"": { ""accuracy"": 25 }, ""riot"": { ""conversionChance"": 0.4 } }
}
";

        private const string Police = @"{
  ""_stock"": true,
  ""name"": ""Police State"",
  ""description"": ""Sirens on, lights running, and nobody is walking away from a stop."",
  ""enabled"": true,
  ""playerRelationship"": ""hate"",

  ""ambience"": { ""weather"": ""OVERCAST"", ""timecycle"": ""hud_def_blue"", ""timecycleStrength"": 0.3 },

  ""escalation"": {
    ""phases"": [
      { ""name"": ""Heavy Patrol"", ""durationSeconds"": 90 },
      { ""name"": ""Crackdown"", ""durationSeconds"": 150, ""fires"": true, ""killThreshold"": 12 },
      { ""name"": ""No Restraint"", ""durationSeconds"": 0, ""fires"": true }
    ]
  },

  ""factions"": {
    ""patrol"": {
      ""name"": ""LSPD Patrol"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1,
      ""armedChance"": 1.0, ""ammo"": 250, ""armour"": 50, ""accuracy"": 40,
      ""weapons"": [
        { ""name"": ""Pistol"", ""weight"": 4 }, { ""name"": ""Nightstick"", ""weight"": 2 },
        { ""name"": ""PumpShotgun"", ""weight"": 2 }, { ""name"": ""CarbineRifle"", ""weight"": 1 }
      ],
      ""spawn"": {
        ""models"": [ ""s_m_y_cop_01"", ""s_f_y_cop_01"", ""s_m_y_hwaycop_01"" ],
        ""vehicles"": [ ""police"", ""police2"", ""police3"" ],
        ""maxAlive"": 16, ""perWave"": 2, ""waveIntervalMs"": 11000,
        ""inVehicleChance"": 0.8, ""occupants"": 2,
        ""siren"": true, ""driveThroughCrowds"": true
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""PoliceOfficer"", ""colour"": ""Blue"" }
    },

    ""noose"": {
      ""name"": ""NOOSE"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 1,
      ""armedChance"": 1.0, ""ammo"": 300, ""armour"": 100, ""accuracy"": 55, ""health"": 200,
      ""weapons"": [
        { ""name"": ""CarbineRifle"", ""weight"": 3 }, { ""name"": ""PumpShotgun"", ""weight"": 2 },
        { ""name"": ""SMG"", ""weight"": 2 }, { ""name"": ""SmokeGrenade"", ""weight"": 1 }
      ],
      ""spawn"": {
        ""models"": [ ""s_m_y_swat_01"", ""s_m_y_blackops_01"" ],
        ""vehicles"": [ ""riot"", ""fbi2"", ""police4"" ],
        ""maxAlive"": 12, ""perWave"": 3, ""waveIntervalMs"": 16000,
        ""inVehicleChance"": 0.9, ""occupants"": 4,
        ""siren"": true, ""driveThroughCrowds"": true
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""BlueDark"" }
    },

    ""public"": {
      ""name"": ""Everyone Else"", ""reaction"": ""Mixed"", ""fightBackChance"": 0.25,
      ""recruits"": ""civilian"", ""share"": 1, ""armedChance"": 0.25, ""ammo"": 60,
      ""weapons"": [
        { ""name"": ""Bat"", ""weight"": 3 }, { ""name"": ""Bottle"", ""weight"": 3 },
        { ""name"": ""Crowbar"", ""weight"": 2 }, { ""name"": ""Pistol"", ""weight"": 1 }
      ],
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""White"" }
    }
  },

  ""relations"": [
    { ""from"": ""patrol"", ""to"": ""public"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""noose"",  ""to"": ""public"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""patrol"", ""to"": ""noose"",  ""value"": ""companion"", ""mutual"": true }
  ],

  ""config"": { ""riot"": { ""conversionChance"": 0.5 } }
}
";

        private const string Military = @"{
  ""_stock"": true,
  ""name"": ""Martial Law"",
  ""description"": ""The army is deployed to the streets of Los Santos. There is no negotiation."",
  ""enabled"": true,
  ""playerRelationship"": ""hate"",

  ""ambience"": { ""weather"": ""SMOG"", ""hour"": 6, ""timecycle"": ""hud_def_desat_Neutral"", ""timecycleStrength"": 0.5 },

  ""escalation"": {
    ""phases"": [
      { ""name"": ""Deployment"", ""durationSeconds"": 90 },
      { ""name"": ""Armour Moves In"", ""durationSeconds"": 180, ""fires"": true, ""killThreshold"": 15 },
      { ""name"": ""Scorched Earth"", ""durationSeconds"": 0, ""fires"": true }
    ]
  },

  ""factions"": {
    ""infantry"": {
      ""name"": ""Infantry"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1,
      ""armedChance"": 1.0, ""ammo"": 400, ""armour"": 100, ""accuracy"": 50, ""health"": 200,
      ""weapons"": [
        { ""name"": ""CarbineRifle"", ""weight"": 4 }, { ""name"": ""SpecialCarbine"", ""weight"": 2 },
        { ""name"": ""CombatMG"", ""weight"": 1 }, { ""name"": ""SmokeGrenade"", ""weight"": 1 }
      ],
      ""spawn"": {
        ""models"": [ ""s_m_y_marine_01"", ""s_m_y_marine_02"", ""s_m_y_marine_03"", ""s_m_m_marine_01"" ],
        ""vehicles"": [ ""barracks"", ""crusader"" ],
        ""maxAlive"": 18, ""perWave"": 4, ""waveIntervalMs"": 13000,
        ""inVehicleChance"": 0.6, ""occupants"": 4, ""driveThroughCrowds"": true
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Green"" }
    },

    ""armour"": {
      ""name"": ""Armoured Column"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 1,
      ""armedChance"": 1.0, ""ammo"": 500, ""armour"": 100, ""accuracy"": 60, ""health"": 300,
      ""weapons"": [ { ""name"": ""CombatMG"", ""weight"": 3 }, { ""name"": ""RPG"", ""weight"": 1 } ],
      ""spawn"": {
        ""models"": [ ""s_m_y_blackops_01"", ""s_m_y_blackops_02"", ""s_m_m_armoured_01"" ],
        ""vehicles"": [ ""insurgent"", ""barracks2"", ""crusader"" ],
        ""maxAlive"": 10, ""perWave"": 3, ""waveIntervalMs"": 20000,
        ""inVehicleChance"": 1.0, ""occupants"": 4, ""driveThroughCrowds"": true
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""GreenDark"" }
    },

    ""civilians"": {
      ""name"": ""Civilians"", ""reaction"": ""Mixed"", ""fightBackChance"": 0.2,
      ""recruits"": ""civilian"", ""share"": 1, ""armedChance"": 0.3, ""ammo"": 90,
      ""weapons"": [
        { ""name"": ""Pistol"", ""weight"": 3 }, { ""name"": ""Bat"", ""weight"": 3 },
        { ""name"": ""PumpShotgun"", ""weight"": 1 }, { ""name"": ""Molotov"", ""weight"": 1 }
      ],
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""White"" }
    }
  },

  ""relations"": [
    { ""from"": ""infantry"", ""to"": ""civilians"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""armour"",   ""to"": ""civilians"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""infantry"", ""to"": ""armour"",    ""value"": ""companion"", ""mutual"": true }
  ],

  ""config"": { ""riot"": { ""conversionChance"": 0.5 } }
}
";

        private const string Animals = @"{
  ""_stock"": true,
  ""name"": ""Animal Uprising"",
  ""description"": ""The wildlife has had enough. Deeply silly, and that is the point."",
  ""enabled"": true,
  ""playerRelationship"": ""hate"",

  ""ambience"": { ""weather"": ""FOGGY"", ""hour"": 5, ""timecycle"": ""CAMERA_secuirity"", ""timecycleStrength"": 0.3 },

  ""escalation"": {
    ""phases"": [
      { ""name"": ""Something In The Bushes"", ""durationSeconds"": 60 },
      { ""name"": ""The Pack"", ""durationSeconds"": 150, ""killThreshold"": 8 },
      { ""name"": ""Nature Wins"", ""durationSeconds"": 0 }
    ]
  },

  ""factions"": {
    ""predators"": {
      ""name"": ""Predators"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1,
      ""armedChance"": 0, ""accuracy"": 100, ""health"": 200,
      ""weapons"": [],
      ""spawn"": {
        ""models"": [ ""a_c_coyote"", ""a_c_mtlion"", ""a_c_rottweiler"", ""a_c_husky"", ""a_c_shepherd"" ],
        ""maxAlive"": 14, ""perWave"": 3, ""waveIntervalMs"": 12000, ""minDistance"": 40, ""maxDistance"": 110
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""RedDark"" }
    },

    ""herd"": {
      ""name"": ""The Herd"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 1,
      ""armedChance"": 0, ""accuracy"": 100, ""health"": 250,
      ""weapons"": [],
      ""spawn"": {
        ""models"": [ ""a_c_boar"", ""a_c_deer"", ""a_c_chimp"", ""a_c_pig"", ""a_c_cow"" ],
        ""maxAlive"": 12, ""perWave"": 3, ""waveIntervalMs"": 15000, ""minDistance"": 40, ""maxDistance"": 110
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Orange"" }
    },

    ""people"": {
      ""name"": ""People"", ""reaction"": ""Mixed"", ""fightBackChance"": 0.3,
      ""recruits"": ""civilian"", ""share"": 1, ""armedChance"": 0.35, ""ammo"": 80,
      ""weapons"": [
        { ""name"": ""Bat"", ""weight"": 3 }, { ""name"": ""Pistol"", ""weight"": 2 },
        { ""name"": ""PumpShotgun"", ""weight"": 2 }, { ""name"": ""Machete"", ""weight"": 1 }
      ],
      ""blip"": { ""enabled"": false }
    }
  },

  ""relations"": [
    { ""from"": ""predators"", ""to"": ""people"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""herd"",      ""to"": ""people"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""predators"", ""to"": ""herd"",   ""value"": ""like"", ""mutual"": true }
  ],

  ""config"": { ""riot"": { ""conversionChance"": 0.45 }, ""combat"": { ""accuracy"": 40 } }
}
";

        private const string Aliens = @"{
  ""_stock"": true,
  ""name"": ""Invasion"",
  ""description"": ""Green men, ray guns, and a city that was not ready. Uses only content already in your game."",
  ""enabled"": true,
  ""playerRelationship"": ""hate"",

  ""ambience"": { ""weather"": ""FOGGY"", ""hour"": 1, ""timecycle"": ""spectator5"", ""timecycleStrength"": 0.7 },

  ""escalation"": {
    ""phases"": [
      { ""name"": ""First Contact"", ""durationSeconds"": 75 },
      { ""name"": ""Harvest"", ""durationSeconds"": 180, ""fires"": true, ""killThreshold"": 15 },
      { ""name"": ""Extermination"", ""durationSeconds"": 0, ""fires"": true }
    ]
  },

  ""factions"": {
    ""scouts"": {
      ""name"": ""Scouts"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1,
      ""armedChance"": 1.0, ""ammo"": 500, ""armour"": 60, ""accuracy"": 45, ""health"": 200,
      ""weapons"": [
        { ""name"": ""WEAPON_RAYPISTOL"", ""weight"": 4 },
        { ""name"": ""UnholyHellbringer"", ""weight"": 2 }
      ],
      ""spawn"": {
        ""models"": [ ""s_m_m_movalien_01"" ],
        ""maxAlive"": 14, ""perWave"": 3, ""waveIntervalMs"": 13000, ""minDistance"": 50, ""maxDistance"": 120
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Green"" }
    },

    ""hunters"": {
      ""name"": ""Hunters"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 1,
      ""armedChance"": 1.0, ""ammo"": 800, ""armour"": 100, ""accuracy"": 60, ""health"": 350,
      ""weapons"": [
        { ""name"": ""Widowmaker"", ""weight"": 3 },
        { ""name"": ""UnholyHellbringer"", ""weight"": 2 },
        { ""name"": ""Railgun"", ""weight"": 1 }
      ],
      ""spawn"": {
        ""models"": [ ""s_m_m_movalien_01"" ],
        ""maxAlive"": 8, ""perWave"": 2, ""waveIntervalMs"": 20000, ""minDistance"": 50, ""maxDistance"": 120
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""GreenDark"" }
    },

    ""resistance"": {
      ""name"": ""Resistance"", ""reaction"": ""Mixed"", ""fightBackChance"": 0.4,
      ""recruits"": ""civilian"", ""share"": 1, ""armedChance"": 0.45, ""ammo"": 120,
      ""weapons"": [
        { ""name"": ""Pistol"", ""weight"": 3 }, { ""name"": ""PumpShotgun"", ""weight"": 2 },
        { ""name"": ""CarbineRifle"", ""weight"": 1 }, { ""name"": ""Molotov"", ""weight"": 1 }
      ],
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""White"" }
    }
  },

  ""relations"": [
    { ""from"": ""scouts"",  ""to"": ""resistance"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""hunters"", ""to"": ""resistance"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""scouts"",  ""to"": ""hunters"",    ""value"": ""companion"", ""mutual"": true }
  ],

  ""config"": { ""riot"": { ""conversionChance"": 0.5 } }
}
";

        private const string Purge = @"{
  ""_stock"": true,
  ""name"": ""The Purge"",
  ""description"": ""Twelve minutes. All crime legal. Emergency services suspended. Then it stops."",
  ""enabled"": true,
  ""playerRelationship"": ""hate"",

  ""ambience"": { ""weather"": ""CLEARING"", ""hour"": 22, ""timecycle"": ""Bloom"", ""timecycleStrength"": 0.35 },

  ""purge"": { ""enabled"": true, ""startHour"": 22, ""startMinute"": 0, ""durationMinutes"": 12, ""announce"": true },

  ""escalation"": {
    ""phases"": [
      { ""name"": ""The Siren"", ""durationSeconds"": 60 },
      { ""name"": ""Anything Goes"", ""durationSeconds"": 240, ""fires"": true, ""looting"": true, ""killThreshold"": 15 },
      { ""name"": ""Last Hour"", ""durationSeconds"": 0, ""fires"": true, ""looting"": true }
    ]
  },

  ""factions"": {
    ""purgers"": {
      ""name"": ""Purgers"", ""reaction"": ""Fight"", ""recruits"": ""civilian"", ""share"": 1,
      ""armedChance"": 0.9, ""ammo"": 150, ""armour"": 20,
      ""weapons"": [
        { ""name"": ""Machete"", ""weight"": 4 }, { ""name"": ""Bat"", ""weight"": 3 },
        { ""name"": ""Hammer"", ""weight"": 3 }, { ""name"": ""Knife"", ""weight"": 3 },
        { ""name"": ""SawnOffShotgun"", ""weight"": 2 }, { ""name"": ""Pistol"", ""weight"": 2 },
        { ""name"": ""Molotov"", ""weight"": 1 }
      ],
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Red"" }
    },

    ""gangs"": {
      ""name"": ""Opportunists"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 1,
      ""armedChance"": 1.0, ""ammo"": 250, ""armour"": 50, ""accuracy"": 35,
      ""weapons"": [
        { ""name"": ""MicroSMG"", ""weight"": 3 }, { ""name"": ""PumpShotgun"", ""weight"": 2 },
        { ""name"": ""AssaultRifle"", ""weight"": 2 }, { ""name"": ""Molotov"", ""weight"": 1 }
      ],
      ""spawn"": {
        ""models"": [ ""g_m_y_lost_01"", ""g_m_y_ballasout_01"", ""g_m_y_mexgoon_01"", ""g_m_y_famca_01"" ],
        ""vehicles"": [ ""gburrito2"", ""manana"", ""chino"" ],
        ""maxAlive"": 14, ""perWave"": 3, ""waveIntervalMs"": 15000, ""inVehicleChance"": 0.4, ""occupants"": 3
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Orange"" }
    },

    ""survivors"": {
      ""name"": ""Survivors"", ""reaction"": ""Mixed"", ""fightBackChance"": 0.35,
      ""recruits"": ""civilian"", ""share"": 1, ""armedChance"": 0.4, ""ammo"": 90,
      ""weapons"": [
        { ""name"": ""Pistol"", ""weight"": 3 }, { ""name"": ""PumpShotgun"", ""weight"": 2 },
        { ""name"": ""Bat"", ""weight"": 2 }, { ""name"": ""Knife"", ""weight"": 1 }
      ],
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""White"" }
    }
  },

  ""relations"": [
    { ""from"": ""purgers"", ""to"": ""survivors"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""gangs"",   ""to"": ""survivors"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""purgers"", ""to"": ""gangs"",     ""value"": ""hate"", ""mutual"": true }
  ],

  ""config"": { ""riot"": { ""conversionChance"": 0.6 }, ""combat"": { ""accuracy"": 20 } }
}
";

        private const string Everything = @"{
  ""_stock"": true,
  ""name"": ""Everything"",
  ""description"": ""Every faction at once. Needs a gameconfig and Heap Adjuster. Do not say you were not told."",
  ""enabled"": true,
  ""playerRelationship"": ""hate"",

  ""ambience"": { ""weather"": ""THUNDER"", ""hour"": 23, ""timecycle"": ""spectator5"", ""timecycleStrength"": 0.4 },

  ""escalation"": {
    ""phases"": [
      { ""name"": ""It Starts"", ""durationSeconds"": 60 },
      { ""name"": ""Response"", ""durationSeconds"": 120, ""fires"": true, ""killThreshold"": 15 },
      { ""name"": ""Lockdown"", ""durationSeconds"": 150, ""fires"": true, ""looting"": true, ""killThreshold"": 40 },
      { ""name"": ""Something Else Arrives"", ""durationSeconds"": 0, ""fires"": true }
    ]
  },

  ""factions"": {
    ""rioters"": {
      ""name"": ""Rioters"", ""reaction"": ""Fight"", ""recruits"": ""civilian"", ""share"": 1,
      ""armedChance"": 0.85, ""ammo"": 150,
      ""weapons"": [
        { ""name"": ""Bat"", ""weight"": 4 }, { ""name"": ""Machete"", ""weight"": 3 },
        { ""name"": ""Crowbar"", ""weight"": 3 }, { ""name"": ""Pistol"", ""weight"": 2 },
        { ""name"": ""Molotov"", ""weight"": 1 }
      ],
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Red"" }
    },

    ""gangs"": {
      ""name"": ""Gangs"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1,
      ""armedChance"": 1.0, ""ammo"": 250, ""armour"": 30, ""accuracy"": 30,
      ""weapons"": [ { ""name"": ""MicroSMG"", ""weight"": 3 }, { ""name"": ""AssaultRifle"", ""weight"": 2 }, { ""name"": ""PumpShotgun"", ""weight"": 2 } ],
      ""spawn"": {
        ""models"": [ ""g_m_y_ballasout_01"", ""g_m_y_famca_01"", ""g_m_y_mexgoon_01"", ""g_m_y_lost_01"" ],
        ""vehicles"": [ ""manana"", ""peyote"", ""gburrito2"" ],
        ""maxAlive"": 10, ""perWave"": 2, ""waveIntervalMs"": 18000, ""inVehicleChance"": 0.3, ""occupants"": 3
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Purple"" }
    },

    ""police"": {
      ""name"": ""Police"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 1,
      ""armedChance"": 1.0, ""ammo"": 300, ""armour"": 60, ""accuracy"": 40,
      ""weapons"": [ { ""name"": ""Pistol"", ""weight"": 3 }, { ""name"": ""PumpShotgun"", ""weight"": 2 }, { ""name"": ""CarbineRifle"", ""weight"": 2 } ],
      ""spawn"": {
        ""models"": [ ""s_m_y_cop_01"", ""s_f_y_cop_01"", ""s_m_y_swat_01"" ],
        ""vehicles"": [ ""police"", ""police2"", ""riot"" ],
        ""maxAlive"": 12, ""perWave"": 2, ""waveIntervalMs"": 14000,
        ""inVehicleChance"": 0.8, ""occupants"": 2, ""siren"": true, ""driveThroughCrowds"": true
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""PoliceOfficer"", ""colour"": ""Blue"" }
    },

    ""military"": {
      ""name"": ""Military"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 2,
      ""armedChance"": 1.0, ""ammo"": 400, ""armour"": 100, ""accuracy"": 50, ""health"": 200,
      ""weapons"": [ { ""name"": ""CarbineRifle"", ""weight"": 3 }, { ""name"": ""CombatMG"", ""weight"": 1 } ],
      ""spawn"": {
        ""models"": [ ""s_m_y_marine_01"", ""s_m_y_marine_02"", ""s_m_y_blackops_01"" ],
        ""vehicles"": [ ""barracks"", ""crusader"", ""insurgent"" ],
        ""maxAlive"": 12, ""perWave"": 3, ""waveIntervalMs"": 18000,
        ""inVehicleChance"": 0.7, ""occupants"": 4, ""driveThroughCrowds"": true
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Green"" }
    },

    ""aliens"": {
      ""name"": ""Aliens"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 3,
      ""armedChance"": 1.0, ""ammo"": 600, ""armour"": 100, ""accuracy"": 55, ""health"": 250,
      ""weapons"": [ { ""name"": ""WEAPON_RAYPISTOL"", ""weight"": 3 }, { ""name"": ""UnholyHellbringer"", ""weight"": 2 }, { ""name"": ""Widowmaker"", ""weight"": 1 } ],
      ""spawn"": {
        ""models"": [ ""s_m_m_movalien_01"" ],
        ""maxAlive"": 10, ""perWave"": 3, ""waveIntervalMs"": 16000
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""GreenDark"" }
    },

    ""animals"": {
      ""name"": ""Animals"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 2,
      ""armedChance"": 0, ""accuracy"": 100, ""health"": 200,
      ""weapons"": [],
      ""spawn"": {
        ""models"": [ ""a_c_coyote"", ""a_c_mtlion"", ""a_c_rottweiler"", ""a_c_boar"" ],
        ""maxAlive"": 8, ""perWave"": 2, ""waveIntervalMs"": 20000
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Orange"" }
    }
  },

  ""relations"": [
    { ""from"": ""rioters"",  ""to"": ""gangs"",    ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""rioters"",  ""to"": ""police"",   ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""rioters"",  ""to"": ""military"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""gangs"",    ""to"": ""police"",   ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""gangs"",    ""to"": ""military"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""police"",   ""to"": ""military"", ""value"": ""like"", ""mutual"": true },
    { ""from"": ""aliens"",   ""to"": ""rioters"",  ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""aliens"",   ""to"": ""gangs"",    ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""aliens"",   ""to"": ""police"",   ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""aliens"",   ""to"": ""military"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""animals"",  ""to"": ""rioters"",  ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""animals"",  ""to"": ""police"",   ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""animals"",  ""to"": ""military"", ""value"": ""hate"", ""mutual"": true }
  ],

  ""config"": {
    ""riot"": { ""conversionChance"": 0.6 },
    ""engine"": { ""maxTrackedPeds"": 150 },
    ""combat"": { ""accuracy"": 30 }
  }
}
";

    }
}
