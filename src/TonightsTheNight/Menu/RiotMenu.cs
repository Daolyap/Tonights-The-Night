using System;
using System.Collections.Generic;
using LemonUI;
using LemonUI.Menus;
using TonightsTheNight.Config;
using TonightsTheNight.Core;
using TonightsTheNight.Factions;
using TonightsTheNight.Util;

namespace TonightsTheNight.Menu
{
    /// <summary>
    /// The mod menu.
    ///
    /// Every control here writes to the config store's live layer rather than to a field, so
    /// what you change in the menu, what you write in user.json, and what a mode overrides are
    /// all the same mechanism. A setting can therefore be tuned mid-riot and, later, saved to
    /// a profile without any of it being special-cased.
    /// </summary>
    public sealed class RiotMenu
    {
        private readonly ObjectPool _pool = new ObjectPool();
        private readonly List<Action> _refreshers = new List<Action>();
        private readonly ConfigStore _config;
        private readonly Director _director;
        private readonly ModeLibrary _modes;
        private readonly Action _reloadRequested;

        private NativeMenu _root;
        private NativeMenu _modeMenu;
        private NativeMenu _tuningMenu;
        private NativeMenu _featuresMenu;
        private NativeMenu _zoneMenu;
        private NativeMenu _weaponMenu;
        private NativeMenu _pursuitMenu;
        private NativeMenu _lootMenu;
        private NativeMenu _profileMenu;
        private NativeItem _stopItem;
        private NativeItem _phaseItem;
        private int _profileSlot = 1;

        public RiotMenu(ConfigStore config, Director director, ModeLibrary modes, Action reloadRequested)
        {
            _config = config;
            _director = director;
            _modes = modes;
            _reloadRequested = reloadRequested;

            Build();
        }

        public bool Visible
        {
            get { return _root.Visible; }
            set { _root.Visible = value; }
        }

        public void Toggle() { _root.Visible = !_root.Visible; }

        public void Process() { _pool.Process(); }

        private void Build()
        {
            _root = new NativeMenu("Tonight's The Night", "RIOT CONTROL");
            _pool.Add(_root);

            BuildModeMenu();
            BuildTuningMenu();
            BuildWeaponMenu();
            BuildZoneMenu();
            BuildPursuitMenu();
            BuildLootMenu();
            BuildFeaturesMenu();
            BuildProfileMenu();

            _phaseItem = new NativeItem("Skip To Next Phase", "Force the riot to escalate now instead of waiting.");
            _phaseItem.Activated += (sender, args) =>
            {
                if (!_director.Escalation.Advance())
                {
                    GTA.UI.Notification.Show("~o~Already at the final phase.");
                }
                RefreshStopItem();
            };
            _root.Add(_phaseItem);

            _stopItem = new NativeItem("Stop Riot", "Restore the world and release every ped we took over.");
            _stopItem.Activated += (sender, args) =>
            {
                _director.Stop();
                RefreshStopItem();
            };
            _root.Add(_stopItem);

            var reload = new NativeItem("Reload Config", "Re-read defaults.json, user.json and every mode file from disk.");
            reload.Activated += (sender, args) => _reloadRequested();
            _root.Add(reload);

            // Where the files went should never again be something you can only find by
            // searching the disk.
            var paths = new NativeItem("Show File Locations", "Print the config and log paths on screen and to the log.");
            paths.Activated += (sender, args) =>
            {
                Log.Info("Path resolution (requested from menu):");
                foreach (string line in Paths.Diagnostics().Split('\n'))
                {
                    Log.Info("  " + line.TrimEnd('\r'));
                }

                GTA.UI.Notification.Show("~b~Config:~s~ " + Paths.ConfigDir);
                GTA.UI.Screen.ShowSubtitle(Paths.Diagnostics().Replace(Environment.NewLine, "~n~"), 12000);
                _root.Visible = false;
            };
            _root.Add(paths);

            _root.Shown += (sender, args) => RefreshStopItem();
            RefreshStopItem();
        }

        private void BuildModeMenu()
        {
            _modeMenu = new NativeMenu("Tonight's The Night", "RIOT MODES");
            _pool.Add(_modeMenu);
            AddSubMenu(_modeMenu, "Riot Modes", "Pick a mode and start it.");
            PopulateModes();
        }

        /// <summary>
        /// LemonUI names the parent item from the submenu's *subtitle*, not its title, so a
        /// submenu added bare shows up as a shouty banner string in the parent list. Name the
        /// returned item explicitly instead.
        /// </summary>
        private NativeSubmenuItem AddSubMenu(NativeMenu submenu, string title, string description)
        {
            NativeSubmenuItem item = _root.AddSubMenu(submenu);
            item.Title = title;
            item.Description = description;
            return item;
        }

        private void PopulateModes()
        {
            _modeMenu.Clear();

            if (_modes.Modes.Count == 0)
            {
                _modeMenu.Add(new NativeItem("No modes found", "Check scripts/TonightsTheNight/modes/ and the log."));
                return;
            }

            foreach (RiotMode mode in _modes.Modes)
            {
                RiotMode captured = mode;
                var item = new NativeItem(mode.Name, string.IsNullOrEmpty(mode.Description) ? "Start this mode." : mode.Description);
                item.Activated += (sender, args) =>
                {
                    try
                    {
                        _director.Start(captured);
                        RefreshStopItem();
                        _root.Visible = false;
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Could not start mode '" + captured.Id + "'", ex);
                        GTA.UI.Notification.Show("~r~Could not start that mode. See the log.");
                    }
                };
                _modeMenu.Add(item);
            }
        }

        private void BuildTuningMenu()
        {
            _tuningMenu = new NativeMenu("Tonight's The Night", "TUNING");
            _pool.Add(_tuningMenu);
            AddSubMenu(_tuningMenu, "Tuning", "Live settings: how many peds, how hard they fight, blips.");

            AddPercentSlider(_tuningMenu, "Conversion Chance", "riot.conversionChance", 0.85f,
                "How likely a nearby pedestrian is to be pulled into a faction.");

            AddRangeSlider(_tuningMenu, "Recruit Radius", "riot.recruitRadius", 180, 50, 400, 25,
                "How far from you peds get recruited. Bigger is not better - it costs frames.");

            AddRangeSlider(_tuningMenu, "Max Tracked Peds", "engine.maxTrackedPeds", 120, 20, 300, 20,
                "The hard ceiling. Raise it only if you have a gameconfig and Heap Adjuster.");

            AddRangeSlider(_tuningMenu, "Accuracy", "combat.accuracy", 20, 0, 100, 5,
                "Rioter marksmanship. Low is realistic; high turns a riot into a massacre.");

            AddPercentSlider(_tuningMenu, "Ped Density", "density.pedMultiplier", 1.5f,
                "Ambient crowd multiplier, applied every frame while a mode runs.", 3f);

            AddToggle(_tuningMenu, "Blips", "blips.enabled", true,
                "Faction blips on the minimap. Capped and distance-limited, but still not free.");

            AddStancePicker(_tuningMenu);

            AddToggle(_tuningMenu, "Never Flee", "combat.neverFlee", true,
                "Stops fighters breaking off and running. Turn off for a more realistic crowd.");
        }

        /// <summary>
        /// The weapon presets from the original plan, as one picker. A preset is a whole
        /// answer - weapons, armour and how many people are armed at all - because those are
        /// the same decision, and splitting them across three sliders would only let you build
        /// the incoherent middle.
        /// </summary>
        private void BuildWeaponMenu()
        {
            _weaponMenu = new NativeMenu("Tonight's The Night", "WEAPONS");
            _pool.Add(_weaponMenu);
            AddSubMenu(_weaponMenu, "Weapons", "What the crowd is carrying. Presets, or your own list.");

            var picker = new NativeListItem<string>("Preset", WeaponPresets.DescriptionOf(WeaponPresets.UseMode), WeaponPresets.Names);
            picker.SelectedIndex = Math.Max(0, Array.IndexOf(WeaponPresets.Ids, _config.GetString("weapons.preset", WeaponPresets.UseMode)));
            picker.Description = WeaponPresets.DescriptionOf(WeaponPresets.Ids[picker.SelectedIndex]);

            picker.ItemChanged += (sender, args) =>
            {
                string id = WeaponPresets.Ids[picker.SelectedIndex];
                _config.SetLive("weapons.preset", JsonValue.Of(id));
                picker.Description = WeaponPresets.DescriptionOf(id);
            };

            _weaponMenu.Add(picker);
            _refreshers.Add(() =>
            {
                picker.SelectedIndex = Math.Max(0, Array.IndexOf(WeaponPresets.Ids, _config.GetString("weapons.preset", WeaponPresets.UseMode)));
                picker.Description = WeaponPresets.DescriptionOf(WeaponPresets.Ids[picker.SelectedIndex]);
            });

            _weaponMenu.Add(new NativeItem("Applies To New Recruits",
                "Changing the preset does not re-arm the people already out there. It takes effect " +
                "as the riot pulls in more of the crowd, which happens continuously."));

            _weaponMenu.Add(new NativeItem("Custom Preset",
                "Pick Custom, then list weapon names in weapons.custom in user.json - " +
                "for example [\"WEAPON_BAT\", {\"name\": \"WEAPON_PISTOL\", \"weight\": 0.2}]. " +
                "Reload with " + _config.GetString("menu.reloadKey", "F5") + "."));
        }

        /// <summary>
        /// Car chases. The trigger is provocation, not proximity, so these settings are mostly
        /// about how long a grudge lasts and how hard it is to shake.
        /// </summary>
        private void BuildPursuitMenu()
        {
            _pursuitMenu = new NativeMenu("Tonight's The Night", "CAR CHASES");
            _pool.Add(_pursuitMenu);
            AddSubMenu(_pursuitMenu, "Car Chases", "Hurt someone and drive off, and their side comes after you.");

            AddToggle(_pursuitMenu, "Car Chases", "features.pursuit.enabled", true,
                "Off: nobody follows you. Everything else here does nothing.");

            AddRangeSlider(_pursuitMenu, "Chasers Per Car", "features.pursuit.crewSize", 3, 1, 4, 1,
                "How many of them pile into the same car. The whole point is that it is a carload.");

            AddRangeSlider(_pursuitMenu, "Chases At Once", "features.pursuit.maxChases", 2, 1, 4, 1,
                "How many separate carloads can be after you at the same time.");

            AddRangeSlider(_pursuitMenu, "Grudge Length", "features.pursuit.grudgeSeconds", 45, 10, 180, 10,
                "Seconds after you hurt someone that their side still wants you.");

            AddToggle(_pursuitMenu, "Drive-Bys", "features.pursuit.driveBys", true,
                "Passengers lean out and shoot. The driver keeps both hands on the wheel.");

            AddToggle(_pursuitMenu, "Ram Instead Of Chase", "features.pursuit.ram", false,
                "On: they drive into you rather than tailing you. Shorter, louder chases.");

            AddRangeSlider(_pursuitMenu, "Give Up Distance", "features.pursuit.giveUpDistance", 320, 100, 800, 50,
                "How far ahead you have to get before they start losing interest.");

            AddToggle(_pursuitMenu, "Blip The Chase Car", "features.pursuit.blip", true,
                "Puts their car on the minimap. It flashes once they are close.");

            AddToggle(_pursuitMenu, "Announce Chases", "features.pursuit.notify", true,
                "A notification when a carload sets off after you.");
        }

        private void BuildLootMenu()
        {
            _lootMenu = new NativeMenu("Tonight's The Night", "LOOTING");
            _pool.Add(_lootMenu);
            AddSubMenu(_lootMenu, "Looting", "People taking advantage. Starts once the riot has been going a while.");

            AddToggle(_lootMenu, "Looting", "features.looting.enabled", true,
                "Only runs during phases the mode marks as looting phases.");

            AddRangeSlider(_lootMenu, "Looters At Once", "features.looting.maxActive", 6, 0, 20, 2,
                "How many people are carrying something off at any one time.");

            AddToggle(_lootMenu, "Carry Things", "features.looting.carryProps", true,
                "Televisions, cases and bin bags. Off: they just leave in a hurry.");

            AddToggle(_lootMenu, "Steal Cars", "features.looting.stealVehicles", true,
                "Some looters take a parked car instead. Empty cars only.");

            AddPercentSlider(_lootMenu, "Fighters Who Loot", "features.looting.fighterChance", 0.2f,
                "How often someone mid-fight breaks off to loot. Low keeps the riot fighting.");
        }

        private void BuildZoneMenu()
        {
            _zoneMenu = new NativeMenu("Tonight's The Night", "RIOT ZONE");
            _pool.Add(_zoneMenu);
            AddSubMenu(_zoneMenu, "Riot Zone", "Confine the riot, or let it run citywide.");

            string[] labels = { "Radius Around You", "Citywide" };
            string[] values = { "radius", "citywide" };

            var mode = new NativeListItem<string>("Extent",
                "Citywide costs frames and loses the contrast of one district in flames while the rest carries on.",
                labels);
            mode.SelectedIndex = Math.Max(0, Array.IndexOf(values, _config.GetString("zone.mode", "radius")));
            mode.ItemChanged += (sender, args) => _config.SetLive("zone.mode", JsonValue.Of(values[mode.SelectedIndex]));
            _zoneMenu.Add(mode);
            _refreshers.Add(() => mode.SelectedIndex = Math.Max(0, Array.IndexOf(values, _config.GetString("zone.mode", "radius"))));

            AddRangeSlider(_zoneMenu, "Radius", "zone.radius", 300, 100, 900, 50,
                "How far the riot reaches. The single most effective performance control here.");

            AddToggle(_zoneMenu, "Zone Follows You", "zone.followPlayer", true,
                "On: the riot travels with you. Off: it stays where you started it.");

            AddToggle(_zoneMenu, "Show Zone On Map", "zone.showOnMap", true,
                "Draw the riot zone as a circle on the minimap.");
        }

        private void BuildProfileMenu()
        {
            _profileMenu = new NativeMenu("Tonight's The Night", "PROFILES");
            _pool.Add(_profileMenu);
            AddSubMenu(_profileMenu, "Profiles", "Save the settings you have changed and come back to them.");

            var slots = new string[Profiles.Slots];
            for (int i = 0; i < Profiles.Slots; i++) { slots[i] = "Slot " + (i + 1); }

            var picker = new NativeListItem<string>("Slot", "Which slot to save to or load from.", slots);
            picker.ItemChanged += (sender, args) => _profileSlot = picker.SelectedIndex + 1;
            _profileMenu.Add(picker);

            var save = new NativeItem("Save Settings To Slot",
                "Stores only the settings you changed, so a profile still makes sense after an update.");
            save.Activated += (sender, args) =>
            {
                GTA.UI.Notification.Show(Profiles.Save(_profileSlot, _config)
                    ? "~g~Saved~s~ to slot " + _profileSlot + "."
                    : "~o~Nothing to save~s~ - no settings changed this session.");
            };
            _profileMenu.Add(save);

            var load = new NativeItem("Load Slot", "Apply a saved profile over your current settings.");
            load.Activated += (sender, args) =>
            {
                if (Profiles.Load(_profileSlot, _config))
                {
                    Rebuild();
                    GTA.UI.Notification.Show("~g~Loaded~s~ slot " + _profileSlot + ".");
                }
                else
                {
                    GTA.UI.Notification.Show("~o~Slot " + _profileSlot + " is empty.");
                }
            };
            _profileMenu.Add(load);

            var clear = new NativeItem("Clear Slot", "Delete the profile in this slot.");
            clear.Activated += (sender, args) =>
            {
                GTA.UI.Notification.Show(Profiles.Delete(_profileSlot)
                    ? "~g~Cleared~s~ slot " + _profileSlot + "."
                    : "~o~Slot " + _profileSlot + " was already empty.");
            };
            _profileMenu.Add(clear);
        }

        private void BuildFeaturesMenu()
        {
            _featuresMenu = new NativeMenu("Tonight's The Night", "FEATURES");
            _pool.Add(_featuresMenu);
            AddSubMenu(_featuresMenu, "Features", "Optional extras and mod-compatibility switches.");

            AddToggle(_featuresMenu, "Escalation Phases", "features.escalation.enabled", true,
                "Off: every faction is present from the start, including the military.");

            AddToggle(_featuresMenu, "Fires And Debris", "features.fires.enabled", true,
                "Burning cars and street fires once the riot reaches its second phase.");

            AddRangeSlider(_featuresMenu, "Max Fires", "features.fires.maxActive", 8, 0, 24, 2,
                "How many fires can burn at once.");

            AddToggle(_featuresMenu, "Weather And Lighting", "features.ambience.enabled", true,
                "Let a mode set the weather, time of day and colour grade.");

            AddToggle(_featuresMenu, "Purge Timer", "features.purge.enabled", true,
                "The countdown and curfew in Purge mode. Off makes it run indefinitely.");

            AddToggle(_featuresMenu, "Spawn Factions", "riot.spawnFactions", true,
                "Off: only ambient pedestrians are used. No police, military, aliens or animals.");

            AddToggle(_featuresMenu, "Drive Through Crowds", "features.police.driveThroughCrowds", true,
                "Police and military drivers stop steering around people.");

            AddToggle(_featuresMenu, "Debug Overlay", "features.debugOverlay.enabled", false,
                "On-screen counters. Logging happens either way.");

            AddToggle(_featuresMenu, "Hot Reload", "features.hotReload.enabled", true,
                "Re-read config files without restarting the game.");

            AddToggle(_featuresMenu, "Watch Files", "features.hotReload.watchFiles", true,
                "Reload automatically when a config file changes on disk.");

            AddToggle(_featuresMenu, "Protect Police & Emergency", "compatibility.protectEmergencyServices", true,
                "Leave cops, SWAT, army, medics and firefighters alone. Keep this on if you run a police or wanted-system overhaul.");

            AddToggle(_featuresMenu, "Protect Mission Peds", "compatibility.protectMissionPeds", true,
                "Never recruit story peds. Turning this off can break missions.");
        }

        /// <summary>
        /// How the rioters regard the player. Applied live, so it can be flipped mid-riot
        /// without restarting the mode.
        /// </summary>
        private void AddStancePicker(NativeMenu menu)
        {
            string[] labels = { "Ignored", "Disliked", "Target" };
            string[] values = { "ignored", "disliked", "target" };

            var item = new NativeListItem<string>("They Treat You As",
                "Ignored: walk through it untouched. Disliked: they react if you get close. Target: fair game.",
                labels);

            item.SelectedIndex = Math.Max(0, Array.IndexOf(values, _config.GetString("player.stance", "target")));
            item.ItemChanged += (sender, args) =>
            {
                _config.SetLive("player.stance", JsonValue.Of(values[item.SelectedIndex]));
                _director.RefreshPlayerStance();
            };

            menu.Add(item);
            _refreshers.Add(() =>
                item.SelectedIndex = Math.Max(0, Array.IndexOf(values, _config.GetString("player.stance", "target"))));
        }

        private void AddToggle(NativeMenu menu, string title, string path, bool fallback, string description)
        {
            var item = new NativeCheckboxItem(title, description, _config.GetBool(path, fallback));
            item.CheckboxChanged += (sender, args) => _config.SetLive(path, JsonValue.Of(item.Checked));
            menu.Add(item);
            _refreshers.Add(() => item.Checked = _config.GetBool(path, fallback));
        }

        private void AddPercentSlider(NativeMenu menu, string title, string path, float fallback, string description, float scale = 1f)
        {
            int steps = 20;
            float current = _config.GetFloat(path, fallback);
            var item = new NativeSliderItem(title, description, steps, (int)Math.Round(current / scale * steps));
            item.ValueChanged += (sender, args) => _config.SetLive(path, JsonValue.Of(item.Value / (double)steps * scale));
            menu.Add(item);
            _refreshers.Add(() => item.Value = (int)Math.Round(_config.GetFloat(path, fallback) / scale * steps));
        }

        private void AddRangeSlider(NativeMenu menu, string title, string path, int fallback, int min, int max, int step, string description)
        {
            int steps = (max - min) / step;
            int current = _config.GetInt(path, fallback);
            int index = Math.Max(0, Math.Min(steps, (current - min) / step));

            var item = new NativeSliderItem(title, description, steps, index);
            item.ValueChanged += (sender, args) => _config.SetLive(path, JsonValue.Of(min + item.Value * step));
            menu.Add(item);
            _refreshers.Add(() =>
                item.Value = Math.Max(0, Math.Min(steps, (_config.GetInt(path, fallback) - min) / step)));
        }

        /// <summary>
        /// Called after a config or mode reload so the menu reflects what is on disk. Rebuilding
        /// the menu objects would churn the LemonUI pool and drop the player out of whatever
        /// submenu they were in, so instead the mode list is repopulated and every control
        /// re-reads its own setting.
        /// </summary>
        public void Rebuild()
        {
            PopulateModes();

            foreach (Action refresh in _refreshers)
            {
                try
                {
                    refresh();
                }
                catch (Exception ex)
                {
                    Log.Error("Could not refresh a menu control", ex);
                }
            }

            RefreshStopItem();
        }

        private void RefreshStopItem()
        {
            _stopItem.Enabled = _director.IsRunning;
            _stopItem.Title = _director.IsRunning ? "Stop Riot" : "Stop Riot (nothing running)";

            _phaseItem.Enabled = _director.IsRunning && _director.Escalation.Enabled;
            _phaseItem.Title = _director.IsRunning
                ? "Skip Phase (now: " + _director.Escalation.CurrentName + ")"
                : "Skip Phase (nothing running)";
        }
    }
}
