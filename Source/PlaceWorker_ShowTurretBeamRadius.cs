using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace FuelElectricityTurretMod
{
    public class PlaceWorker_ShowTurretBeamRadius : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(
            BuildableDef checkingDef,
            IntVec3 loc,
            Rot4 rot,
            Map map,
            Thing thingToIgnore = null,
            Thing thing = null)
        {
            VerbProperties verbProperties = ((ThingDef)checkingDef).building.turretGunDef.Verbs.Find((Predicate<VerbProperties>)(v => v.verbClass == typeof(Verb_ShootBeam)));
            if ((double)verbProperties.range > 0.0)
                GenDraw.DrawRadiusRing(loc, verbProperties.range);
            if ((double)verbProperties.minRange > 0.0)
                GenDraw.DrawRadiusRing(loc, verbProperties.minRange);
            return (AcceptanceReport)true;
        }
    }
}
