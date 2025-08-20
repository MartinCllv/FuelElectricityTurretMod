using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;

namespace FuelElectricityTurretMod
{
    public class CompElectricalChargable : ThingComp
    {
        public CompProperties_Chargeable Props => (CompProperties_Chargeable)this.props;

        public float EnergyChargePerTick => (this.Props.netPowerconsumptioOnCharge / 60000f) * this.Props.efficiency;

        public float Charge = 1f;

        public float NetPowerconsumptioOnCharge => Props.netPowerconsumptioOnCharge;

        public float ConsumptionPerShoot => this.Props.consumptionPerShoot;

        public float ChargeCapacity => this.Props.chargeCapacity;

        public ThingDef ChargeRateIncrement;
        public ThingDef ChargeRateDecrement;

        public void IncreaseChargeRate()
        {
            Props.netPowerconsumptioOnCharge += Props.chargeRateStep;
        }
        public void DecreaseChargeRate()
        {
            if (!(Props.netPowerconsumptioOnCharge - Props.chargeRateStep < 0f))
            {
                Props.netPowerconsumptioOnCharge -= Props.chargeRateStep;
            }
        }

        public float CurrentChargePercent
        {
            get
            {
                if (this.Charge <= 0f) return 0f;
                return this.Charge / this.ChargeCapacity;
            }
        }
        public bool HasChargeToShoot
        {
            get
            {
                if (this.Props.consumptionPerShoot > this.Charge)
                    return false;
                return true;
            }
        }
        public bool IsFullCharged
        {
            get
            {
                return ((double)this.ChargeCapacity - (double)this.Charge) <= 0;
            }
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look<float>(ref this.Charge, "ChargeStored");
        }
        public void DecreseCharge()
        {
            this.Charge -= this.Props.consumptionPerShoot;
        }
        public void AccumulateCharge()
        {
            this.Charge += this.EnergyChargePerTick;
        }

        public void Init()
        {
            this.Charge = this.Props.initialChargePercent * this.Props.chargeCapacity;
        }

    }
}
