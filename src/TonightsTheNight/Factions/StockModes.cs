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
                { "night", Night },
                { "chaos", Chaos },
                { "chase", Chase },
                { "criminals", Criminals },
                { "police", Police },
                { "military", Military },
                { "animals", Animals },
                { "aliens", Aliens },
                { "contagion", Contagion },
                { "purge", Purge },
                { "everything", Everything }
            };
        }

        private const string Pedestrians = @"{
  // Stock mode - remove the _stock line below to stop updates overwriting your edits.
  ""_stock"": true,

  ""name"": ""Pedestrian Riot"",
  ""order"": 10,
  // Two mobs out of one crowd is the whole mode, so pedestrians fighting pedestrians is
  // intended here. Saying so keeps the build from treating it as the Martial Law bug, where
  // the crowd turning on itself instead of on the army was exactly what went wrong.
  ""crowdFightsItself"": true,
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
  ""order"": 30,
  ""description"": ""Every gang in Los Santos settles up at once. Civilians are in the way."",
  ""enabled"": true,
  ""playerRelationship"": ""hate"",


  ""escalation"": {
    ""phases"": [
      { ""name"": ""Turf Dispute"", ""durationSeconds"": 90 },
      { ""name"": ""Open War"", ""durationSeconds"": 180, ""fires"": true, ""barricades"": true, ""killThreshold"": 20 },
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
        ""maxAlive"": 20, ""perWave"": 5, ""waveIntervalMs"": 10000, ""minDistance"": 140, ""maxDistance"": 260, ""inVehicleChance"": 0.8, ""occupants"": 4, ""vehiclesPerWave"": 2
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
        ""maxAlive"": 20, ""perWave"": 5, ""waveIntervalMs"": 10000, ""minDistance"": 140, ""maxDistance"": 260, ""inVehicleChance"": 0.8, ""occupants"": 4, ""vehiclesPerWave"": 2
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
        ""maxAlive"": 18, ""perWave"": 5, ""waveIntervalMs"": 12000, ""minDistance"": 140, ""maxDistance"": 260, ""inVehicleChance"": 0.8, ""occupants"": 4, ""vehiclesPerWave"": 2
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
        ""maxAlive"": 12, ""perWave"": 3, ""waveIntervalMs"": 18000, ""minDistance"": 140, ""maxDistance"": 260, ""inVehicleChance"": 0.8, ""occupants"": 2
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
  ""order"": 40,
  ""description"": ""Sirens on, lights running, and nobody is walking away from a stop. The public is not your enemy here."",
  ""enabled"": true,

  // Mode-wide default. Individual factions override it below - which is the whole point:
  // the police want you, the people being shot at do not.
  ""playerRelationship"": ""hate"",

  ""escalation"": {
    ""phases"": [
      { ""name"": ""Heavy Patrol"", ""durationSeconds"": 90 },
      { ""name"": ""Crackdown"", ""durationSeconds"": 150, ""fires"": true, ""looting"": true, ""barricades"": true, ""killThreshold"": 12 },
      { ""name"": ""No Restraint"", ""durationSeconds"": 0, ""fires"": true, ""looting"": true, ""barricades"": true }
    ]
  },

  ""factions"": {
    ""patrol"": {
      ""name"": ""LSPD Patrol"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1,
      ""playerRelationship"": ""hate"",
      ""armedChance"": 1.0, ""ammo"": 250, ""armour"": 50, ""accuracy"": 40,
      ""weapons"": [
        { ""name"": ""Pistol"", ""weight"": 4 }, { ""name"": ""Nightstick"", ""weight"": 2 },
        { ""name"": ""PumpShotgun"", ""weight"": 2 }, { ""name"": ""CarbineRifle"", ""weight"": 1 }
      ],
      ""spawn"": {
        ""models"": [ ""s_m_y_cop_01"", ""s_f_y_cop_01"", ""s_m_y_hwaycop_01"" ],
        ""vehicles"": [ ""police"", ""police2"", ""police3"", ""sheriff"" ],
        // Two cars of four every eleven seconds, up to eighteen on the street. A couple of
        // officers trickling in does not read as a police state; twenty-six all pointed at one
        // person is not one either.
        ""maxAlive"": 18, ""perWave"": 4, ""waveIntervalMs"": 11000,
        ""minDistance"": 140, ""maxDistance"": 260, ""inVehicleChance"": 1, ""occupants"": 4, ""vehiclesPerWave"": 2,
        ""siren"": true, ""driveThroughCrowds"": true
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""PoliceOfficer"", ""colour"": ""Blue"" }
    },

    ""riotsquad"": {
      ""name"": ""NOOSE"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 1,
      ""playerRelationship"": ""hate"",
      ""armedChance"": 1.0, ""ammo"": 300, ""armour"": 100, ""accuracy"": 55, ""health"": 200,
      ""weapons"": [
        { ""name"": ""CarbineRifle"", ""weight"": 3 }, { ""name"": ""PumpShotgun"", ""weight"": 2 },
        { ""name"": ""SMG"", ""weight"": 2 }, { ""name"": ""SmokeGrenade"", ""weight"": 1 }
      ],
      ""spawn"": {
        ""models"": [ ""s_m_y_swat_01"", ""s_m_y_blackops_01"" ],
        // A riot van arrives with a van's worth of people in it.
        ""vehicles"": [ ""riot"", ""fbi2"", ""police4"" ],
        ""maxAlive"": 12, ""perWave"": 6, ""waveIntervalMs"": 18000,
        ""minDistance"": 140, ""maxDistance"": 260, ""inVehicleChance"": 1, ""occupants"": 6, ""vehiclesPerWave"": 1,
        ""siren"": true, ""driveThroughCrowds"": true
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""BlueDark"" }
    },

    ""airsupport"": {
      ""name"": ""Air Support"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 2,
      ""playerRelationship"": ""hate"",
      ""armedChance"": 1.0, ""ammo"": 400, ""armour"": 100, ""accuracy"": 45,
      ""weapons"": [ { ""name"": ""CarbineRifle"", ""weight"": 3 }, { ""name"": ""SMG"", ""weight"": 1 } ],
      ""spawn"": {
        ""models"": [ ""s_m_y_swat_01"", ""s_m_y_cop_01"" ],
        ""vehicles"": [ ""polmav"" ],
        ""vehicleType"": ""air"", ""flightHeight"": 55,
        ""maxAlive"": 4, ""perWave"": 2, ""waveIntervalMs"": 45000,
        ""inVehicleChance"": 1.0, ""occupants"": 2, ""vehiclesPerWave"": 1,
        ""minDistance"": 80, ""maxDistance"": 140, ""siren"": true
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""BlueLight"" }
    },

    ""public"": {
      ""name"": ""The Public"", ""reaction"": ""Mixed"", ""fightBackChance"": 0.45,
      ""recruits"": ""civilian"", ""share"": 3,
      // Not your enemy. They are being shot at by the same people you are.
      ""playerRelationship"": ""neutral"",
      ""armedChance"": 0.4, ""ammo"": 80,
      ""weapons"": [
        { ""name"": ""Bat"", ""weight"": 3 }, { ""name"": ""Bottle"", ""weight"": 3 },
        { ""name"": ""Crowbar"", ""weight"": 2 }, { ""name"": ""Molotov"", ""weight"": 2 },
        { ""name"": ""Pistol"", ""weight"": 1 }
      ],
      ""blip"": { ""enabled"": false }
    }
  },

  // The public is one crowd against the police, not two mobs. Nothing here makes them
  // hostile to each other, so they are not.
  ""relations"": [
    { ""from"": ""patrol"",     ""to"": ""public"",     ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""riotsquad"",  ""to"": ""public"",     ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""airsupport"", ""to"": ""public"",     ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""patrol"",     ""to"": ""riotsquad"",  ""value"": ""companion"", ""mutual"": true },
    { ""from"": ""patrol"",     ""to"": ""airsupport"", ""value"": ""companion"", ""mutual"": true },
    { ""from"": ""riotsquad"",  ""to"": ""airsupport"", ""value"": ""companion"", ""mutual"": true }
  ],

  ""config"": { ""riot"": { ""conversionChance"": 0.6 } }
}
";

        private const string Military = @"{
  ""_stock"": true,
  ""name"": ""Martial Law"",
  ""order"": 50,
  ""description"": ""The army takes Los Santos. Police hold the line, armour rolls in, helicopters hold station overhead."",
  ""enabled"": true,
  ""playerRelationship"": ""hate"",

  ""escalation"": {
    ""phases"": [
      { ""name"": ""Curfew"", ""durationSeconds"": 90 },
      { ""name"": ""Deployment"", ""durationSeconds"": 180, ""fires"": true, ""barricades"": true, ""killThreshold"": 15 },
      { ""name"": ""Full Control"", ""durationSeconds"": 0, ""fires"": true, ""looting"": true, ""barricades"": true, ""blackout"": true }
    ]
  },

  ""factions"": {
    // The police are here from the start - they are the ones who call the army in.
    ""police"": {
      ""name"": ""LSPD"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1,
      ""playerRelationship"": ""hate"",
      ""armedChance"": 1.0, ""ammo"": 250, ""armour"": 50, ""accuracy"": 40,
      ""weapons"": [
        { ""name"": ""Pistol"", ""weight"": 3 }, { ""name"": ""PumpShotgun"", ""weight"": 2 },
        { ""name"": ""CarbineRifle"", ""weight"": 2 }, { ""name"": ""Nightstick"", ""weight"": 1 }
      ],
      ""spawn"": {
        ""models"": [ ""s_m_y_cop_01"", ""s_f_y_cop_01"", ""s_m_y_swat_01"" ],
        ""vehicles"": [ ""police"", ""police3"", ""riot"", ""sheriff"" ],
        ""maxAlive"": 18, ""perWave"": 4, ""waveIntervalMs"": 10000,
        ""minDistance"": 140, ""maxDistance"": 260, ""inVehicleChance"": 1, ""occupants"": 4, ""vehiclesPerWave"": 2,
        ""siren"": true, ""driveThroughCrowds"": true
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""PoliceOfficer"", ""colour"": ""Blue"" }
    },

    ""infantry"": {
      ""name"": ""Infantry"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1,
      ""playerRelationship"": ""hate"",
      ""armedChance"": 1.0, ""ammo"": 400, ""armour"": 100, ""accuracy"": 50, ""health"": 200,
      ""weapons"": [
        { ""name"": ""CarbineRifle"", ""weight"": 4 }, { ""name"": ""SpecialCarbine"", ""weight"": 2 },
        { ""name"": ""CombatMG"", ""weight"": 1 }, { ""name"": ""SmokeGrenade"", ""weight"": 1 }
      ],
      ""spawn"": {
        // s_m_y_marine_03 is deliberately absent. It is the marine in a white t-shirt, and
        // because the spawner used to take the first name it could resolve rather than a
        // random one, a list containing it produced an entire army of men in white t-shirts.
        ""models"": [ ""s_m_y_marine_01"", ""s_m_y_marine_02"", ""s_m_m_marine_01"", ""s_m_m_marine_02"" ],
        // A troop carrier arrives carrying troops. Two of them, six each, every ten seconds.
        ""vehicles"": [ ""barracks"", ""barracks2"", ""crusader"" ],
        ""maxAlive"": 28, ""perWave"": 6, ""waveIntervalMs"": 10000,
        ""minDistance"": 140, ""maxDistance"": 260, ""inVehicleChance"": 1, ""occupants"": 6, ""vehiclesPerWave"": 2,
        ""driveThroughCrowds"": true
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Green"" }
    },

    ""armour"": {
      ""name"": ""Armoured Column"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 1,
      ""playerRelationship"": ""hate"",
      ""armedChance"": 1.0, ""ammo"": 500, ""armour"": 100, ""accuracy"": 50, ""health"": 250,
      // A weight is a share of the faction, not a rarity. RPG at 1 against 3 armed a quarter of
      // the column with rocket launchers, which by the final phase is a street nothing survives.
      // It is rare here and the global combat.heavyWeaponChance thins it again on top.
      ""weapons"": [
        { ""name"": ""CarbineRifle"", ""weight"": 5 }, { ""name"": ""CombatMG"", ""weight"": 2 },
        { ""name"": ""RPG"", ""weight"": 0.5 }
      ],
      ""spawn"": {
        ""models"": [ ""s_m_y_marine_02"", ""s_m_y_blackops_01"", ""s_m_m_armoured_01"" ],
        // No rhino. Armour arriving is a column of troop carriers and technicals; a tank is a
        // separate faction below with a cap of two, because a tank should be an event.
        ""vehicles"": [ ""insurgent"", ""insurgent2"", ""barracks2"", ""crusader"" ],
        ""maxAlive"": 12, ""perWave"": 4, ""waveIntervalMs"": 20000,
        ""minDistance"": 140, ""maxDistance"": 260, ""inVehicleChance"": 1, ""occupants"": 4, ""vehiclesPerWave"": 1,
        ""driveThroughCrowds"": true
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""GreenDark"" }
    },

    // Two of them, ninety seconds apart, from the last phase only. Every previous version of
    // this mode put the rhino in the armoured column's vehicle list, and since the spawner took
    // the first name it could resolve, every armoured wave was two tanks - which is why the
    // final phase was survivable only with invincibility on.
    ""tanks"": {
      ""name"": ""Armour"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 2,
      ""playerRelationship"": ""hate"",
      ""armedChance"": 1.0, ""ammo"": 300, ""armour"": 100, ""accuracy"": 45, ""health"": 200,
      ""weapons"": [ { ""name"": ""CarbineRifle"", ""weight"": 1 } ],
      ""spawn"": {
        ""models"": [ ""s_m_y_marine_02"", ""s_m_m_armoured_01"" ],
        ""vehicles"": [ ""rhino"" ],
        ""maxAlive"": 2, ""perWave"": 2, ""waveIntervalMs"": 90000,
        ""minDistance"": 180, ""maxDistance"": 300, ""inVehicleChance"": 1, ""occupants"": 2, ""vehiclesPerWave"": 1,
        ""driveThroughCrowds"": true
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""GreenDark"" }
    },

    ""airsupport"": {
      ""name"": ""Air Support"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 1,
      ""playerRelationship"": ""hate"",
      ""armedChance"": 1.0, ""ammo"": 600, ""armour"": 100, ""accuracy"": 50, ""health"": 200,
      ""weapons"": [ { ""name"": ""CombatMG"", ""weight"": 3 }, { ""name"": ""CarbineRifle"", ""weight"": 2 } ],
      ""spawn"": {
        ""models"": [ ""s_m_y_marine_01"", ""s_m_y_blackops_01"" ],
        ""vehicles"": [ ""savage"", ""annihilator"", ""buzzard"", ""frogger"" ],
        ""vehicleType"": ""air"", ""flightHeight"": 60,
        ""maxAlive"": 8, ""perWave"": 3, ""waveIntervalMs"": 35000,
        ""inVehicleChance"": 1.0, ""occupants"": 3, ""vehiclesPerWave"": 1,
        ""minDistance"": 90, ""maxDistance"": 160
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""GreenLight"" }
    },

    // Only turns up when there is water within reach, which is most of the time in Vespucci
    // and never in Sandy Shores. A wave that finds nowhere to float is silently skipped.
    ""jets"": {
      ""name"": ""Air Force"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 2,
      ""playerRelationship"": ""hate"",
      ""armedChance"": 1.0, ""ammo"": 200, ""armour"": 100, ""accuracy"": 40,
      ""weapons"": [ { ""name"": ""Pistol"", ""weight"": 1 } ],
      ""spawn"": {
        ""models"": [ ""s_m_y_pilot_01"", ""s_m_y_blackops_01"" ],
        ""vehicles"": [ ""lazer"", ""hydra"" ],
        ""vehicleType"": ""air"", ""flightHeight"": 220, ""footFallback"": false,
        ""maxAlive"": 2, ""perWave"": 1, ""waveIntervalMs"": 70000,
        ""inVehicleChance"": 1.0, ""occupants"": 1, ""vehiclesPerWave"": 1,
        ""minDistance"": 250, ""maxDistance"": 420
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""GreenLight"" }
    },

    ""navy"": {
      ""name"": ""Coastal Patrol"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 2,
      ""playerRelationship"": ""hate"",
      ""armedChance"": 1.0, ""ammo"": 400, ""armour"": 100, ""accuracy"": 45, ""health"": 200,
      ""weapons"": [ { ""name"": ""CarbineRifle"", ""weight"": 3 }, { ""name"": ""CombatMG"", ""weight"": 1 } ],
      ""spawn"": {
        ""models"": [ ""s_m_y_marine_01"", ""s_m_y_marine_02"", ""s_m_m_marine_02"" ],
        ""vehicles"": [ ""predator"", ""dinghy"", ""dinghy2"" ],
        ""vehicleType"": ""water"",
        ""maxAlive"": 6, ""perWave"": 3, ""waveIntervalMs"": 40000,
        ""inVehicleChance"": 1.0, ""occupants"": 3, ""vehiclesPerWave"": 1,
        ""minDistance"": 60, ""maxDistance"": 200
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""GreenLight"" }
    },

    ""civilians"": {
      ""name"": ""Civilians"", ""reaction"": ""Mixed"", ""fightBackChance"": 0.45,
      ""recruits"": ""civilian"", ""share"": 3,
      // The people are not your problem here. One mode-wide ""hate"" had the crowd punching
      // you instead of the army that was shooting at them.
      ""playerRelationship"": ""neutral"",
      ""armedChance"": 0.4, ""ammo"": 120,
      ""weapons"": [
        { ""name"": ""Bat"", ""weight"": 3 }, { ""name"": ""Pistol"", ""weight"": 3 },
        { ""name"": ""Molotov"", ""weight"": 2 }, { ""name"": ""PumpShotgun"", ""weight"": 1 },
        { ""name"": ""MicroSMG"", ""weight"": 1 }
      ],
      ""blip"": { ""enabled"": false }
    }
  },

  // Every uniform hates the civilians and nothing else. Civilians are one crowd, so they do
  // not fight each other, and the mode reads as a city against an occupation.
  ""relations"": [
    { ""from"": ""police"",     ""to"": ""civilians"",  ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""infantry"",   ""to"": ""civilians"",  ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""armour"",     ""to"": ""civilians"",  ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""airsupport"", ""to"": ""civilians"",  ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""navy"",       ""to"": ""civilians"",  ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""police"",     ""to"": ""infantry"",   ""value"": ""companion"", ""mutual"": true },
    { ""from"": ""police"",     ""to"": ""armour"",     ""value"": ""companion"", ""mutual"": true },
    { ""from"": ""infantry"",   ""to"": ""armour"",     ""value"": ""companion"", ""mutual"": true },
    { ""from"": ""infantry"",   ""to"": ""airsupport"", ""value"": ""companion"", ""mutual"": true },
    { ""from"": ""armour"",     ""to"": ""airsupport"", ""value"": ""companion"", ""mutual"": true },
    { ""from"": ""infantry"",   ""to"": ""navy"",       ""value"": ""companion"", ""mutual"": true },
    { ""from"": ""jets"",       ""to"": ""civilians"",  ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""jets"",       ""to"": ""airsupport"", ""value"": ""companion"", ""mutual"": true },
    { ""from"": ""tanks"",      ""to"": ""civilians"",  ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""tanks"",      ""to"": ""infantry"",   ""value"": ""companion"", ""mutual"": true },
    { ""from"": ""tanks"",      ""to"": ""armour"",     ""value"": ""companion"", ""mutual"": true }
  ],

  ""config"": {
    ""riot"": { ""conversionChance"": 0.6 },
    ""engine"": { ""maxTrackedPeds"": 150 }
  }
}
";

        private const string Animals = @"{
  ""_stock"": true,
  ""name"": ""Animal Uprising"",
  ""order"": 60,
  ""description"": ""The wildlife has had enough. Deeply silly, and that is the point."",
  ""enabled"": true,
  ""playerRelationship"": ""hate"",


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
      ""playerRelationship"": ""hate"", ""armedChance"": 0, ""accuracy"": 100, ""health"": 200,
      ""weapons"": [],
      ""spawn"": {
        ""models"": [ ""a_c_coyote"", ""a_c_mtlion"", ""a_c_rottweiler"", ""a_c_husky"", ""a_c_shepherd"" ],
        ""maxAlive"": 20, ""perWave"": 5, ""waveIntervalMs"": 9000, ""minDistance"": 40, ""maxDistance"": 110
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""RedDark"" }
    },

    ""herd"": {
      ""name"": ""The Herd"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 1,
      ""playerRelationship"": ""hate"", ""armedChance"": 0, ""accuracy"": 100, ""health"": 250,
      ""weapons"": [],
      ""spawn"": {
        ""models"": [ ""a_c_boar"", ""a_c_deer"", ""a_c_chimp"", ""a_c_pig"", ""a_c_cow"" ],
        ""maxAlive"": 18, ""perWave"": 5, ""waveIntervalMs"": 11000, ""minDistance"": 40, ""maxDistance"": 110
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Orange"" }
    },

    ""people"": {
      ""name"": ""People"", ""reaction"": ""Mixed"", ""fightBackChance"": 0.5,
      ""recruits"": ""civilian"", ""share"": 2,
      ""playerRelationship"": ""neutral"",
      ""armedChance"": 0.4, ""ammo"": 80,
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
  ""order"": 70,

  // Ships overhead, and the scouts walk out from under them. Model names only, resolved at
  // runtime - if none of these exist in your install the invasion simply arrives without
  // ships rather than failing.
  ""craft"": {
    ""name"": ""Mothership"",
    ""models"": [ ""p_spinning_anus_s"", ""prop_ufo_01"", ""p_ufo_s"" ],
    ""count"": 2, ""height"": 115, ""orbitRadius"": 95
  },

  ""description"": ""Green men, ray guns, and a city that was not ready. Uses only content already in your game."",
  ""enabled"": true,
  ""playerRelationship"": ""hate"",


  ""escalation"": {
    ""phases"": [
      { ""name"": ""First Contact"", ""durationSeconds"": 75 },
      { ""name"": ""Harvest"", ""durationSeconds"": 180, ""fires"": true, ""blackout"": true, ""killThreshold"": 15 },
      { ""name"": ""Extermination"", ""durationSeconds"": 0, ""fires"": true }
    ]
  },

  ""factions"": {
    ""scouts"": {
      ""name"": ""Scouts"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1,
      ""playerRelationship"": ""hate"", ""arrivesByCraft"": true, ""armedChance"": 1.0, ""ammo"": 500, ""armour"": 30, ""accuracy"": 30, ""health"": 150,
      ""weapons"": [
        { ""name"": ""WEAPON_RAYPISTOL"", ""weight"": 5 },
        { ""name"": ""UnholyHellbringer"", ""weight"": 1 }
      ],
      ""spawn"": {
        ""models"": [ ""s_m_m_movalien_01"" ],
        // Fourteen, three at a time. Twenty-four arriving five at a time every nine seconds was
        // not an invasion, it was a wall - and with reinforcements on top it reached sixty.
        ""maxAlive"": 14, ""perWave"": 3, ""waveIntervalMs"": 12000,
        // Wider than it was. They walk out from under a ship, and a ring this tight around the
        // drop point has nowhere to put anybody when the ship is over a hillside.
        ""minDistance"": 20, ""maxDistance"": 60
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Green"" }
    },

    ""hunters"": {
      ""name"": ""Hunters"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 1,
      ""playerRelationship"": ""hate"", ""arrivesByCraft"": true, ""armedChance"": 1.0, ""ammo"": 800, ""armour"": 100, ""accuracy"": 40, ""health"": 250,
      ""weapons"": [
        { ""name"": ""WEAPON_RAYPISTOL"", ""weight"": 3 },
        { ""name"": ""Widowmaker"", ""weight"": 2 },
        { ""name"": ""UnholyHellbringer"", ""weight"": 2 },
        { ""name"": ""Railgun"", ""weight"": 0.5 }
      ],
      ""spawn"": {
        ""models"": [ ""s_m_m_movalien_01"" ],
        ""maxAlive"": 6, ""perWave"": 2, ""waveIntervalMs"": 22000, ""minDistance"": 20, ""maxDistance"": 60
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""GreenDark"" }
    },

    ""resistance"": {
      ""name"": ""Resistance"", ""reaction"": ""Mixed"", ""fightBackChance"": 0.55,
      ""recruits"": ""civilian"", ""share"": 3,
      // Humans are not each other's problem during an invasion, and they are not yours.
      ""playerRelationship"": ""neutral"",
      ""armedChance"": 0.5, ""ammo"": 120,
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
  ""order"": 80,
  // Purgers hunt survivors, and both are pedestrians. Intended.
  ""crowdFightsItself"": true,
  ""description"": ""Twelve minutes. All crime legal. Emergency services suspended. Then it stops."",
  ""enabled"": true,
  ""playerRelationship"": ""hate"",


  ""purge"": { ""enabled"": true, ""startHour"": 22, ""startMinute"": 0, ""durationMinutes"": 12, ""announce"": true },

  ""escalation"": {
    ""phases"": [
      // These add up to less than the purge window on purpose: the final phase runs until
      // the clock ends the mode, which is the event. Phases never stop it - the clock does.
      { ""name"": ""The Siren"", ""durationSeconds"": 120 },
      { ""name"": ""Anything Goes"", ""durationSeconds"": 300, ""fires"": true, ""looting"": true, ""barricades"": true, ""killThreshold"": 15 },
      { ""name"": ""The Last Hour"", ""durationSeconds"": 0, ""fires"": true, ""looting"": true, ""barricades"": true, ""blackout"": true }
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
        ""maxAlive"": 22, ""perWave"": 5, ""waveIntervalMs"": 11000, ""minDistance"": 140, ""maxDistance"": 260, ""inVehicleChance"": 0.9, ""occupants"": 5, ""vehiclesPerWave"": 2
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

        private const string Chaos = @"{
  ""_stock"": true,
  ""name"": ""Pedestrian Chaos"",
  ""order"": 20,
  // A free-for-all, so yes, the crowd fights itself. That is the mode.
  ""crowdFightsItself"": true,
  ""description"": ""A free-for-all. Everyone kills everyone, the police included, and you are in it. The only people on your side are the ones in your car."",
  ""enabled"": true,
  ""playerRelationship"": ""hate"",

  ""escalation"": {
    ""phases"": [
      { ""name"": ""Something Snaps"", ""durationSeconds"": 45 },
      { ""name"": ""Free For All"", ""durationSeconds"": 180, ""fires"": true, ""looting"": true, ""barricades"": true, ""killThreshold"": 10 },
      { ""name"": ""Nobody Left"", ""durationSeconds"": 0, ""fires"": true, ""looting"": true, ""barricades"": true, ""blackout"": true }
    ]
  },

  // Four anonymous crowds rather than one, purely so that ""everyone fights everyone"" can
  // still let people who share a car be on the same side. Recruits inherit the faction of
  // whoever they are riding with, so a carload stays a carload. You will never see the seams:
  // they all look the same and none of them carry a blip.
  ""factions"": {
    ""crowd_a"": {
      ""name"": ""Rioters"", ""reaction"": ""Fight"", ""recruits"": ""civilian"", ""share"": 1,
      ""playerRelationship"": ""hate"", ""armedChance"": 0.8, ""ammo"": 120,
      ""weapons"": [
        { ""name"": ""Bat"", ""weight"": 4 }, { ""name"": ""Machete"", ""weight"": 3 },
        { ""name"": ""Crowbar"", ""weight"": 3 }, { ""name"": ""Knife"", ""weight"": 3 },
        { ""name"": ""Hammer"", ""weight"": 2 }, { ""name"": ""Pistol"", ""weight"": 2 },
        { ""name"": ""SawnOffShotgun"", ""weight"": 1 }, { ""name"": ""Molotov"", ""weight"": 1 }
      ],
      ""blip"": { ""enabled"": false }
    },
    ""crowd_b"": {
      ""name"": ""Rioters"", ""reaction"": ""Fight"", ""recruits"": ""civilian"", ""share"": 1,
      ""playerRelationship"": ""hate"", ""armedChance"": 0.8, ""ammo"": 120,
      ""weapons"": [
        { ""name"": ""Bat"", ""weight"": 4 }, { ""name"": ""Machete"", ""weight"": 3 },
        { ""name"": ""Crowbar"", ""weight"": 3 }, { ""name"": ""Knife"", ""weight"": 3 },
        { ""name"": ""Hammer"", ""weight"": 2 }, { ""name"": ""Pistol"", ""weight"": 2 },
        { ""name"": ""SawnOffShotgun"", ""weight"": 1 }, { ""name"": ""Molotov"", ""weight"": 1 }
      ],
      ""blip"": { ""enabled"": false }
    },
    ""crowd_c"": {
      ""name"": ""Rioters"", ""reaction"": ""Fight"", ""recruits"": ""civilian"", ""share"": 1,
      ""playerRelationship"": ""hate"", ""armedChance"": 0.8, ""ammo"": 120,
      ""weapons"": [
        { ""name"": ""Bat"", ""weight"": 4 }, { ""name"": ""Machete"", ""weight"": 3 },
        { ""name"": ""Crowbar"", ""weight"": 3 }, { ""name"": ""Knife"", ""weight"": 3 },
        { ""name"": ""Hammer"", ""weight"": 2 }, { ""name"": ""Pistol"", ""weight"": 2 },
        { ""name"": ""SawnOffShotgun"", ""weight"": 1 }, { ""name"": ""Molotov"", ""weight"": 1 }
      ],
      ""blip"": { ""enabled"": false }
    },
    ""crowd_d"": {
      ""name"": ""Rioters"", ""reaction"": ""Fight"", ""recruits"": ""civilian"", ""share"": 1,
      ""playerRelationship"": ""hate"", ""armedChance"": 0.8, ""ammo"": 120,
      ""weapons"": [
        { ""name"": ""Bat"", ""weight"": 4 }, { ""name"": ""Machete"", ""weight"": 3 },
        { ""name"": ""Crowbar"", ""weight"": 3 }, { ""name"": ""Knife"", ""weight"": 3 },
        { ""name"": ""Hammer"", ""weight"": 2 }, { ""name"": ""Pistol"", ""weight"": 2 },
        { ""name"": ""SawnOffShotgun"", ""weight"": 1 }, { ""name"": ""Molotov"", ""weight"": 1 }
      ],
      ""blip"": { ""enabled"": false }
    },

    ""police"": {
      ""name"": ""Police"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 1,
      ""playerRelationship"": ""hate"",
      ""armedChance"": 1.0, ""ammo"": 300, ""armour"": 60, ""accuracy"": 40,
      ""weapons"": [
        { ""name"": ""Pistol"", ""weight"": 3 }, { ""name"": ""PumpShotgun"", ""weight"": 2 },
        { ""name"": ""CarbineRifle"", ""weight"": 2 }, { ""name"": ""Nightstick"", ""weight"": 1 }
      ],
      ""spawn"": {
        ""models"": [ ""s_m_y_cop_01"", ""s_f_y_cop_01"", ""s_m_y_swat_01"" ],
        ""vehicles"": [ ""police"", ""police2"", ""police3"", ""riot"" ],
        ""maxAlive"": 20, ""perWave"": 4, ""waveIntervalMs"": 10000,
        ""minDistance"": 140, ""maxDistance"": 260, ""inVehicleChance"": 1, ""occupants"": 4, ""vehiclesPerWave"": 2,
        ""siren"": true, ""driveThroughCrowds"": true
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""PoliceOfficer"", ""colour"": ""Blue"" }
    }
  },

  ""relations"": [
    { ""from"": ""crowd_a"", ""to"": ""crowd_b"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""crowd_a"", ""to"": ""crowd_c"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""crowd_a"", ""to"": ""crowd_d"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""crowd_b"", ""to"": ""crowd_c"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""crowd_b"", ""to"": ""crowd_d"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""crowd_c"", ""to"": ""crowd_d"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""police"",  ""to"": ""crowd_a"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""police"",  ""to"": ""crowd_b"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""police"",  ""to"": ""crowd_c"", ""value"": ""hate"", ""mutual"": true },
    { ""from"": ""police"",  ""to"": ""crowd_d"", ""value"": ""hate"", ""mutual"": true }
  ],

  ""config"": {
    ""riot"": { ""conversionChance"": 0.85 },
    ""blips"": { ""enabled"": true },
    ""combat"": { ""accuracy"": 25 }
  }
}
";


        private const string Night = @"{
  ""_stock"": true,
  ""name"": ""Tonight's The Night"",
  ""order"": 15,
  ""description"": ""One thing on the map is coming for you. It is faster than you, it does not stop, and there is nowhere it cannot follow. Everything it does is announced a moment before it lands; nothing it does can be walked off."",
  ""enabled"": true,

  // Not the crowd's problem and not the police's. This mode is one fight, and everything else
  // in the street is scenery for it - or a body.
  ""playerRelationship"": ""neutral"",

  // The whole mode. Its real health is pinned and meaningless: damage goes into a Resolve pool
  // at a fraction of its value, in whole while it is staggered, and staggering it is a
  // consequence of what it does rather than something you can force. Survive the ability,
  // punish the recovery. Every number is in features.hunter and can be reloaded live.
  //
  // Four heavy moves, all of them announced. A charge that commits to the line it launched on,
  // so stepping off it works and missing costs it more than connecting does; a ground strike
  // you get out of; a swing you get behind; and something out of the street thrown at your head
  // once it has been hurt. Between them it hits with its hands. Nothing it does is an explosion
  // - which is why it no longer spends the fight lying next to one of its own.
  ""hunter"": {
    ""enabled"": true,
    ""name"": ""The Night"",
    // Resolved in order, so the first one your install has is the one you get. The juggernaut
    // is the intent: armoured, faceless and far too large. Point this at an add-on model if you
    // have something better.
    ""models"": [ ""u_m_y_juggernaut_01"", ""s_m_y_blackops_03"", ""s_m_y_blackops_01"", ""s_m_m_highsec_01"" ],
    // Resolved the same way. What it picks up off the street to throw at you.
    ""debris"": [ ""prop_barrel_02a"", ""prop_bin_01a"", ""prop_rub_wheel_01"", ""prop_roadcone02a"" ],
    ""resolve"": 2400,
    ""health"": 5000,
    ""weapon"": ""WEAPON_MACHETE"",
    ""spawnDistance"": 80
  },

  ""escalation"": {
    ""phases"": [
      { ""name"": ""Something Is Wrong"", ""durationSeconds"": 90 },
      { ""name"": ""The Bodies"", ""durationSeconds"": 180, ""fires"": true, ""killThreshold"": 8 },
      { ""name"": ""Nowhere Left To Go"", ""durationSeconds"": 0, ""fires"": true, ""blackout"": true }
    ]
  },

  ""factions"": {
    ""public"": {
      ""name"": ""People"", ""reaction"": ""Mixed"", ""fightBackChance"": 0.2,
      ""recruits"": ""civilian"", ""share"": 4,
      ""playerRelationship"": ""neutral"",
      ""armedChance"": 0.25, ""ammo"": 60,
      ""weapons"": [
        { ""name"": ""Bat"", ""weight"": 3 }, { ""name"": ""Bottle"", ""weight"": 3 },
        { ""name"": ""Pistol"", ""weight"": 1 }
      ],
      ""blip"": { ""enabled"": false }
    },

    // They turn up because of the bodies, not because of you. Standing next to them is the
    // closest thing this mode has to safety, and it does not last.
    ""police"": {
      ""name"": ""LSPD"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 1,
      ""playerRelationship"": ""neutral"",
      ""armedChance"": 1.0, ""ammo"": 250, ""armour"": 50, ""accuracy"": 35,
      ""weapons"": [
        { ""name"": ""Pistol"", ""weight"": 3 }, { ""name"": ""PumpShotgun"", ""weight"": 2 },
        { ""name"": ""CarbineRifle"", ""weight"": 1 }
      ],
      ""spawn"": {
        ""models"": [ ""s_m_y_cop_01"", ""s_f_y_cop_01"", ""s_m_y_hwaycop_01"" ],
        ""vehicles"": [ ""police"", ""police2"", ""police3"", ""sheriff"" ],
        ""maxAlive"": 10, ""perWave"": 4, ""waveIntervalMs"": 25000,
        ""minDistance"": 120, ""maxDistance"": 220, ""inVehicleChance"": 1, ""occupants"": 2, ""vehiclesPerWave"": 1,
        ""siren"": true
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""PoliceOfficer"", ""colour"": ""Blue"" }
    }
  },

  ""relations"": [
    { ""from"": ""police"", ""to"": ""public"", ""value"": ""like"", ""mutual"": true }
  ],

  ""ambience"": { ""weather"": ""THUNDER"", ""hour"": 1, ""minute"": 30 },

  ""config"": {
    ""riot"": { ""conversionChance"": 0.5 },
    ""combat"": { ""accuracy"": 15 },
    // A busy street, because it is not only after you. Anything meaningfully closer to it than
    // you are is a detour it takes, and those few seconds are the only breathing room the mode
    // has - so there has to be somebody there for it to take them on.
    ""density"": { ""pedMultiplier"": 1.8, ""vehicleMultiplier"": 1.0 },
    // Nothing else in the street should be competing for your attention.
    ""features"": {
      ""pursuit"": { ""enabled"": false },
      ""looting"": { ""enabled"": false },
      ""spectacle"": { ""barricades"": false }
    }
  }
}
";

        private const string Chase = @"{
  ""_stock"": true,
  ""name"": ""Car Chase"",
  ""order"": 25,
  ""description"": ""Somebody put a price on you. It goes up. Nothing here is fighting anything except you, and it does not stop until you do."",
  ""enabled"": true,
  ""playerRelationship"": ""hate"",

  // Waves are counted from the body count rather than from a timer, so the escalation is a
  // consequence of how well you are doing. Standing still does not advance it; winning does.
  ""manhunt"": { ""enabled"": true, ""killsPerWave"": 5, ""announce"": true },

  ""escalation"": {
    ""phases"": [
      { ""name"": ""Word Gets Around"", ""durationSeconds"": 90, ""killThreshold"": 0 },
      { ""name"": ""Serious People"", ""durationSeconds"": 120, ""killThreshold"": 5 },
      { ""name"": ""Professionals"", ""durationSeconds"": 150, ""killThreshold"": 14 },
      { ""name"": ""Contractors"", ""durationSeconds"": 180, ""killThreshold"": 26 },
      { ""name"": ""Whatever It Takes"", ""durationSeconds"": 0, ""killThreshold"": 40 }
    ]
  },

  ""factions"": {
    ""bounty"": {
      ""name"": ""Chancers"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1,
      ""armedChance"": 0.9, ""ammo"": 150, ""armour"": 0, ""accuracy"": 20,
      ""weapons"": [
        { ""name"": ""Pistol"", ""weight"": 4 }, { ""name"": ""SNSPistol"", ""weight"": 3 },
        { ""name"": ""MicroSMG"", ""weight"": 2 }, { ""name"": ""Bat"", ""weight"": 2 }
      ],
      ""spawn"": {
        ""models"": [ ""g_m_y_ballasout_01"", ""g_m_y_famca_01"", ""g_m_y_mexgoon_01"", ""g_f_y_ballas_01"" ],
        ""vehicles"": [ ""manana"", ""peyote"", ""tornado"", ""emperor"", ""chino"" ],
        ""maxAlive"": 12, ""perWave"": 3, ""waveIntervalMs"": 15000,
        ""minDistance"": 130, ""maxDistance"": 240, ""inVehicleChance"": 1, ""occupants"": 3, ""vehiclesPerWave"": 1
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Orange"" }
    },

    ""crews"": {
      ""name"": ""Crews"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 1,
      ""armedChance"": 1.0, ""ammo"": 250, ""armour"": 25, ""accuracy"": 28,
      ""weapons"": [
        { ""name"": ""MicroSMG"", ""weight"": 4 }, { ""name"": ""SawnOffShotgun"", ""weight"": 3 },
        { ""name"": ""SMG"", ""weight"": 2 }, { ""name"": ""APPistol"", ""weight"": 2 }
      ],
      ""spawn"": {
        ""models"": [ ""g_m_y_lost_01"", ""g_m_y_lost_02"", ""g_m_y_lost_03"", ""g_m_y_mexgang_01"" ],
        ""vehicles"": [ ""gburrito2"", ""buccaneer"", ""vamos"", ""hexer"", ""daemon"" ],
        ""maxAlive"": 14, ""perWave"": 4, ""waveIntervalMs"": 16000,
        ""minDistance"": 130, ""maxDistance"": 240, ""inVehicleChance"": 1, ""occupants"": 4, ""vehiclesPerWave"": 2
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Purple"" }
    },

    ""pros"": {
      ""name"": ""Professionals"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 2,
      ""armedChance"": 1.0, ""ammo"": 300, ""armour"": 75, ""accuracy"": 38, ""health"": 175,
      ""weapons"": [
        { ""name"": ""CarbineRifle"", ""weight"": 4 }, { ""name"": ""AssaultRifle"", ""weight"": 3 },
        { ""name"": ""PumpShotgun"", ""weight"": 2 }, { ""name"": ""SmokeGrenade"", ""weight"": 1 }
      ],
      ""spawn"": {
        ""models"": [ ""s_m_m_armoured_01"", ""s_m_y_blackops_01"", ""s_m_m_highsec_01"" ],
        ""vehicles"": [ ""schafter2"", ""fq2"", ""granger"", ""dubsta2"", ""baller"" ],
        ""maxAlive"": 14, ""perWave"": 4, ""waveIntervalMs"": 18000,
        ""minDistance"": 140, ""maxDistance"": 260, ""inVehicleChance"": 1, ""occupants"": 4, ""vehiclesPerWave"": 2
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""RedDark"" }
    },

    ""contractors"": {
      ""name"": ""Contractors"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 3,
      ""armedChance"": 1.0, ""ammo"": 400, ""armour"": 100, ""accuracy"": 45, ""health"": 200,
      ""weapons"": [
        { ""name"": ""SpecialCarbine"", ""weight"": 4 }, { ""name"": ""CarbineRifle"", ""weight"": 3 },
        { ""name"": ""CombatMG"", ""weight"": 1 }
      ],
      ""spawn"": {
        ""models"": [ ""s_m_y_blackops_01"", ""s_m_y_blackops_02"", ""s_m_y_marine_01"" ],
        ""vehicles"": [ ""insurgent"", ""insurgent2"", ""crusader"", ""barracks"" ],
        ""maxAlive"": 12, ""perWave"": 4, ""waveIntervalMs"": 24000,
        ""minDistance"": 150, ""maxDistance"": 280, ""inVehicleChance"": 1, ""occupants"": 4, ""vehiclesPerWave"": 1,
        ""driveThroughCrowds"": true
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Red"" }
    },

    // The last thing that happens to a car chase, which is that it stops being one.
    ""air"": {
      ""name"": ""Air Support"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 4,
      ""armedChance"": 1.0, ""ammo"": 400, ""armour"": 100, ""accuracy"": 35,
      ""weapons"": [ { ""name"": ""CarbineRifle"", ""weight"": 3 }, { ""name"": ""SMG"", ""weight"": 1 } ],
      ""spawn"": {
        ""models"": [ ""s_m_y_blackops_01"", ""s_m_y_pilot_01"" ],
        ""vehicles"": [ ""buzzard"", ""frogger"", ""annihilator"" ],
        ""vehicleType"": ""air"", ""flightHeight"": 50, ""footFallback"": false,
        ""maxAlive"": 4, ""perWave"": 2, ""waveIntervalMs"": 55000,
        ""inVehicleChance"": 1.0, ""occupants"": 2, ""vehiclesPerWave"": 1,
        ""minDistance"": 100, ""maxDistance"": 180
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""RedLight"" }
    }
  },

  // Everybody here is on the same side and that side is not yours.
  ""relations"": [
    { ""from"": ""bounty"", ""to"": ""crews"",       ""value"": ""companion"", ""mutual"": true },
    { ""from"": ""bounty"", ""to"": ""pros"",        ""value"": ""companion"", ""mutual"": true },
    { ""from"": ""crews"",  ""to"": ""pros"",        ""value"": ""companion"", ""mutual"": true },
    { ""from"": ""pros"",   ""to"": ""contractors"", ""value"": ""companion"", ""mutual"": true },
    { ""from"": ""crews"",  ""to"": ""contractors"", ""value"": ""companion"", ""mutual"": true },
    { ""from"": ""bounty"", ""to"": ""air"",         ""value"": ""companion"", ""mutual"": true },
    { ""from"": ""contractors"", ""to"": ""air"",    ""value"": ""companion"", ""mutual"": true }
  ],

  ""config"": {
    // Nobody is recruited. The street stays a street and everything after you arrived to be.
    ""riot"": { ""convertAmbientPeds"": false },
    // The one mode where being converged on by cars is the point rather than the bug, so the
    // limits that keep ordinary riots survivable are lifted here and only here.
    ""vehicles"": {
      ""huntPlayerWeight"": 12, ""huntEnemyWeight"": 0, ""dismountWeight"": 2, ""fleePlayerWeight"": 0,
      ""maxHuntingPlayer"": 6, ""driveBys"": true
    },
    ""combat"": { ""maxPlayerAttackers"": 8, ""accuracy"": 25 },
    ""features"": {
      ""looting"": { ""enabled"": false },
      ""spectacle"": { ""barricades"": false },
      ""police"": { ""playerStandoff"": 8 }
    },
    ""density"": { ""pedMultiplier"": 1.0, ""vehicleMultiplier"": 1.0 }
  }
}
";

        private const string Contagion = @"{
  ""_stock"": true,
  ""name"": ""Patient Zero"",
  ""order"": 75,
  // Infected and uninfected are both pedestrians, and the whole mode is one turning into the
  // other. Saying so keeps the build from treating it as the Martial Law bug.
  ""crowdFightsItself"": true,
  ""description"": ""It spreads by reaching people, and it keeps spreading whether or not you are there to watch. Hold a street; you will not hold the district."",
  ""enabled"": true,
  ""playerRelationship"": ""hate"",

  // The third supply line. Every other mode is fed by converting the crowd near you and by
  // spawning; this one grows where its members physically are, so it moves outward, thins where
  // it is being killed, and gets worse the longer it is left. It is the only thing here that
  // can be contained by killing the right people rather than by a setting.
  //
  // Under the contact spread there is an epidemic on the clock - an origin, a widening front,
  // and a prevalence curve - and anybody the riot takes over inside that front may already have
  // it. That is what stops driving away from working: the outbreak does not pause because it is
  // off screen, and the street you cleared an hour ago is not the street you left.
  ""contagion"": {
    ""enabled"": true,
    ""source"": ""infected"",
    ""target"": ""public"",
    ""radius"": 3.5,
    ""chance"": 0.5,
    ""intervalMs"": 1100,
    ""perPass"": 3,
    ""samplesPerPass"": 20,
    ""max"": 0,

    // The unwatched half. Every one of these has a default in features.contagion; they are
    // written out here because this is the mode they are for and the curve is the mode.
    ""startingPrevalence"": 0.03,
    ""startingFront"": 140,
    ""growthPerSecond"": 0.012,
    ""maxPrevalence"": 0.9,
    ""frontMetresPerSecond"": 7
  },

  ""escalation"": {
    ""phases"": [
      { ""name"": ""An Incident"", ""durationSeconds"": 90 },
      { ""name"": ""It Is Spreading"", ""durationSeconds"": 180, ""fires"": true, ""killThreshold"": 12 },
      { ""name"": ""Quarantine"", ""durationSeconds"": 0, ""fires"": true, ""barricades"": true, ""blackout"": true }
    ]
  },

  ""factions"": {
    // A handful to start it. Everything after this is spread rather than recruited, which is
    // why the share is so low - if this recruited at any real rate the contagion would be
    // decoration on top of an ordinary riot.
    // Nothing. No knife, no hammer, and - because conversion never used to take anything away -
    // not the pistol they happened to be carrying before this started either. That last one is
    // why the street read as two crowds having a firefight instead of as an outbreak: an
    // infected who keeps their gun is not an infected, it is a person with a gun.
    //
    // Harder to put down than a pedestrian, because a thing that walks at you with its hands is
    // only frightening if walking at you works.
    ""infected"": {
      ""name"": ""Infected"", ""reaction"": ""Fight"", ""recruits"": ""civilian"", ""share"": 0.05,
      ""playerRelationship"": ""hate"",
      ""armedChance"": 0, ""disarm"": true, ""health"": 220, ""accuracy"": 0,
      ""weapons"": [ { ""name"": ""Unarmed"", ""weight"": 1 } ],
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""GreenDark"" }
    },

    // People, mostly running. A handful swing something at whatever is coming for them, and
    // that is the ceiling: an armed civilian population turns the mode into a gunfight between
    // two crowds, which is a fine thing for a riot mode to be and the wrong thing for this one.
    // The people with rifles are the cordon, and they arrive later, on purpose.
    ""public"": {
      ""name"": ""Uninfected"", ""reaction"": ""Mixed"", ""fightBackChance"": 0.12,
      ""recruits"": ""civilian"", ""share"": 4,
      ""playerRelationship"": ""neutral"",
      ""armedChance"": 0.12, ""disarm"": true, ""ammo"": 0,
      ""weapons"": [
        { ""name"": ""Bat"", ""weight"": 3 }, { ""name"": ""Crowbar"", ""weight"": 2 },
        { ""name"": ""Bottle"", ""weight"": 2 }, { ""name"": ""GolfClub"", ""weight"": 1 }
      ],
      ""blip"": { ""enabled"": false }
    },

    // The only people here with firearms, which is what makes them read as the army arriving
    // rather than as more of the crowd. They are not here to help anybody: everything inside the
    // line is a risk, including you.
    ""cordon"": {
      ""name"": ""Cordon"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 2,
      ""playerRelationship"": ""hate"",
      ""armedChance"": 1.0, ""ammo"": 350, ""armour"": 100, ""accuracy"": 40, ""health"": 200,
      ""weapons"": [
        { ""name"": ""CarbineRifle"", ""weight"": 4 }, { ""name"": ""PumpShotgun"", ""weight"": 2 },
        { ""name"": ""SmokeGrenade"", ""weight"": 1 }
      ],
      ""spawn"": {
        ""models"": [ ""s_m_y_marine_01"", ""s_m_y_marine_02"", ""s_m_m_marine_01"" ],
        ""vehicles"": [ ""barracks"", ""crusader"", ""riot"" ],
        ""maxAlive"": 16, ""perWave"": 5, ""waveIntervalMs"": 20000,
        ""minDistance"": 140, ""maxDistance"": 260, ""inVehicleChance"": 1, ""occupants"": 5, ""vehiclesPerWave"": 1,
        ""driveThroughCrowds"": true
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Green"" }
    }
  },

  ""relations"": [
    { ""from"": ""infected"", ""to"": ""public"",   ""value"": ""hate"",    ""mutual"": true },
    { ""from"": ""cordon"",   ""to"": ""infected"", ""value"": ""hate"",    ""mutual"": true },
    { ""from"": ""cordon"",   ""to"": ""public"",   ""value"": ""dislike"", ""mutual"": false }
  ],

  ""config"": {
    // High, because the crowd is the raw material for both sides of this one.
    ""riot"": { ""conversionChance"": 0.75 },
    ""combat"": { ""accuracy"": 12 },
    ""density"": { ""pedMultiplier"": 2.0 }
  }
}
";

        private const string Everything = @"{
  ""_stock"": true,
  ""name"": ""Everything"",
  ""order"": 90,
  ""description"": ""Every faction at once. Needs a gameconfig and Heap Adjuster. Do not say you were not told."",
  ""enabled"": true,
  ""playerRelationship"": ""hate"",


  ""escalation"": {
    ""phases"": [
      { ""name"": ""It Starts"", ""durationSeconds"": 60 },
      { ""name"": ""Response"", ""durationSeconds"": 120, ""fires"": true, ""killThreshold"": 15 },
      { ""name"": ""Lockdown"", ""durationSeconds"": 150, ""fires"": true, ""looting"": true, ""barricades"": true, ""killThreshold"": 40 },
      { ""name"": ""Something Else Arrives"", ""durationSeconds"": 0, ""fires"": true, ""looting"": true, ""barricades"": true, ""blackout"": true }
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
        ""maxAlive"": 16, ""perWave"": 4, ""waveIntervalMs"": 13000, ""minDistance"": 140, ""maxDistance"": 260, ""inVehicleChance"": 0.75, ""occupants"": 4, ""vehiclesPerWave"": 2
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
        ""maxAlive"": 18, ""perWave"": 4, ""waveIntervalMs"": 10000,
        ""minDistance"": 140, ""maxDistance"": 260, ""inVehicleChance"": 1, ""occupants"": 4, ""vehiclesPerWave"": 2, ""siren"": true, ""driveThroughCrowds"": true
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
        ""maxAlive"": 20, ""perWave"": 5, ""waveIntervalMs"": 12000,
        ""minDistance"": 140, ""maxDistance"": 260, ""inVehicleChance"": 1, ""occupants"": 6, ""vehiclesPerWave"": 2, ""driveThroughCrowds"": true
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""Green"" }
    },

    ""aliens"": {
      ""name"": ""Aliens"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 3,
      ""armedChance"": 1.0, ""ammo"": 600, ""armour"": 100, ""accuracy"": 55, ""health"": 250,
      ""weapons"": [ { ""name"": ""WEAPON_RAYPISTOL"", ""weight"": 3 }, { ""name"": ""UnholyHellbringer"", ""weight"": 2 }, { ""name"": ""Widowmaker"", ""weight"": 1 } ],
      ""spawn"": {
        ""models"": [ ""s_m_m_movalien_01"" ],
        ""maxAlive"": 16, ""perWave"": 5, ""waveIntervalMs"": 11000
      },
      ""blip"": { ""enabled"": true, ""sprite"": ""Standard"", ""colour"": ""GreenDark"" }
    },

    ""animals"": {
      ""name"": ""Animals"", ""reaction"": ""Fight"", ""recruits"": ""none"", ""share"": 1, ""fromPhase"": 2,
      ""armedChance"": 0, ""accuracy"": 100, ""health"": 200,
      ""weapons"": [],
      ""spawn"": {
        ""models"": [ ""a_c_coyote"", ""a_c_mtlion"", ""a_c_rottweiler"", ""a_c_boar"" ],
        ""maxAlive"": 14, ""perWave"": 4, ""waveIntervalMs"": 14000
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
