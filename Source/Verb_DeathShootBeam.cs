using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Noise;
using Verse.Sound;

namespace FuelElectricityTurretMod
{
    public class Verb_DeathShootBeam : Verb
    {
        private List<Vector3> path = new List<Vector3>();
        private List<Vector3> tmpPath = new List<Vector3>();
        private int ticksToNextPathStep;
        private Vector3 initialTargetPosition;
        private MoteDualAttached mote;
        private Effecter endEffecter;
        private Sustainer sustainer;
        private HashSet<IntVec3> pathCells = new HashSet<IntVec3>();
        private HashSet<IntVec3> tmpPathCells = new HashSet<IntVec3>();
        private HashSet<IntVec3> tmpHighlightCells = new HashSet<IntVec3>();
        private HashSet<IntVec3> tmpSecondaryHighlightCells = new HashSet<IntVec3>();
        private HashSet<IntVec3> hitCells = new HashSet<IntVec3>();
        private const int NumSubdivisionsPerUnitLength = 1;

        protected override int ShotsPerBurst => this.BurstShotCount;

        public float ShotProgress
        {
            get => (float)this.ticksToNextPathStep / (float)this.TicksBetweenBurstShots;
        }

        public Vector3 InterpolatedPosition
        {
            get
            {
                Vector3 vector3 = this.CurrentTarget.CenterVector3 - this.initialTargetPosition;
                return Vector3.Lerp(this.path[this.burstShotsLeft], this.path[Mathf.Min(this.burstShotsLeft + 1, this.path.Count - 1)], this.ShotProgress) + vector3;
            }
        }

        public override float? AimAngleOverride
        {
            get
            {
                return this.state != VerbState.Bursting ? new float?() : new float?((this.InterpolatedPosition - this.caster.DrawPos).AngleFlat());
            }
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            base.DrawHighlight(target);
            this.CalculatePath(target.CenterVector3, this.tmpPath, this.tmpPathCells, false);
            foreach (IntVec3 tmpPathCell in this.tmpPathCells)
            {
                ShootLine resultingLine;
                bool shootLineFromTo = this.TryFindShootLineFromTo(this.caster.Position, target, out resultingLine);
                IntVec3 hitCell;
                if ((!this.verbProps.stopBurstWithoutLos || shootLineFromTo) && this.TryGetHitCell(resultingLine.Source, tmpPathCell, out hitCell))
                {
                    this.tmpHighlightCells.Add(hitCell);
                    if (this.verbProps.beamHitsNeighborCells)
                    {
                        foreach (IntVec3 hitNeighbourCell in this.GetBeamHitNeighbourCells(resultingLine.Source, hitCell))
                        {
                            if (!this.tmpHighlightCells.Contains(hitNeighbourCell))
                                this.tmpSecondaryHighlightCells.Add(hitNeighbourCell);
                        }
                    }
                }
            }
            this.tmpSecondaryHighlightCells.RemoveWhere((Predicate<IntVec3>)(x => this.tmpHighlightCells.Contains(x)));
            Color? nullable;
            if (this.tmpHighlightCells.Any<IntVec3>())
            {
                List<IntVec3> list = this.tmpHighlightCells.ToList<IntVec3>();
                nullable = this.verbProps.highlightColor;
                Color color = nullable ?? Color.white;
                float? altOffset = new float?();
                GenDraw.DrawFieldEdges(list, color, altOffset);
            }
            if (this.tmpSecondaryHighlightCells.Any<IntVec3>())
            {
                List<IntVec3> list = this.tmpSecondaryHighlightCells.ToList<IntVec3>();
                nullable = this.verbProps.secondaryHighlightColor;
                Color color = nullable ?? Color.white;
                float? altOffset = new float?();
                GenDraw.DrawFieldEdges(list, color, altOffset);
            }
            this.tmpHighlightCells.Clear();
            this.tmpSecondaryHighlightCells.Clear();
        }

        protected override bool TryCastShot()
        {
            if (this.currentTarget.HasThing && this.currentTarget.Thing.Map != this.caster.Map)
                return false;
            ShootLine resultingLine;
            bool shootLineFromTo = this.TryFindShootLineFromTo(this.caster.Position, this.currentTarget, out resultingLine);
            if (this.verbProps.stopBurstWithoutLos && !shootLineFromTo)
                return false;
            if (this.EquipmentSource != null)
            {
                this.EquipmentSource.GetComp<CompChangeableProjectile>()?.Notify_ProjectileLaunched();
                this.EquipmentSource.GetComp<CompApparelReloadable>()?.UsedOnce();
            }
            this.lastShotTick = Find.TickManager.TicksGame;
            this.ticksToNextPathStep = this.TicksBetweenBurstShots;
            IntVec3 intVec3 = this.InterpolatedPosition.Yto0().ToIntVec3();
            IntVec3 hitCell;
            if (!this.TryGetHitCell(resultingLine.Source, intVec3, out hitCell))
                return true;
            this.HitCell(hitCell, resultingLine.Source);
            if (this.verbProps.beamHitsNeighborCells)
            {
                this.hitCells.Add(hitCell);
                foreach (IntVec3 hitNeighbourCell in this.GetBeamHitNeighbourCells(resultingLine.Source, hitCell))
                {
                    if (!this.hitCells.Contains(hitNeighbourCell))
                    {
                        float damageFactor = this.pathCells.Contains(hitNeighbourCell) ? 1f : 0.5f;
                        this.HitCell(hitNeighbourCell, resultingLine.Source, damageFactor);
                        this.hitCells.Add(hitNeighbourCell);
                    }
                }
            }
            return true;
        }

        protected bool TryGetHitCell(IntVec3 source, IntVec3 targetCell, out IntVec3 hitCell)
        {
            //this.TrySpawnFakefilthThing(this.verbProps.spawnDef, targetCell, 1, this.caster.Map);
            IntVec3 a = GenSight.LastPointOnLineOfSight(source, targetCell, (Func<IntVec3, bool>)(c => c.InBounds(this.caster.Map) && c.CanBeSeenOverFast(this.caster.Map)), true);
            if (this.verbProps.beamCantHitWithinMinRange && (double)a.DistanceTo(source) < (double)this.verbProps.minRange)
            {
                hitCell = new IntVec3();
                return false;
            }
            hitCell = a.IsValid ? a : targetCell;
            return a.IsValid;
        }

        protected IntVec3 GetHitCell(IntVec3 source, IntVec3 targetCell)
        {
            IntVec3 hitCell;
            this.TryGetHitCell(source, targetCell, out hitCell);
            return hitCell;
        }

        private void TrySpawnFakefilthThing(ThingDef thingDef, IntVec3 c, int count, Map map)
        {
            if (thingDef != null)
            {
                Thing thing;
                if (thingDef.IsFilth)
                {
                    FilthMaker.TryMakeFilth(c, map, thingDef, count);
                }
                else if (GenSpawn.TrySpawn(thingDef, c, map, out thing))
                {
                    thing.stackCount = count;
                    thing.TryGetComp<CompReleaseGas>()?.StartRelease();
                }
            }
        }

        protected IEnumerable<IntVec3> GetBeamHitNeighbourCells(IntVec3 source, IntVec3 pos)
        {
            Verb_DeathShootBeam verbShootBeam = this;

            if (verbShootBeam.verbProps.beamHitsNeighborCells)
            {
                GenExplosion.DoExplosion(
                    center: pos, map: verbShootBeam.Caster.Map, radius:3f, damType:this.verbProps.beamDamageDef, instigator: verbShootBeam.Caster,
                    damAmount: 10, armorPenetration: 0.20f, explosionSound: this.verbProps.soundLanding, applyDamageToExplosionCellsNeighbors: true, chanceToStartFire: 0.7f, damageFalloff: true);
                
                for (int i = 0; i < 4; ++i)
                {
                    IntVec3 hitNeighbourCell = pos + GenAdj.CardinalDirections[i];
                    if (hitNeighbourCell.InBounds(verbShootBeam.Caster.Map) && (!verbShootBeam.verbProps.beamHitsNeighborCellsRequiresLOS ? 1 : (GenSight.LineOfSight(source, hitNeighbourCell, verbShootBeam.caster.Map) ? 1 : 0)) != 0)
                        yield return hitNeighbourCell;
                }
            }
        }

        public override bool TryStartCastOn(
          LocalTargetInfo castTarg,
          LocalTargetInfo destTarg,
          bool surpriseAttack = false,
          bool canHitNonTargetPawns = true,
          bool preventFriendlyFire = false,
          bool nonInterruptingSelfCast = false)
        {
            return base.TryStartCastOn(this.verbProps.beamTargetsGround ? (LocalTargetInfo)castTarg.Cell : castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
        }

        public override void BurstingTick()
        {
            --this.ticksToNextPathStep;
            Vector3 vector3_1 = this.InterpolatedPosition;
            IntVec3 intVec3_1 = vector3_1.ToIntVec3();
            Vector3 vector3_2 = this.InterpolatedPosition - this.caster.Position.ToVector3Shifted();
            float num1 = vector3_2.MagnitudeHorizontal();
            Vector3 normalized = vector3_2.Yto0().normalized;
            IntVec3 intVec3_2 = GenSight.LastPointOnLineOfSight(this.caster.Position, intVec3_1, (Func<IntVec3, bool>)(c => c.CanBeSeenOverFast(this.caster.Map)), true);
            IntVec3 intVec3_3;
            if (intVec3_2.IsValid)
            {
                double num2 = (double)num1;
                intVec3_3 = intVec3_1 - intVec3_2;
                double lengthHorizontal = (double)intVec3_3.LengthHorizontal;
                num1 = (float)(num2 - lengthHorizontal);
                intVec3_3 = this.caster.Position;
                vector3_1 = intVec3_3.ToVector3Shifted() + normalized * num1;
                intVec3_1 = vector3_1.ToIntVec3();
            }
            Vector3 offsetA = normalized * this.verbProps.beamStartOffset;
            Vector3 vector3_3 = vector3_1 - intVec3_1.ToVector3Shifted();
            if (this.mote != null)
            {
                this.mote.UpdateTargets(new TargetInfo(this.caster.Position, this.caster.Map), new TargetInfo(intVec3_1, this.caster.Map), offsetA, vector3_3);
                this.mote.Maintain();
            }
            if (this.verbProps.beamGroundFleckDef != null && Rand.Chance(this.verbProps.beamFleckChancePerTick))
                FleckMaker.Static(vector3_1, this.caster.Map, this.verbProps.beamGroundFleckDef);
            if (this.endEffecter == null && this.verbProps.beamEndEffecterDef != null)
                this.endEffecter = this.verbProps.beamEndEffecterDef.Spawn(intVec3_1, this.caster.Map, vector3_3);
            if (this.endEffecter != null)
            {
                this.endEffecter.offset = vector3_3;
                this.endEffecter.EffectTick(new TargetInfo(intVec3_1, this.caster.Map), TargetInfo.Invalid);
                --this.endEffecter.ticksLeft;
            }
            if (this.verbProps.beamLineFleckDef != null)
            {
                float num3 = 1f * num1;
                for (int index = 0; (double)index < (double)num3; ++index)
                {
                    if (Rand.Chance(this.verbProps.beamLineFleckChanceCurve.Evaluate((float)index / num3)))
                    {
                        Vector3 vector3_4 = (float)index * normalized - normalized * Rand.Value + normalized / 2f;
                        intVec3_3 = this.caster.Position;
                        FleckMaker.Static(intVec3_3.ToVector3Shifted() + vector3_4, this.caster.Map, this.verbProps.beamLineFleckDef);
                    }
                }
            }
            this.sustainer?.Maintain();
        }

        public override void WarmupComplete()
        {
            this.burstShotsLeft = this.ShotsPerBurst;
            this.state = VerbState.Bursting;
            this.initialTargetPosition = this.currentTarget.CenterVector3;
            this.CalculatePath(this.currentTarget.CenterVector3, this.path, this.pathCells);
            this.hitCells.Clear();
            if (this.verbProps.beamMoteDef != null)
                this.mote = MoteMaker.MakeInteractionOverlay(this.verbProps.beamMoteDef, (TargetInfo)this.caster, new TargetInfo(this.path[0].ToIntVec3(), this.caster.Map));
            this.TryCastNextBurstShot();
            this.ticksToNextPathStep = this.TicksBetweenBurstShots;
            this.endEffecter?.Cleanup();
            if (this.verbProps.soundCastBeam == null)
                return;
            this.sustainer = this.verbProps.soundCastBeam.TrySpawnSustainer(SoundInfo.InMap((TargetInfo)this.caster, MaintenanceType.PerTick));
        }

        private void CalculatePath(
          Vector3 target,
          List<Vector3> pathList,
          HashSet<IntVec3> pathCellsList,
          bool addRandomOffset = true)
        {
            pathList.Clear();
            Vector3 vector3_1 = (target - this.caster.Position.ToVector3Shifted()).Yto0();
            float magnitude = vector3_1.magnitude;
            Vector3 normalized = vector3_1.normalized;
            Vector3 vector3_2 = normalized.RotatedBy(-90f);
            float num1 = (double)this.verbProps.beamFullWidthRange > 0.0 ? Mathf.Min(magnitude / this.verbProps.beamFullWidthRange, 1f) : 1f;
            float num2 = (this.verbProps.beamWidth + 1f) * num1 / (float)this.ShotsPerBurst;
            Vector3 vector3_3 = target.Yto0() - vector3_2 * this.verbProps.beamWidth / 2f * num1;
            pathList.Add(vector3_3);
            for (int index = 0; index < this.ShotsPerBurst; ++index)
            {
                Vector3 vector3_4 = normalized * (Rand.Value * this.verbProps.beamMaxDeviation) - normalized / 2f;
                Vector3 vector3_5 = Mathf.Sin((float)(((double)index / (double)this.ShotsPerBurst + 0.5) * 3.1415927410125732 * 57.295780181884766)) * this.verbProps.beamCurvature * -normalized - normalized * this.verbProps.beamMaxDeviation / 2f;
                if (addRandomOffset)
                    pathList.Add(vector3_3 + (vector3_4 + vector3_5) * num1);
                else
                    pathList.Add(vector3_3 + vector3_5 * num1);
                vector3_3 += vector3_2 * num2;
            }
            pathCellsList.Clear();
            foreach (Vector3 path in pathList)
                pathCellsList.Add(path.ToIntVec3());
        }

        private bool CanHit(Thing thing)
        {
            return thing.Spawned && !CoverUtility.ThingCovered(thing, this.caster.Map);
        }

        private void HitCell(IntVec3 cell, IntVec3 sourceCell, float damageFactor = 1f)
        {
            if (!cell.InBounds(this.caster.Map))
                return;
            this.ApplyDamage(VerbUtility.ThingsToHit(cell, this.caster.Map, new Func<Thing, bool>(this.CanHit)).RandomElementWithFallback<Thing>(), sourceCell, damageFactor);

            this.TrySpawnFakefilthThing(this.verbProps.spawnDef, cell, 1, this.caster.Map);

            if (!this.verbProps.beamSetsGroundOnFire || !Rand.Chance(this.verbProps.beamChanceToStartFire))
                return;
            FireUtility.TryStartFireIn(cell, this.caster.Map, 1f, this.caster);
        }

        private void ApplyDamage(Thing thing, IntVec3 sourceCell, float damageFactor = 1f)
        {
            IntVec3 intVec3_1 = this.InterpolatedPosition.Yto0().ToIntVec3();
            IntVec3 intVec3_2 = GenSight.LastPointOnLineOfSight(sourceCell, intVec3_1, (Func<IntVec3, bool>)(c => c.InBounds(this.caster.Map) && c.CanBeSeenOverFast(this.caster.Map)), true);
            if (intVec3_2.IsValid)
                intVec3_1 = intVec3_2;
            Map map = this.caster.Map;
            if (thing == null || this.verbProps.beamDamageDef == null)
                return;
            float angleFlat = (this.currentTarget.Cell - this.caster.Position).AngleFlat;
            BattleLogEntry_RangedImpact log = new BattleLogEntry_RangedImpact(this.caster, thing, this.currentTarget.Thing, this.EquipmentSource.def, (ThingDef)null, (ThingDef)null);
            DamageInfo dinfo = (double)this.verbProps.beamTotalDamage <= 0.0 ? new DamageInfo(this.verbProps.beamDamageDef, (float)this.verbProps.beamDamageDef.defaultDamage * damageFactor, this.verbProps.beamDamageDef.defaultArmorPenetration, angleFlat, this.caster, weapon: this.EquipmentSource.def, intendedTarget: this.currentTarget.Thing) : new DamageInfo(this.verbProps.beamDamageDef, this.verbProps.beamTotalDamage / (float)this.pathCells.Count * damageFactor, this.verbProps.beamDamageDef.defaultArmorPenetration, angleFlat, this.caster, weapon: this.EquipmentSource.def, intendedTarget: this.currentTarget.Thing);
            thing.TakeDamage(dinfo).AssociateWithLog((LogEntry_DamageResult)log);
            if (thing.CanEverAttachFire())
            {
                if (!Rand.Chance(this.verbProps.flammabilityAttachFireChanceCurve == null ? this.verbProps.beamChanceToAttachFire : this.verbProps.flammabilityAttachFireChanceCurve.Evaluate(thing.GetStatValue(StatDefOf.Flammability))))
                    return;
                thing.TryAttachFire(this.verbProps.beamFireSizeRange.RandomInRange, this.caster);
            }
            else
            {
                if (!Rand.Chance(this.verbProps.beamChanceToStartFire))
                    return;
                FireUtility.TryStartFireIn(intVec3_1, map, this.verbProps.beamFireSizeRange.RandomInRange, this.caster, this.verbProps.flammabilityAttachFireChanceCurve);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look<Vector3>(ref this.path, "path", LookMode.Value);
            Scribe_Values.Look<int>(ref this.ticksToNextPathStep, "ticksToNextPathStep");
            Scribe_Values.Look<Vector3>(ref this.initialTargetPosition, "initialTargetPosition");
            if (Scribe.mode != LoadSaveMode.PostLoadInit || this.path != null)
                return;
            this.path = new List<Vector3>();
        }
    }

}
