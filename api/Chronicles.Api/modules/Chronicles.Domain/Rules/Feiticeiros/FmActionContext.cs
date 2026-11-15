namespace Chronicles.Domain;

// Explicit facts supplied by the player/GM. The engine never guesses range, weapon kind or a target's state from its name.
public sealed record FmActionContext(string AttackKind = "unspecified", bool Advantage = false, bool Disadvantage = false,
    bool TargetExposed = false, bool TargetUnconscious = false, bool TargetParalyzed = false,
    bool OffhandLight = false, int Concealment = 0, string Ritual = "none", string[]? Improvements = null,
    string RitualFailure = "cancel", string FighterManeuver = "none", string CombatArt = "none",
    bool TargetOffguard = false, bool TargetFlanked = false, bool SneakAttack = false,
    string UnarmedAction = "normal", bool SingleWeaponFreeHand = false, bool DualWielding = false,
    bool WeaponTwoHanded = false, bool WeaponHeavy = false, string[]? SpecialStrike = null, bool SacrificeDamageConfirmed = false,
    bool BoostSelfSufficient = false, string[]? Fundamentals = null, string SpellPowerAttribute = "inteligencia",
    bool SpellSingleTarget = false, bool SpellMelee = false, bool SpellForcesSave = false,
    int IgnoreReduction = 0, bool IgnoreResistance = false, bool TargetEnemy = false, bool UseAdeptDiscount = false);
