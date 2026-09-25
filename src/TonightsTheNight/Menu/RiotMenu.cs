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
        private NativeMenu _hunterMenu;
        private NativeMenu _powerMenu;
        private NativeMenu _loadoutMenu;
        private NativeMenu _pickerMenu;
        private NativeItem _stopItem;
        private NativeItem _phaseItem;
        private int _profileSlot = 1;

        /// <summary>Weight given to the next weapon added to the custom loadout.</summary>
        private int _nextWeight = 1;

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
        /// Two things here were wrong and both were visible in every screenshot anyone took.
        ///
        /// The banner was a <c>ScaledRectangle</c> created with an empty size. LemonUI resizes a
        /// banner on recalculate, but a flat rectangle has no texture behind it, so anywhere the
        /// recalculate had not landed yet the header was simply the game showing through — a
        /// title floating over traffic. It is a real texture now, tinted, which is both solid
        /// and what every other menu in the game does.
        ///
        /// The title was set in HouseScript, which is the handwritten Los Santos font. Its
        /// glyphs are far taller than the Chalet faces LemonUI sizes its header against, so at
        /// the default banner scale the title overhung the subtitle bar and the first two items.
        /// That is the text lying across "Riot Modes" in every screenshot. The scale is a
        /// setting now and the default font is one that fits.
        /// </summary>
        private void Style(NativeMenu menu)
        {
            try
            {
                menu.ItemCount = CountVisibility.Always;
                menu.MaxItems = Math.Max(4, _config.GetInt("menu.maxItems", 9));
                menu.Width = Math.Max(300f, _config.GetFloat("menu.width", 460f));
                // NameFont is the subtitle bar and DescriptionFont the panel underneath; the
                // banner's own font is BannerText.Font, set below.
                menu.NameFont = GTA.UI.Font.ChaletComprimeCologne;
                menu.DescriptionFont = GTA.UI.Font.ChaletLondon;

                if (_config.GetBool("menu.lightweight", false))
                {
                    menu.Banner = null;
                    menu.Buttons.Clear();
                    menu.MouseBehavior = MenuMouseBehavior.Disabled;
                    menu.RotateCamera = false;
                    return;
                }

                menu.Banner = BuildBanner();
                StyleBannerText(menu);
            }
            catch (Exception ex)
            {
                Log.Error("Could not style a menu", ex);
            }
        }

        /// <summary>
        /// texture | flat | none.
        ///
        /// The texture is the default because it is the only one that cannot end up transparent:
        /// it is a base-game asset every install has, and tinting it gives the same flat colour
        /// the rectangle was reaching for with none of the ways that went wrong.
        /// </summary>
        private I2Dimensional BuildBanner()
        {
            string style = _config.GetString("menu.bannerStyle", "texture");
            Color colour = Colour("menu.bannerColour", Color.FromArgb(235, 132, 22, 22));

            if (string.Equals(style, "none", StringComparison.OrdinalIgnoreCase)) { return null; }

            if (string.Equals(style, "flat", StringComparison.OrdinalIgnoreCase))
            {
                return new ScaledRectangle(PointF.Empty, new SizeF(_config.GetFloat("menu.width", 460f), 108f))
                {
                    Color = colour
                };
            }

            return new ScaledTexture(PointF.Empty, new SizeF(_config.GetFloat("menu.width", 460f), 108f),
                "commonmenu", "interaction_bgd")
            {
                Color = colour
            };
        }

        private void StyleBannerText(NativeMenu menu)
        {
            if (menu.BannerText == null) { return; }

            GTA.UI.Font font;
            if (!Enum.TryParse(_config.GetString("menu.titleFont", "ChaletComprimeCologne"), true, out font))
            {
                font = GTA.UI.Font.ChaletComprimeCologne;
            }

            menu.BannerText.Font = font;
            // Clamped rather than trusted. A title that overhangs the items is exactly the bug
            // being fixed here, and it should not be reachable by typing a number into a file.
            menu.BannerText.Scale = Math.Max(0.4f, Math.Min(1.2f, _config.GetFloat("menu.titleScale", 0.95f)));
            menu.BannerText.Color = Colour("menu.titleColour", Color.White);
            menu.BannerText.Outline = false;
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
            BuildPowerMenu();
            BuildHunterMenu();
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
            return AddSubMenu(_root, submenu, title, description);
        }

        /// <summary>The same, for a page that hangs off another page rather than off the root.</summary>
        private NativeSubmenuItem AddSubMenu(NativeMenu parent, NativeMenu submenu, string title, string description)
        {
            Style(submenu);
            NativeSubmenuItem item = parent.AddSubMenu(submenu);
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

            AddPercentSlider(_tuningMenu, "Intensity", "riot.intensity", 1f,
                "How much of everything. Scales every spawning faction's wave size and ceiling and "
                + "how often they arrive. Turn it down if a mode is overwhelming you; turn it up if "
                + "nothing is. Nothing else here has this much effect.", 2f);

            AddRangeSlider(_tuningMenu, "How Many Can Fight You", "combat.maxPlayerAttackers", 4, 0, 16, 1,
                "How many of them may be in a combat task against you at once. Hostility is a "
                + "property of a group and a group has no size, so without this every soldier who "
                + "could see you was individually trying to kill you. 0 is no limit.");

            AddPercentSlider(_tuningMenu, "Heavy Weapons", "combat.heavyWeaponChance", 0.35f,
                "Rockets, launchers, miniguns and machine guns, as a share of the picks that landed "
                + "on one. A loadout weight is a share of a faction, not a rarity - an RPG at weight "
                + "1 against a 3 arms a quarter of them. 100% honours every loadout as written.");

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

            BuildLoadoutMenu();
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

            AddRangeSlider(_drivingMenu, "Cars Hunting You At Once", "vehicles.maxHuntingPlayer", 1, 0, 8, 1,
                "The weights above are each driver's own decision and say nothing about how many "
                + "drivers have already made it. Without a ceiling, every car on the street rolling "
                + "independently converges on you from four directions.");

            AddToggle(_drivingMenu, "Ram Instead Of Chase", "vehicles.ram", false,
                "On: drivers going after someone drive into them rather than following. Off, a "
                + "driver who is after you tails you and their passengers lean out - a vehicle "
                + "chase task against somebody on foot is resolved with the bumper.");
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

            AddPicker(_lootMenu, "What They Carry", "features.looting.propStyle",
                new[] { "Anything", "Small Items Only" }, new[] { "mixed", "small" },
                "Anything: big items get both arms and the box-carry animation, small ones hang off "
                + "one hand. Small Items Only: bags and cases, for anyone who would rather nobody "
                + "ran past holding a television.");

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

            AddToggle(_featuresMenu, "Stop When You Die", "riot.stopOnPlayerDeath", true,
                "A riot is something that happened to you, and dying ends your part in it. Off, it "
                + "carries on through the respawn - you get up in a hospital in a city that is "
                + "still on fire, which is a legitimate thing to want and a poor default.");

            AddToggle(_featuresMenu, "Purge Timer", "features.purge.enabled", true,
                "The countdown, the final warning and the siren that ends it. Off makes The Purge "
                + "run indefinitely, which is a mode of its own but not the one it says it is.");

            AddToggle(_featuresMenu, "Purge Countdown On Screen", "features.purge.showTimer", true,
                "The clock, top centre. The most direct available answer to 'does this ever end'.");

            AddRangeSlider(_featuresMenu, "Purge Wind-Down", "features.purge.windDownSeconds", 25, 0, 90, 5,
                "Seconds after the siren before the mode lets go. Nothing new arrives and what is "
                + "out there is talked down, so the event ends rather than being cut.");

            AddToggle(_featuresMenu, "Wave Counter On Screen", "features.manhunt.showWave", true,
                "Which wave you are on during Car Chase, and how many are currently after you.");

            AddToggle(_featuresMenu, "Contagion", "features.contagion.enabled", true,
                "Whether an infection can spread from person to person. Off, Patient Zero is an "
                + "ordinary riot between a few infected and everybody else.");

            AddToggle(_featuresMenu, "No Wanted Level During Purge", "features.purge.noWantedLevel", true,
                "All crime is legal, so the game's own police lose interest. Your wanted ceiling is " +
                "put back exactly as it was when the purge ends.");

            AddToggle(_featuresMenu, "Barricades", "features.spectacle.barricades", true,
                "Rioters drag street furniture across the roads and set it alight. Cars can still "
                + "smash through - that is the point of them being props rather than walls.");

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

        /// <summary>
        /// The city's power, which is the single largest change this mod can make to how a
        /// street looks for one native call.
        /// </summary>
        private void BuildPowerMenu()
        {
            _powerMenu = new NativeMenu("Tonight's The Night", "BLACKOUT & POWER");
            _pool.Add(_powerMenu);
            AddSubMenu(_powerMenu, "Blackout & Power", "When the lights go out, and how far it reaches.");

            AddToggle(_powerMenu, "Blackout", "features.spectacle.blackout", true,
                "Street lights, shop signs and window light across the whole map. At night the "
                + "difference is total.");

            AddPicker(_powerMenu, "When", "features.spectacle.blackoutWhen",
                new[] { "Never", "When The Mode Asks", "From The Start" },
                new[] { "never", "phase", "always" },
                "When The Mode Asks waits for a riot to escalate far enough that its author "
                + "declared a blackout - which for most modes is three quarters of the way in. "
                + "From The Start makes it the premise instead.");

            AddToggle(_powerMenu, "Flicker", "features.spectacle.blackoutFlicker", true,
                "The grid coming back for half a second and going again. Most of the difference "
                + "between 'the lights are off' and 'something is wrong with the city'.");

            AddRangeSlider(_powerMenu, "Seconds Between Flickers", "features.spectacle.blackoutFlickerMs",
                9000, 2000, 30000, 1000,
                "Roughly. Each one is jittered so it does not read as a metronome.");

            AddToggle(_powerMenu, "Headlights Too", "features.spectacle.blackoutVehicles", false,
                "On: vehicle lights go out with everything else. Total darkness, and almost "
                + "unplayable at night, which is either the point or a reason to leave this off.");

            AddToggle(_powerMenu, "Smoke Columns", "features.spectacle.smoke", true,
                "Smoke rising off the burning barricades, visible from the other side of the map.");

            AddToggle(_powerMenu, "Helicopter Searchlights", "features.air.searchlight", true,
                "Sweeping a blacked-out street is most of what a helicopter is for.");
        }

        /// <summary>
        /// The one mode that is a fight rather than a riot. Every number here changes the shape
        /// of that fight, so they are all in one place with what they cost written next to them.
        /// </summary>
        private void BuildHunterMenu()
        {
            _hunterMenu = new NativeMenu("Tonight's The Night", "THE HUNTER");
            _pool.Add(_hunterMenu);
            AddSubMenu(_hunterMenu, "The Hunter", "The thing in Tonight's The Night, and how hard it is.");

            AddToggle(_hunterMenu, "Hunter", "features.hunter.enabled", true,
                "Off: Tonight's The Night runs as an ordinary night with nothing hunting you, "
                + "which is not much of a mode.");

            AddRangeSlider(_hunterMenu, "Resolve", "features.hunter.resolve", 2400, 600, 6000, 200,
                "The pool that actually has to be emptied. Its real health is pinned and means "
                + "nothing; this is the fight's length.");

            AddPercentSlider(_hunterMenu, "Damage While Armoured", "features.hunter.armouredMultiplier", 0.2f,
                "What ordinary hits are worth outside a stagger. Low is the point: it is what "
                + "makes the openings matter rather than being optional.");

            AddPercentSlider(_hunterMenu, "Damage While Exposed", "features.hunter.vulnerableMultiplier", 1f,
                "What hits are worth during a stagger. This is where the fight is won.");

            AddRangeSlider(_hunterMenu, "Sustained Fire To Stagger", "features.hunter.breakThreshold",
                900, 200, 3000, 100,
                "Raw damage needed to break through and force an opening. The route for a player "
                + "with good aim and no explosives. Lower is kinder.");

            AddToggle(_hunterMenu, "Time Slows When It Moves", "features.hunter.timeSlow", true,
                "Everything else slowing down while it does not. Brief, rare and self-restoring - "
                + "but it is a global, so here is the switch.");

            AddToggle(_hunterMenu, "It Kills What It Passes", "features.hunter.cull", true,
                "Anybody standing near it stops standing near it. Rate-limited hard: the point is "
                + "a trail behind it rather than an empty district.");

            AddToggle(_hunterMenu, "Show Its Health", "features.hunter.showBar", true,
                "The bar, the percentage and the range. Off makes the fight considerably harder to "
                + "read, which is a legitimate way to play it.");

            AddToggle(_hunterMenu, "Blip It", "features.hunter.blip", true,
                "A flashing marker on the minimap. Off and you have to listen for it.");

            AddRangeSlider(_hunterMenu, "How Far It Lets You Get", "features.hunter.leashDistance",
                220, 80, 600, 20,
                "Past this it stops walking and starts arriving. Losing it is allowed; losing it "
                + "permanently is not.");
        }

        /// <summary>
        /// Building a loadout without leaving the game.
        ///
        /// This used to be a menu item that explained how to write a JSON array into user.json,
        /// which is a fine thing to offer somebody already editing config and a useless thing to
        /// offer anybody on a controller. The setting is identical - it is still weapons.custom
        /// in the live config layer - so a loadout built here can still be saved to a profile,
        /// read back from a file, or hand-written by anyone who prefers to.
        /// </summary>
        private void BuildLoadoutMenu()
        {
            _loadoutMenu = new NativeMenu("Tonight's The Night", "CUSTOM LOADOUT");
            _pool.Add(_loadoutMenu);
            AddSubMenu(_weaponMenu, _loadoutMenu, "Custom Loadout",
                "Build your own list of what the crowd is carrying, weapon by weapon.");

            _pickerMenu = new NativeMenu("Tonight's The Night", "ADD A WEAPON");
            _pool.Add(_pickerMenu);
            Style(_pickerMenu);

            foreach (WeaponCategory category in WeaponCatalog.Categories)
            {
                var categoryMenu = new NativeMenu("Tonight's The Night", category.Name.ToUpperInvariant());
                _pool.Add(categoryMenu);

                AddSubMenu(_pickerMenu, categoryMenu, category.Name, "Add one of these to the loadout.");

                foreach (CatalogWeapon weapon in category.Weapons)
                {
                    CatalogWeapon captured = weapon;
                    var item = new NativeItem(weapon.Label, "Add " + weapon.Label + " to the custom loadout.");
                    item.Activated += (sender, args) => AddToLoadout(captured.Name);
                    categoryMenu.Add(item);
                }
            }

            // Built once. Only the weapon rows are rebuilt when the loadout changes, because
            // the sliders below register themselves with the refresher list - rebuilding the
            // whole page every time a weapon was added would grow that list without bound and
            // leave it pointing at controls no longer on screen.
            var use = new NativeItem("Use This Loadout",
                "Switches the weapon preset to Custom. Without this the list is built but not used.");
            use.Activated += (sender, args) =>
            {
                _config.SetLive("weapons.preset", JsonValue.Of("custom"));
                WeaponPresets.Reset();
                GTA.UI.Notification.Show("~g~Custom loadout~s~ is now in use.");
                Rebuild();
            };
            _loadoutMenu.Add(use);

            AddSubMenu(_loadoutMenu, _pickerMenu, "Add A Weapon", "Base-game weapons, by category.");

            var weight = new NativeSliderItem("Weight For The Next One",
                "How common the next weapon you add is, relative to the others. Four is four times "
                + "as likely as one.", 10, Math.Max(0, _nextWeight - 1));
            weight.ValueChanged += (sender, args) => _nextWeight = weight.Value + 1;
            _loadoutMenu.Add(weight);

            AddRangeSlider(_loadoutMenu, "Ammunition", "weapons.customAmmo", 120, 0, 500, 20,
                "Rounds handed out with the weapon. Melee ignores it.");

            AddRangeSlider(_loadoutMenu, "Body Armour", "weapons.customArmour", 0, 0, 100, 5,
                "How much punishment they take before it starts counting. Part of the loadout, "
                + "because 'armed and armoured' is a different event from 'armed'.");

            AddPercentSlider(_loadoutMenu, "How Many Are Armed", "weapons.customArmedChance", 1f,
                "The rest go out empty-handed and fight anyway.");

            var clear = new NativeItem("Clear The Loadout", "Removes every weapon from the list.");
            clear.Activated += (sender, args) =>
            {
                _config.SetLive("weapons.custom", JsonValue.NewArray());
                WeaponPresets.Reset();
                PopulateLoadout();
            };
            _loadoutMenu.Add(clear);

            _loadoutMenu.Add(new NativeItem("Keeping It",
                "A loadout lives in the live settings layer, so it lasts the session. Save it to a "
                + "profile slot to keep it, or copy weapons.custom into user.json by hand."));

            PopulateLoadout();
        }

        /// <summary>Marks the rows this page rebuilds, as opposed to the controls it does not.</summary>
        private const string LoadoutRow = "loadout-entry";

        /// <summary>Where the weapon rows sit: after Use, Add A Weapon and the weight slider.</summary>
        private const int LoadoutRowStart = 3;

        /// <summary>
        /// Rebuilt from the config every time it changes, rather than kept in a parallel list.
        /// The config is the loadout; anything else here would be a second copy to disagree with.
        /// </summary>
        private void PopulateLoadout()
        {
            if (_loadoutMenu == null) { return; }

            _loadoutMenu.Remove(item => item.Tag as string == LoadoutRow);

            List<JsonValue> entries = Loadout();
            int at = LoadoutRowStart;

            if (entries.Count == 0)
            {
                var empty = new NativeItem("Nothing In It Yet",
                    "Add a weapon above. An empty custom loadout falls back to each mode's own.");
                empty.Tag = LoadoutRow;
                _loadoutMenu.Add(at, empty);
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                int index = i;
                string name = entries[i]["name"].AsString("?");
                double entryWeight = entries[i]["weight"].AsDouble(1);

                var item = new NativeItem(WeaponCatalog.LabelFor(name),
                    "Weight " + entryWeight + ". Select to remove it from the loadout.",
                    "x" + entryWeight);

                item.Tag = LoadoutRow;
                item.Activated += (sender, args) => RemoveFromLoadout(index);
                _loadoutMenu.Add(at++, item);
            }
        }

        /// <summary>The current custom loadout, normalised so every entry has a name and a weight.</summary>
        private List<JsonValue> Loadout()
        {
            var entries = new List<JsonValue>();

            foreach (JsonValue entry in _config.Resolve("weapons.custom").Items)
            {
                JsonValue normalised = JsonValue.NewObject();

                if (entry.IsObject)
                {
                    string name = entry["name"].AsString(null);
                    if (name == null) { continue; }

                    normalised.Set("name", JsonValue.Of(name));
                    normalised.Set("weight", JsonValue.Of(entry["weight"].AsDouble(1)));
                }
                else
                {
                    string name = entry.AsString(null);
                    if (name == null) { continue; }

                    normalised.Set("name", JsonValue.Of(name));
                    normalised.Set("weight", JsonValue.Of(1));
                }

                entries.Add(normalised);
            }

            return entries;
        }

        private void AddToLoadout(string gameName)
        {
            List<JsonValue> entries = Loadout();

            foreach (JsonValue entry in entries)
            {
                if (!string.Equals(entry["name"].AsString(""), gameName, StringComparison.OrdinalIgnoreCase)) { continue; }

                // Already there: raise its weight rather than listing it twice, which the picker
                // would otherwise make very easy to do by accident.
                entry.Set("weight", JsonValue.Of(entry["weight"].AsDouble(1) + _nextWeight));
                Commit(entries);
                GTA.UI.Notification.Show("~g~" + WeaponCatalog.LabelFor(gameName) + "~s~ is now weight " +
                                         entry["weight"].AsDouble(1) + ".");
                return;
            }

            JsonValue added = JsonValue.NewObject();
            added.Set("name", JsonValue.Of(gameName));
            added.Set("weight", JsonValue.Of(_nextWeight));
            entries.Add(added);

            Commit(entries);
            GTA.UI.Notification.Show("~g~Added~s~ " + WeaponCatalog.LabelFor(gameName) + ".");
        }

        private void RemoveFromLoadout(int index)
        {
            List<JsonValue> entries = Loadout();
            if (index < 0 || index >= entries.Count) { return; }

            entries.RemoveAt(index);
            Commit(entries);
        }

        private void Commit(List<JsonValue> entries)
        {
            JsonValue list = JsonValue.NewArray();
            foreach (JsonValue entry in entries) { list.Add(entry); }

            _config.SetLive("weapons.custom", list);
            // The preset caches its table, so without this the next recruit is armed from the
            // list as it was before the edit.
            WeaponPresets.Reset();
            PopulateLoadout();
        }

        /// <summary>A list of labelled choices writing one of a fixed set of values to config.</summary>
        private void AddPicker(NativeMenu menu, string title, string path, string[] labels, string[] values, string description)
        {
            var item = new NativeListItem<string>(title, description, labels);
            item.SelectedIndex = Math.Max(0, Array.IndexOf(values, _config.GetString(path, values[0])));

            item.ItemChanged += (sender, args) =>
            {
                if (_refreshing) { return; }
                _config.SetLive(path, JsonValue.Of(values[item.SelectedIndex]));
            };

            menu.Add(item);
            _refreshers.Add(() =>
                item.SelectedIndex = Math.Max(0, Array.IndexOf(values, _config.GetString(path, values[0]))));
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
            PopulateLoadout();

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
