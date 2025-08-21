using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace FuelElectricityTurretMod
{
    public class CompProperties_Chargeable : CompProperties
    {
        public float chargeCapacity = 4f;
        public float consumptionPerShoot = 1f;
        public float initialChargePercent = 0.5f;
        public float netPowerconsumptioOnCharge;
        public float efficiency = 1f;
        public float chargeRateStep = 10f;

        public CompProperties_Chargeable() => this.compClass = typeof(CompElectricalChargable);

    }
}
