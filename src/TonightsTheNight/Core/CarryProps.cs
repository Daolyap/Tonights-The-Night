using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using TonightsTheNight.Config;
using TonightsTheNight.Util;

namespace TonightsTheNight.Core
{
    /// <summary>How a looter holds the thing they have just taken.</summary>
    public enum CarryStyle
    {
        /// <summary>One hand, hanging off the prop-attach bone. Bags, cases, small boxes.</summary>
        Hand,

        /// <summary>Both arms, with the box-carry animation over the top. Televisions, crates.</summary>
        Box
    }

    /// <summary>One carryable thing, and exactly how it sits in somebody's hands.</summary>
    public sealed class CarryProp
    {
        public string Model;
        public CarryStyle Style;
        public Vector3 Offset;
        public Vector3 Rotation;
    }

    /// <summary>
    /// The looting props and how to hold them.
    ///
    /// This is a class rather than an array because the previous version was an array: ten prop
    /// names sharing one hand-tuned offset and rotation. Those numbers were right for one prop
    /// and wrong for the other nine, which is why a looter could be seen sprinting down the
    /// street with a flat-screen television balanced on one fingertip at forty-five degrees.
    ///
    /// A television is not carried the way a carrier bag is, so size decides the pose: small
    /// things hang off the prop-attach bone in one hand, big things get both arms and the
    /// box-carry animation, which is an upper-body secondary so they can still run with it.
    ///
    /// Every offset is config data (<c>features.looting.props</c>) rather than a constant. They
    /// are eyeballed numbers against models nobody documented, so being wrong about one should
    /// cost an edit and a reload, not a rebuild.
    /// </summary>
    public sealed class CarryProps
    {
        /// <summary>PH_R_Hand — the bone the game itself hangs held props off.</summary>
        private const int PropHandBone = 28422;

        /// <summary>Upper body, looping, secondary. Lets a ped carry a box and still walk.</summary>
        private const int CarryAnimFlags = 49;

        private const string CarryAnimDict = "anim@heists@box_carry@";
        private const string CarryAnimName = "idle";

        private readonly ConfigStore _config;
        private readonly ModelResolver _models;
        private readonly Random _random;

        private List<CarryProp> _usable;
        private bool _animRequested;

        public CarryProps(ConfigStore config, ModelResolver models, Random random)
        {
            _config = config;
            _models = models;
            _random = random;
        }

        public void Reset()
        {
            _usable = null;
            _animRequested = false;
        }

        /// <summary>How many of the declared props this install actually has.</summary>
        public int Available { get { return _usable == null ? 0 : _usable.Count; } }

        /// <summary>
        /// Creates a prop and puts it in a ped's hands. Returns null when there is nothing to
        /// carry, which is a perfectly good outcome — the looter simply leaves empty-handed.
        /// </summary>
        public Prop GiveTo(Ped ped)
        {
            if (!_config.GetBool("features.looting.carryProps", true)) { return null; }

            List<CarryProp> choices = Usable();
            if (choices.Count == 0) { return null; }

            CarryProp choice = choices[_random.Next(choices.Count)];

            Model model;
            if (!_models.TryResolve(new[] { choice.Model }, out model)) { return null; }
            if (!_models.Load(model)) { return null; }

            Prop prop = World.CreateProp(model, ped.Position, false, false);
            if (prop == null || !prop.Exists()) { return null; }

            try
            {
                // Ours: attached to a ped's hand and deleted explicitly when the job ends. Left
                // reclaimable it could vanish out of their grip.
                prop.IsPersistent = true;
                Function.Call(Hash.SET_ENTITY_COLLISION, prop, false, false);

                Function.Call(Hash.ATTACH_ENTITY_TO_ENTITY, prop, ped,
                    Function.Call<int>(Hash.GET_PED_BONE_INDEX, ped, PropHandBone),
                    choice.Offset.X, choice.Offset.Y, choice.Offset.Z,
                    choice.Rotation.X, choice.Rotation.Y, choice.Rotation.Z,
                    true, true, false, true, 1, true);

                return prop;
            }
            catch (Exception ex)
            {
                Log.Error("Could not hand a looter '" + choice.Model + "'", ex);

                try { if (prop.Exists()) { prop.Delete(); } }
                catch (Exception) { }

                return null;
            }
        }

        /// <summary>
        /// Plays the box-carry animation over whatever the ped is already doing, for the props
        /// that need both arms. Called after the movement task so it layers rather than replaces.
        ///
        /// Silently does nothing when the animation has not streamed in yet. That is the right
        /// answer: the prop is already in their hands, and a missing animation costs a slightly
        /// odd walk rather than a broken looter.
        /// </summary>
        public void PlayCarryAnimation(Ped ped, Prop prop)
        {
            if (ped == null || !ped.Exists() || prop == null || !prop.Exists()) { return; }
            if (StyleOf(prop) != CarryStyle.Box) { return; }
            if (!EnsureAnimation()) { return; }

            try
            {
                Function.Call(Hash.TASK_PLAY_ANIM, ped, CarryAnimDict, CarryAnimName,
                    8f, -8f, -1, CarryAnimFlags, 0f, false, false, false);
            }
            catch (Exception ex)
            {
                Log.Error("Could not play the carry animation", ex);
            }
        }

        /// <summary>Stops the carry animation when the prop goes, so they walk normally again.</summary>
        public void StopCarryAnimation(Ped ped)
        {
            if (ped == null || !ped.Exists()) { return; }

            try
            {
                Function.Call(Hash.STOP_ANIM_TASK, ped, CarryAnimDict, CarryAnimName, 3f);
            }
            catch (Exception)
            {
                // A ped that has already stopped, died or despawned needs no unwinding.
            }
        }

        private CarryStyle StyleOf(Prop prop)
        {
            List<CarryProp> choices = Usable();
            int model = prop.Model.Hash;

            foreach (CarryProp candidate in choices)
            {
                if (new Model(candidate.Model).Hash == model) { return candidate.Style; }
            }

            return CarryStyle.Hand;
        }

        private bool EnsureAnimation()
        {
            try
            {
                if (Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, CarryAnimDict)) { return true; }

                if (!_animRequested)
                {
                    _animRequested = true;
                    Function.Call(Hash.REQUEST_ANIM_DICT, CarryAnimDict);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not request the carry animation", ex);
            }

            return false;
        }

        /// <summary>
        /// The declared props this install actually has, filtered by the chosen style. Resolved
        /// once per mode, because a model lookup that misses costs a log line.
        /// </summary>
        private List<CarryProp> Usable()
        {
            if (_usable != null) { return _usable; }

            bool smallOnly = string.Equals(
                _config.GetString("features.looting.propStyle", "mixed"), "small", StringComparison.OrdinalIgnoreCase);

            _usable = new List<CarryProp>();
            List<CarryProp> declared = Declared();

            foreach (CarryProp candidate in declared)
            {
                if (smallOnly && candidate.Style != CarryStyle.Hand) { continue; }

                Model model;
                if (!_models.TryResolve(new[] { candidate.Model }, out model)) { continue; }

                _usable.Add(candidate);
            }

            Log.Info("Looting: " + _usable.Count + " of " + declared.Count + " carryable prop(s) available" +
                     (smallOnly ? " (small items only)." : "."));

            return _usable;
        }

        /// <summary>
        /// Reads <c>features.looting.props</c>. Falls back to the shipped list when the setting
        /// is missing or unusable, so a mistyped user.json costs the customisation rather than
        /// the feature.
        /// </summary>
        private List<CarryProp> Declared()
        {
            var result = new List<CarryProp>();

            foreach (JsonValue entry in _config.Resolve("features.looting.props").Items)
            {
                string model = entry["model"].AsString(null);
                if (string.IsNullOrEmpty(model))
                {
                    Log.Warn("A looting prop entry has no 'model'. Skipped.");
                    continue;
                }

                result.Add(new CarryProp
                {
                    Model = model,
                    Style = string.Equals(entry["style"].AsString("hand"), "box", StringComparison.OrdinalIgnoreCase)
                        ? CarryStyle.Box
                        : CarryStyle.Hand,
                    Offset = new Vector3(entry["x"].AsFloat(0f), entry["y"].AsFloat(0f), entry["z"].AsFloat(0f)),
                    Rotation = new Vector3(entry["rx"].AsFloat(0f), entry["ry"].AsFloat(0f), entry["rz"].AsFloat(0f))
                });
            }

            if (result.Count > 0) { return result; }

            Log.Warn("No usable looting props are configured - falling back to the shipped list.");
            return Fallback();
        }

        /// <summary>
        /// The shipped list, duplicated here so the feature survives a broken config. Kept in
        /// step with <c>DefaultConfig</c> by a test.
        /// </summary>
        public static List<CarryProp> Fallback()
        {
            return new List<CarryProp>
            {
                Hand("prop_ld_case_01",     0.13f,  0.02f, -0.02f,   0f,  0f, -100f),
                Hand("prop_cash_case_01",   0.13f,  0.02f, -0.02f,   0f,  0f, -100f),
                Hand("prop_paper_bag_01",   0.10f,  0.01f, -0.05f,  10f,  0f,  -95f),
                Hand("prop_big_bag_01",     0.14f,  0.02f, -0.08f,   5f,  0f, -100f),
                Hand("prop_binbag_01",      0.12f,  0.01f, -0.10f,   0f,  0f,  -95f),
                Hand("prop_cs_shopping_bag",0.10f,  0.01f, -0.06f,   5f,  0f,  -95f),
                Box ("prop_tv_flat_01",     0.025f, 0.12f,  0.24f, -145f, 290f, 0f),
                Box ("prop_cs_box_clothes", 0.025f, 0.08f,  0.255f,-145f, 290f, 0f),
                Box ("prop_cs_cardbox_01",  0.025f, 0.08f,  0.255f,-145f, 290f, 0f),
                Box ("prop_boxpile_07d",    0.025f, 0.08f,  0.255f,-145f, 290f, 0f)
            };
        }

        private static CarryProp Hand(string model, float x, float y, float z, float rx, float ry, float rz)
        {
            return new CarryProp
            {
                Model = model,
                Style = CarryStyle.Hand,
                Offset = new Vector3(x, y, z),
                Rotation = new Vector3(rx, ry, rz)
            };
        }

        private static CarryProp Box(string model, float x, float y, float z, float rx, float ry, float rz)
        {
            return new CarryProp
            {
                Model = model,
                Style = CarryStyle.Box,
                Offset = new Vector3(x, y, z),
                Rotation = new Vector3(rx, ry, rz)
            };
        }
    }
}
