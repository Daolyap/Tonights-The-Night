using System;
using System.Collections.Generic;
using System.Drawing;
using LemonUI.Elements;
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

        /// <summary>
        /// Set while Rebuild is pushing disk values back into the controls.
        ///
        /// LemonUI raises ValueChanged, CheckboxChanged and ItemChanged from the property
        /// setters, so refreshing a control re-entered its own handler and wrote the value
        /// straight back into the live layer. Because the live layer wins over user.json, every
        /// reload silently froze those keys for the rest of the session and profiles saved
        /// settings the player had never touched.
        /// </summary>
        private bool _refreshing;
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
        private NativeMenu _drivingMenu;
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

        /// <summary>
        /// Any menu in the pool, not just the root.
        ///
        /// LemonUI closes the parent when you step into a submenu, so the root is hidden for
        /// nearly all of the time the menu is actually on screen. Reading the root alone meant
        /// the Director never paused where it mattered, and F6 opened a second menu on top of
        /// the one already open and fed input to both.
        /// </summary>
        public bool Visible
        {
            get { return _pool.AreAnyVisible; }
            set
            {
                if (value) { _root.Visible = true; }
                else { _pool.HideAll(); }
            }
        }

        public void Toggle() { Visible = !Visible; }

        /// <summary>Milliseconds the last menu draw took. Shown on the debug overlay.</summary>
        public double LastDrawMs { get; private set; }

        public void Process()
        {
            // Measured because two attempts at fixing the frame rate have now been guesses. If
            // this reads 0.2ms while the frame rate is halved, the cost is not the menu.
            _stopwatch.Restart();
            _pool.Process();
            _stopwatch.Stop();
            LastDrawMs = _stopwatch.Elapsed.TotalMilliseconds;
        }

        private readonly System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();

        /// <summary>
        /// The mod's look, applied to every menu.
        ///
        /// An earlier version stripped the banner, the corner buttons and mouse support on the
        /// theory that the Scaleform was what halved the frame rate. It was not - the lag
        /// survived all of it - so the decoration is back and this is where the styling lives
        /// instead. If you want the bare version, menu.lightweight still does it.
        /// </summary>
        private void Style(NativeMenu menu)
        {
            try
            {
                if (_config.GetBool("menu.lightweight", false))
                {
                    menu.Banner = null;
                    menu.Buttons.Clear();
                    menu.MouseBehavior = MenuMouseBehavior.Disabled;
                    menu.RotateCamera = false;
                    return;
                }

                // A flat colour banner rather than the stock texture: no art to ship, and a
                // riot mod should not open in Franchise blue.
                menu.Banner = new ScaledRectangle(PointF.Empty, SizeF.Empty)
                {
                    Color = Colour("menu.bannerColour", Color.FromArgb(235, 132, 22, 22))
                };

                if (menu.BannerText != null) { menu.BannerText.Font = GTA.UI.Font.HouseScript; }
                menu.NameFont = GTA.UI.Font.ChaletComprimeCologne;
                menu.DescriptionFont = GTA.UI.Font.ChaletLondon;

                menu.ItemCount = CountVisibility.Always;
                menu.MaxItems = Math.Max(4, _config.GetInt("menu.maxItems", 9));
                menu.Width = Math.Max(300f, _config.GetFloat("menu.width", 460f));
            }
            catch (Exception ex)
            {
                Log.Error("Could not style a menu", ex);
            }
        }

        /// <summary>Reads "r,g,b" or "a,r,g,b" from config, falling back to the shipped colour.</summary>
        private Color Colour(string path, Color fallback)
        {
            string text = _config.GetString(path, null);
            if (string.IsNullOrEmpty(text)) { return fallback; }

            string[] parts = text.Split(',');
            var values = new int[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i].Trim(), out values[i])) { return fallback; }
                if (values[i] < 0 || values[i] > 255) { return fallback; }
            }

            if (values.Length == 3) { return Color.FromArgb(255, values[0], values[1], values[2]); }
            if (values.Length == 4) { return Color.FromArgb(values[0], values[1], values[2], values[3]); }

            Log.Warn("Colour '" + text + "' needs three or four numbers. Using the default.");
            return fallback;
        }

        private void Build()
        {
            _root = new NativeMenu("Tonight's The Night", "RIOT CONTROL");
            _pool.Add(_root);
            Style(_root);

            BuildModeMenu();
            BuildTuningMenu();
            BuildWeaponMenu();
            BuildZoneMenu();
            BuildDrivingMenu();
            BuildPursuitMenu();
            BuildLootMenu();
            BuildFeaturesMenu();
            BuildProfileMenu();

            _phaseItem = new NativeItem("Skip To Next Phase", "Force the riot to escalate now instead of waiting.");
            _phaseItem.Activated += (sender, args) =>
            {
                if (!_director.SkipPhase())
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
            Style(submenu);
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
                        Visible = false;
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

            AddToggle(_tuningMenu, "They Have To See You", "features.perception.enabled", true,
                "On: break line of sight and they lose you, then go back to fighting each other. "
                + "Off: everyone hostile knows where you are at all times, through walls.");

            AddRangeSlider(_tuningMenu, "How Far They See", "features.perception.sightRange", 140, 40, 300, 20,
                "How far a rioter can pick you out. Crouching and cover shorten it.");

            AddRangeSlider(_tuningMenu, "How Long They Search", "features.perception.forgetSeconds", 20, 3, 60, 3,
                "Seconds they keep hunting after losing sight of you.");

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
                picker.Description = WeaponPresets.DescriptionOf(id);
                if (_refreshing) { return; }
                _config.SetLive("weapons.preset", JsonValue.Of(id));
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
        /// What rioters already behind a wheel do about it. Weights rather than a switch,
        /// because the bug being fixed here was every driver making the identical decision.
        /// </summary>
        private void BuildDrivingMenu()
        {
            _drivingMenu = new NativeMenu("Tonight's The Night", "RIOTERS DRIVING");
            _pool.Add(_drivingMenu);
            AddSubMenu(_drivingMenu, "Rioters Driving", "What someone already in a car does when the riot reaches them.");

            AddToggle(_drivingMenu, "Rioters Drive", "vehicles.enabled", true,
                "Off: every driver stops and gets out, which is what a street of abandoned cars looks like.");

            AddRangeSlider(_drivingMenu, "Get Out And Fight", "vehicles.dismountWeight", 3, 0, 10, 1,
                "Relative weight. Stop the car, get out, join in on foot.");

            AddRangeSlider(_drivingMenu, "Chase Another Rioter", "vehicles.huntEnemyWeight", 3, 0, 10, 1,
                "Relative weight. Go after someone their side hates.");

            AddRangeSlider(_drivingMenu, "Chase You", "vehicles.huntPlayerWeight", 2, 0, 10, 1,
                "Relative weight. Come after you specifically.");

            AddRangeSlider(_drivingMenu, "Drive Away From You", "vehicles.fleePlayerWeight", 2, 0, 10, 1,
                "Relative weight. Get out of your way and keep going.");

            AddToggle(_drivingMenu, "Shoot From Cars", "vehicles.driveBys", true,
                "Drivers and passengers lean out, if they have something to lean out with.");

            AddToggle(_drivingMenu, "Drive Through Crowds", "vehicles.driveThroughCrowds", true,
                "Rioter drivers stop steering around people.");

            AddToggle(_drivingMenu, "Ram Instead Of Chase", "vehicles.ram", false,
                "On: drivers going after someone drive into them rather than following.");
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
            mode.ItemChanged += (sender, args) =>
            {
                if (_refreshing) { return; }
                _config.SetLive("zone.mode", JsonValue.Of(values[mode.SelectedIndex]));
            };
            _zoneMenu.Add(mode);
            _refreshers.Add(() => mode.SelectedIndex = Math.Max(0, Array.IndexOf(values, _config.GetString("zone.mode", "radius"))));

            AddRangeSlider(_zoneMenu, "Radius", "zone.radius", 300, 100, 900, 50,
                "How far the riot reaches. The single most effective performance control here.");

            AddToggle(_zoneMenu, "Zone Follows You", "zone.followPlayer", true,
                "On: the riot travels with you. Off: it stays where you started it.");

            AddToggle(_zoneMenu, "Show Zone On Map", "zone.showOnMap", false,
                "Draw the riot zone as a circle on the minimap. Only useful when the zone is not following you.");
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

            AddToggle(_featuresMenu, "Weather And Lighting", "features.ambience.enabled", false,
                "Off by default. On, a mode may change your weather and colour grade. Your clock is " +
                "left alone either way unless you turn on features.ambience.setTime.");

            AddToggle(_featuresMenu, "Purge Timer", "features.purge.enabled", true,
                "The countdown in Purge mode. Off makes it run indefinitely.");

            AddToggle(_featuresMenu, "No Wanted Level During Purge", "features.purge.noWantedLevel", true,
                "All crime is legal, so the game's own police lose interest. Your wanted ceiling is " +
                "put back exactly as it was when the purge ends.");

            AddToggle(_featuresMenu, "Blackout", "features.spectacle.blackout", true,
                "The city's power fails in the late phases. Street lights, shop signs and windows, "
                + "the whole map. At night the difference is total.");

            AddToggle(_featuresMenu, "Barricades", "features.spectacle.barricades", true,
                "Rioters drag street furniture across the roads and set it alight. Cars can still "
                + "smash through - that is the point of them being props rather than walls.");

            AddToggle(_featuresMenu, "Smoke Columns", "features.spectacle.smoke", true,
                "Smoke rising off the burning barricades, visible from the other side of the map.");

            AddToggle(_featuresMenu, "Helicopter Searchlights", "features.air.searchlight", true,
                "Sweeping a blacked-out street is most of what a helicopter is for.");

            AddToggle(_featuresMenu, "Reinforcements", "features.reinforcements.enabled", true,
                "Factions that take losses send bigger waves, sooner. Off: the response never grows.");

            AddToggle(_featuresMenu, "Craft Overhead", "features.craft.enabled", true,
                "Ships in the sky during Invasion, with aliens arriving underneath them.");

            AddToggle(_featuresMenu, "Bare Menu", "menu.lightweight", false,
                "Drops the banner, corner buttons and mouse support. Tested and it did not fix the "
                + "frame rate, so it is off - but it is here if you want the plainest possible menu.");

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
            string[] labels = { "Mode's Own", "Ignored", "Disliked", "Target" };
            string[] values = { "mode", "ignored", "disliked", "target" };

            var item = new NativeListItem<string>("They Treat You As",
                "Mode's Own: the army wants you, the crowd it is shooting at does not. The other three " +
                "force one answer on everybody - including the people you would otherwise be running with.",
                labels);

            item.SelectedIndex = Math.Max(0, Array.IndexOf(values, _config.GetString("player.stance", "mode")));
            item.ItemChanged += (sender, args) =>
            {
                if (_refreshing) { return; }
                _config.SetLive("player.stance", JsonValue.Of(values[item.SelectedIndex]));
                _director.RefreshPlayerStance();
            };

            menu.Add(item);
            _refreshers.Add(() =>
                item.SelectedIndex = Math.Max(0, Array.IndexOf(values, _config.GetString("player.stance", "mode"))));
        }

        private void AddToggle(NativeMenu menu, string title, string path, bool fallback, string description)
        {
            var item = new NativeCheckboxItem(title, description, _config.GetBool(path, fallback));
            item.CheckboxChanged += (sender, args) =>
            {
                if (_refreshing) { return; }
                _config.SetLive(path, JsonValue.Of(item.Checked));
            };
            menu.Add(item);
            _refreshers.Add(() => item.Checked = _config.GetBool(path, fallback));
        }

        private void AddPercentSlider(NativeMenu menu, string title, string path, float fallback, string description, float scale = 1f)
        {
            const int steps = 20;

            var item = new NativeSliderItem(title, description, steps, StepFor(path, fallback, scale, steps));
            item.ValueChanged += (sender, args) =>
            {
                if (_refreshing) { return; }
                _config.SetLive(path, JsonValue.Of(item.Value / (double)steps * scale));
            };
            menu.Add(item);
            _refreshers.Add(() => item.Value = StepFor(path, fallback, scale, steps));
        }

        /// <summary>
        /// Clamped, because LemonUI throws when a slider is set past its maximum and this runs
        /// inside the menu constructor. A pedMultiplier of 4 - which the config comments actively
        /// invite - produced step 27 of 20, an ArgumentOutOfRangeException, and a mod that failed
        /// to start at all with no menu to fix it from.
        /// </summary>
        private int StepFor(string path, float fallback, float scale, int steps)
        {
            int step = (int)Math.Round(_config.GetFloat(path, fallback) / scale * steps);
            return Math.Max(0, Math.Min(steps, step));
        }

        private void AddRangeSlider(NativeMenu menu, string title, string path, int fallback, int min, int max, int step, string description)
        {
            int steps = (max - min) / step;
            int current = _config.GetInt(path, fallback);
            int index = Math.Max(0, Math.Min(steps, (current - min) / step));

            var item = new NativeSliderItem(title, description, steps, index);
            item.ValueChanged += (sender, args) =>
            {
                if (_refreshing) { return; }
                _config.SetLive(path, JsonValue.Of(min + item.Value * step));
            };
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

            _refreshing = true;
            try
            {
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
            }
            finally
            {
                _refreshing = false;
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
