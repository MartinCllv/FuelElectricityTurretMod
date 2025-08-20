using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;
using static Verse.GenDraw;

namespace FuelElectricityTurretMod
{
    public class Electricity_Turret : Building_TurretGun
    {
        private static readonly Vector2 BarSize = new Vector2(2.8f, 2.8f);
        private static readonly Material BarBackunfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.3f, 0.3f, 0.3f));
        private Material BarUnfilledMat;
        private Material blinkMaterial;
        private Material RedUnfilledMat;
        private float blinkTimer = 0f;
        public Thing internalBattery;
        private int remainingShoots
        {
            get { return (int)(GetComp<CompElectricalChargable>().Charge / GetComp<CompElectricalChargable>().ConsumptionPerShoot); }   
        }
        protected override void Tick()
        {
            CompProperties_Power props = this.powerComp.Props;
            base.Tick();
            if (this.Active & !GetComp<CompElectricalChargable>().IsFullCharged)
            {
                this.powerComp.PowerOutput = -(props.PowerConsumption + GetComp<CompElectricalChargable>().NetPowerconsumptioOnCharge);
                GetComp<CompElectricalChargable>().AccumulateCharge();
            }
            else {this.powerComp.PowerOutput = -props.PowerConsumption;}
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            GetComp<CompElectricalChargable>().Init();
        }

        protected override void BeginBurst()
        {
            if (GetComp<CompElectricalChargable>().HasChargeToShoot)
            {
                base.BeginBurst();
                GetComp<CompElectricalChargable>().DecreseCharge();
            }
        }
        public override IEnumerable<Gizmo> GetGizmos()
        {
            CompElectricalChargable Props = GetComp<CompElectricalChargable>();
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            Command_Action command_Action = new Command_Action();
            command_Action.defaultLabel = "Increse charge rate.".ToString();
            command_Action.defaultDesc = "Increases the turret's power draw to accelerate battery charging, reducing cooldown between shots at the cost of higher energy consumption.".ToString();
            command_Action.icon = ContentFinder<Texture2D>.Get("UI/Increase_ChargeUI");
            //command_Action.iconAngle = Props.ChargeRateIncrement.uiIconAngle;
            //command_Action.iconOffset = Props.ChargeRateIncrement.uiIconOffset;
            //command_Action.iconDrawScale = GenUI.IconDrawScale(Props.ChargeRateIncrement);
            command_Action.action = delegate
            {
                GetComp<CompElectricalChargable>().IncreaseChargeRate();
            };
            yield return command_Action;

            command_Action = new Command_Action();
            command_Action.defaultLabel = "Decrease charge rate.".ToString();
            command_Action.defaultDesc = "Reduces the turret’s power usage, but slows battery charging, increasing the cooldown between shots.".ToString();
            command_Action.icon = ContentFinder<Texture2D>.Get("UI/Decrease_ChargeUI");
            //command_Action.iconAngle = Props.ChargeRateDecrement.uiIconAngle;
            //command_Action.iconOffset = Props.ChargeRateDecrement.uiIconOffset;
            //command_Action.iconDrawScale = GenUI.IconDrawScale(Props.ChargeRateDecrement);
            command_Action.action = delegate
            {
                GetComp<CompElectricalChargable>().DecreaseChargeRate();
            };
            yield return command_Action;
        }

        public override string GetInspectString()
        {
            string str = "Not Charging";
            if (Active)
            {
                str = "Charging at " + GetComp<CompElectricalChargable>().NetPowerconsumptioOnCharge.ToString("F0") + " W";
            }
            StringBuilder stringBuilder = new StringBuilder();
            string inspectString = base.GetInspectString();
            if (!inspectString.NullOrEmpty())
            {
                stringBuilder.AppendLine(inspectString);
            }
            stringBuilder.AppendLine("PowerBatteryStored".Translate() + ": " + GetComp<CompElectricalChargable>().Charge.ToString("F0") + " / " + GetComp<CompElectricalChargable>().ChargeCapacity.ToString("F0"));
            stringBuilder.AppendLine(str);
            stringBuilder.AppendLine("Remaining Shoots: " + remainingShoots.ToString("F0"));
            return stringBuilder.ToString().TrimEndNewlines();
        }

        private Material PickColorUnFilledMat()
        {
            BarUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.35f, 0.35f, 0.35f));
            RedUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.6f, 0.25f, 0.25f));
            if (!GetComp<CompElectricalChargable>().HasChargeToShoot)
            {
                if (blinkMaterial == null)
                {
                    blinkMaterial = BarUnfilledMat;
                }

                blinkTimer += 1f;
                if (blinkTimer > 100f)
                {
                    blinkTimer = 0f;
                    if (blinkMaterial == BarUnfilledMat)
                    {
                        blinkMaterial = RedUnfilledMat;
                    }
                    else
                    {
                        blinkMaterial = BarUnfilledMat;
                    }
                }
                return blinkMaterial;
            }
            return BarUnfilledMat;
        }

        private Material PickColorFilledMat()
        {
            if (GetComp<CompElectricalChargable>().HasChargeToShoot)
            {
                return SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.9f, 0.85f, 0.2f));
            }
            else { return SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.99f, 0.05f, 0.05f)); }
        }

        public struct FillableBarRequest2
        {
            public Vector3 center;

            public Vector2 size;

            public float fillPercent;

            public Material filledMat;

            public Material unfilledMat;

            public Material unfilledMatBack;

            public float margin;

            public Rot4 rotation;

            public Vector2 preRotationOffset;
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);

            FillableBarRequest2 r = new FillableBarRequest2()
            {
                center = drawLoc + Vector3.down * 0.03f,
                size = Electricity_Turret.BarSize,
                fillPercent = GetComp<CompElectricalChargable>().CurrentChargePercent,
                filledMat = PickColorFilledMat(),
                unfilledMat = PickColorUnFilledMat(),
                unfilledMatBack = Electricity_Turret.BarBackunfilledMat,
                margin = 0.15f
            };
            Rot4 rotation = this.Rotation;
            rotation.Rotate(RotationDirection.Clockwise);
            r.rotation = rotation;
            DrawFillableBar2(r);

        }
        private static void DrawFillableBar2(FillableBarRequest2 r)
        {
            Vector2 vector = r.preRotationOffset.RotatedBy(r.rotation.AsAngle);
            r.center += new Vector3(vector.x, 0f, vector.y);
            if (r.rotation == Rot4.South)
            {
                r.rotation = Rot4.North;
            }

            if (r.rotation == Rot4.West)
            {
                r.rotation = Rot4.East;
            }

            Vector3 s = new Vector3(r.size.x + r.margin, 1f, r.size.y + r.margin);
            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(r.center, r.rotation.AsQuat, s);
            Graphics.DrawMesh(MeshPool.plane10, matrix, r.unfilledMat, 0);

            s = new Vector3(r.size.x + r.margin * 2.2f, 1f, r.size.y + r.margin * 2.2f);
            matrix = default(Matrix4x4);
            Vector3 pos = r.center + Vector3.down * 0.01f;
            matrix.SetTRS(pos, r.rotation.AsQuat, s);
            Graphics.DrawMesh(MeshPool.plane10, matrix, r.unfilledMatBack, 1);

            if (r.fillPercent > 0.001f)
            {
                s = new Vector3(r.size.x * r.fillPercent, 1f, r.size.y);
                matrix = default(Matrix4x4);
                pos = r.center + Vector3.up * 0.02f;
                if (!r.rotation.IsHorizontal)
                {
                    pos.x -= r.size.x * 0.5f;
                    pos.x += 0.5f * r.size.x * r.fillPercent;
                }
                else
                {
                    pos.z -= r.size.x * 0.5f;
                    pos.z += 0.5f * r.size.x * r.fillPercent;
                }

                matrix.SetTRS(pos, r.rotation.AsQuat, s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, r.filledMat, 0);
            }
        }

    }
}
