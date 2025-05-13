using System.Linq;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Content.Shared.EntityEffects;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Alert;
using Content.Shared.Mobs.Systems;

namespace Content.Shared._RMC14.Medical.Pain;

public sealed partial class PainSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";
    private static readonly ProtoId<DamageGroupPrototype> ToxinGroup = "Toxin";
    private static readonly ProtoId<DamageGroupPrototype> AirlossGroup = "Airloss";

    private readonly HashSet<ProtoId<DamageTypePrototype>> _bruteTypes = new();
    private readonly HashSet<ProtoId<DamageTypePrototype>> _burnTypes = new();
    private readonly HashSet<ProtoId<DamageTypePrototype>> _toxinTypes = new();
    private readonly HashSet<ProtoId<DamageTypePrototype>> _airlossTypes = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<PainComponent, DamageChangedEvent>(OnDamageChanged);

        _bruteTypes.Clear();
        _burnTypes.Clear();
        _toxinTypes.Clear();
        _airlossTypes.Clear();

        if (_prototypes.TryIndex(BruteGroup, out var bruteProto))
        {
            foreach (var type in bruteProto.DamageTypes)
            {
                _bruteTypes.Add(type);
            }
        }

        if (_prototypes.TryIndex(BurnGroup, out var burnProto))
        {
            foreach (var type in burnProto.DamageTypes)
            {
                _burnTypes.Add(type);
            }
        }

        if (_prototypes.TryIndex(ToxinGroup, out var toxinProto))
        {
            foreach (var type in toxinProto.DamageTypes)
            {
                _toxinTypes.Add(type);
            }
        }

        if (_prototypes.TryIndex(AirlossGroup, out var airlossProto))
        {
            foreach (var type in airlossProto.DamageTypes)
            {
                _airlossTypes.Add(type);
            }
        }
    }

    private void OnDamageChanged(EntityUid uid, PainComponent comp, ref DamageChangedEvent args)
    {
        var damageDict = args.Damageable.Damage.DamageDict;
        UpdateCurrentPain(comp, damageDict);
        UpdateCurrentPainPercentage(comp);
        Dirty(uid, comp);
    }

    public void TryChangePainLevelTo(EntityUid uid, int level, PainComponent? pain = null)
    {
        if (!Resolve(uid, ref pain))
            return;

        if (pain.LastPainLevelUpdateTime + pain.EffectUpdateRate > _timing.CurTime)
            return;

        pain.LastPainLevelUpdateTime = _timing.CurTime;
        pain.CurrentPainLevel += level.CompareTo(pain.CurrentPainLevel);

        _alerts.ShowAlert(uid, pain.Alert, (short)pain.CurrentPainLevel);
    }
    public void AddPainModificator(EntityUid uid, TimeSpan duration, FixedPoint2 effectStrength, PainModificatorType type, PainComponent? pain = null)
    {
        var mod = new PainModificator(duration, effectStrength, type);
        AddPainModificator(uid, mod, pain);
    }

    public void AddPainModificator(EntityUid uid, PainModificator mod, PainComponent? pain = null)
    {
        if (!Resolve(uid, ref pain))
            return;

        pain.PainModificators.Add(mod);
        UpdateCurrentPainPercentage(pain);
        Dirty(uid, pain);

        Timer.Spawn(mod.Duration, () => RemovePainModificator(uid, mod, pain));
    }

    private void RemovePainModificator(EntityUid uid, PainModificator mod, PainComponent? pain = null)
    {
        if (!Resolve(uid, ref pain))
            return;

        pain.PainModificators.Remove(mod);
        UpdateCurrentPainPercentage(pain);
        Dirty(uid, pain);
    }
    private void UpdateCurrentPainPercentage(PainComponent comp)
    {
        var maxPainReductionModificatorStrength = FixedPoint2.Zero;
        var painIncrease = FixedPoint2.Zero;
        var painIncreases = comp.PainModificators.Where(mod => mod.Type == PainModificatorType.PainIncrease);
        var painReductions = comp.PainModificators.Where(mod => mod.Type == PainModificatorType.PainReduction);
        // get max pain reduction, sum pain increase
        if (painIncreases.Any())
            painIncrease = painIncreases.Select(mod => mod.EffectStrength).Sum();
        if (painReductions.Any())
            maxPainReductionModificatorStrength = painReductions.Max(mod => mod.EffectStrength);

        var realCurrentPain = comp.CurrentPain + painIncrease;
        // Pain reduction effectiveness linear decreases as the pain goes up
        var newPainReduction = FixedPoint2.Max(0, -realCurrentPain * comp.PainReductionDecreaceRate + maxPainReductionModificatorStrength);
        comp.CurrentPainPercentage = FixedPoint2.Clamp(realCurrentPain - newPainReduction, 0, 100);
    }

    private void UpdateCurrentPain(PainComponent comp, Dictionary<string, FixedPoint2> damageDict)
    {
        var newCurrentPain = FixedPoint2.Zero;
        foreach (var (type, _) in damageDict)
        {
            if (_bruteTypes.Contains(type))
            {
                newCurrentPain += comp.BrutePainMultiplier * damageDict[type];
            }

            if (_burnTypes.Contains(type))
            {
                newCurrentPain += comp.BurnPainMultiplier * damageDict[type];
            }

            if (_toxinTypes.Contains(type))
            {
                newCurrentPain += comp.ToxinPainMultiplier * damageDict[type];
            }

            if (_airlossTypes.Contains(type))
            {
                newCurrentPain += comp.AirlossPainMultiplier * damageDict[type];
            }
        }

        comp.CurrentPain = newCurrentPain;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PainComponent>();
        while (query.MoveNext(out var uid, out var pain))
        {
            if (_mobState.IsDead(uid))
                continue;

            if (_timing.CurTime < pain.NextEffectUpdateTime)
                continue;

            pain.NextEffectUpdateTime = _timing.CurTime + pain.EffectUpdateRate;

            var args = new EntityEffectBaseArgs(uid, EntityManager);
            foreach (var effect in pain.PainLevels)
            {
                if (!effect.ShouldApply(args, _random))
                    continue;

                effect.Effect(args);
            }
        }
    }
}
