using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using HarmonyLib;
using UnityEngine.Profiling;
using System.Diagnostics.Eventing.Reader;

namespace PowerSystemTweek
{
    public enum EExchangerDischargeStrategy
    {
        EQUAL_AS_FUEL_GENERATOR = 0,
        DISCHARGE_FIRST = 1,
        DISCHARGE_LAST = 2
    }

    public class PowerSystemPatch
    {
        public static EExchangerDischargeStrategy ExchangerDischargeStrategy = EExchangerDischargeStrategy.EQUAL_AS_FUEL_GENERATOR;
        public static bool PowerSystemGameTickPatch = true;
        public static double[] ExecuteTime = new double[512];
        public static List<HighStopwatch> StopWatchList = new List<HighStopwatch>(512);
        public static void InitStopWatch()
        {
            for(int index = 0; index < 512; index++)
            {
                StopWatchList.Add(new HighStopwatch());
            }
        }
        //[HarmonyPrefix, HarmonyPatch(typeof(PowerGeneratorComponent), nameof(PowerGeneratorComponent.EnergyCap_Gamma_Req))]
        //static bool EnergyCap_Gamma_Req_Prefix(PowerGeneratorComponent __instance, float sx, float sy, float sz, float increase, float eta, ref long __result)
        //{
        //    if (GameMain.gameTick % 60 == __instance.id)
        //    {
        //        float num1 = (float)(((double)sx * (double)__instance.x + (double)sy * (double)__instance.y + (double)sz * (double)__instance.z + (double)increase * 0.800000011920929 + (__instance.catalystPoint > 0 ? (double)__instance.ionEnhance : 0.0)) * 6.0 + 0.5);
        //        float num2 = (double)num1 > 1.0 ? 1f : ((double)num1 < 0.0 ? 0.0f : num1);
        //        __instance.currentStrength = num2;
        //        float num3 = (float)Cargo.accTableMilli[__instance.catalystIncLevel];
        //        __instance.capacityCurrentTick = (long)((double)__instance.currentStrength * (1.0 + (double)__instance.warmup * 1.5) * (__instance.catalystPoint > 0 ? 2.0 * (1.0 + (double)num3) : 1.0) * (__instance.productId > 0 ? 8.0 : 1.0) * (double)__instance.genEnergyPerTick);
        //        __instance.warmupSpeed = (float)(((double)num2 - 0.75) * 4.0 * 1.38888890433009E-05);                
        //    }

        //    eta = (float)(1.0 - (1.0 - (double)eta) * (1.0 - (double)__instance.warmup * (double)__instance.warmup * 0.400000005960464));
        //    __result = (long)((double)__instance.capacityCurrentTick / (double)eta + 0.49999999);
        //    return false;
        //}

        [HarmonyPrefix, HarmonyPatch(typeof(PowerSystem), nameof(PowerSystem.CalculatePowerSystemWeight))]
        static bool CalculatePowerSystemWeightPrefix(PowerSystem __instance, ref int ___totalPowerSystemWeight)
        {
            if (!PowerSystemGameTickPatch)
            {
                return true;
            }
            else {
                ___totalPowerSystemWeight = 10 + (int)(100000000.0 * ExecuteTime[__instance.planet.factoryIndex]);
                return false;
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PowerSystem), nameof(PowerSystem.GameTick))]
        static bool GameTickPrefixExperimental(PowerSystem __instance, DysonSphere ___dysonSphere, long time, bool isActive, bool isMultithreadMode = false)
        {
            if (!PowerSystemGameTickPatch)
            {
                return true;
            }
            var powerSystem = __instance;
            StopWatchList[powerSystem.factory.index].Begin();
            DysonSphere dysonSphere = ___dysonSphere;

            FactoryProductionStat factoryProductionStat = GameMain.statistics.production.factoryStatPool[powerSystem.factory.index];
            int[] productRegister = factoryProductionStat.productRegister;
            int[] consumeRegister = factoryProductionStat.consumeRegister;
            long num1 = 0;
            long num2 = 0;
            long num3 = 0;
            long num4 = 0;
            long num5 = 0;
            float _dt = 0.01666667f;
            PlanetData planet = powerSystem.factory.planet;
            float windStrength = planet.windStrength;
            float luminosity = planet.luminosity;
            Vector3 normalized = planet.runtimeLocalSunDirection.normalized;
            AnimData[] entityAnimPool = powerSystem.factory.entityAnimPool;
            SignData[] entitySignPool = powerSystem.factory.entitySignPool;
            if (powerSystem.networkServes == null || powerSystem.networkServes.Length != powerSystem.netPool.Length)
                powerSystem.networkServes = new float[powerSystem.netPool.Length];
            if (powerSystem.networkGenerates == null || powerSystem.networkGenerates.Length != powerSystem.netPool.Length)
                powerSystem.networkGenerates = new float[powerSystem.netPool.Length];
            bool useIonLayer = GameMain.history.useIonLayer;
            bool useCata = time % 10L == 0L;
            Array.Clear((Array)powerSystem.currentGeneratorCapacities, 0, powerSystem.currentGeneratorCapacities.Length);
            Player mainPlayer = GameMain.mainPlayer;
            Vector3 vector3 = Vector3.zero;
            float num6 = 0.0f;
            bool flag1;
            if (mainPlayer.mecha.coreEnergyCap - mainPlayer.mecha.coreEnergy > 0.0 && mainPlayer.isAlive && mainPlayer.planetId == planet.id)
            {
                vector3 = isMultithreadMode ? powerSystem.multithreadPlayerPos : mainPlayer.position;
                flag1 = true;
            }
            else
                flag1 = false;
            lock (mainPlayer.mecha)
                num6 = Mathf.Pow(Mathf.Clamp01((float)(1.0 - mainPlayer.mecha.coreEnergy / mainPlayer.mecha.coreEnergyCap) * 10f), 0.75f);
            /** Patch note, replace private field dysonSphere with traversed local variable **/
            float response = dysonSphere != null ? dysonSphere.energyRespCoef : 0.0f;
            int num7 = (int)(Math.Min(Math.Abs(powerSystem.factory.planet.rotationPeriod), Math.Abs(powerSystem.factory.planet.orbitalPeriod)) * 60.0 / 2160.0);
            if (num7 < 1)
                num7 = 1;
            else if (num7 > 60)
                num7 = 60;
            if (powerSystem.factory.planet.singularity == EPlanetSingularity.TidalLocked)
                num7 = 60;
            bool flag2 = time % (long)num7 == 0L || GameMain.onceGameTick <= 2L;
            int num8 = (int)(time % 90L);
            EntityData[] entityPool = powerSystem.factory.entityPool;
            for (int index1 = 1; index1 < powerSystem.netCursor; ++index1)
            {
                PowerNetwork powerNetwork = powerSystem.netPool[index1];
                if (powerNetwork != null && powerNetwork.id == index1)
                {
                    /* Patch note: calculate clean energy capacity */
                    long cleanEnergyGeneratorCap = 0;

                    List<int> consumers = powerNetwork.consumers;
                    int count1 = consumers.Count;
                    long num9 = 0;
                    for (int index2 = 0; index2 < count1; ++index2)
                    {
                        long requiredEnergy = powerSystem.consumerPool[consumers[index2]].requiredEnergy;
                        num9 += requiredEnergy;
                        num2 += requiredEnergy;
                    }
                    foreach (PowerNetworkStructures.Node node in powerNetwork.nodes)
                    {
                        int id = node.id;
                        if (powerSystem.nodePool[id].id == id && powerSystem.nodePool[id].isCharger)
                        {
                            if ((double)powerSystem.nodePool[id].coverRadius <= 20.0)
                            {
                                double num10 = 0.0;
                                if (flag1)
                                {
                                    double num11 = (double)powerSystem.nodePool[id].powerPoint.x * 0.987999975681305 - (double)vector3.x;
                                    float num12 = powerSystem.nodePool[id].powerPoint.y * 0.988f - vector3.y;
                                    float num13 = powerSystem.nodePool[id].powerPoint.z * 0.988f - vector3.z;
                                    float coverRadius = powerSystem.nodePool[id].coverRadius;
                                    if ((double)coverRadius < 9.0)
                                        coverRadius += 2.01f;
                                    else if ((double)coverRadius > 20.0)
                                        coverRadius += 0.5f;
                                    float num14 = (float)(num11 * num11 + (double)num12 * (double)num12 + (double)num13 * (double)num13);
                                    float num15 = coverRadius * coverRadius;
                                    if ((double)num14 <= (double)num15)
                                    {
                                        double consumerRatio = powerNetwork.consumerRatio;
                                        float num16 = (float)(((double)num15 - (double)num14) / (3.0 * (double)coverRadius));
                                        if ((double)num16 > 1.0)
                                            num16 = 1f;
                                        num10 = (double)num6 * consumerRatio * consumerRatio * (double)num16;
                                    }
                                }
                                double num17 = (double)powerSystem.nodePool[id].idleEnergyPerTick * (1.0 - num10) + (double)powerSystem.nodePool[id].workEnergyPerTick * num10;
                                if (powerSystem.nodePool[id].requiredEnergy < powerSystem.nodePool[id].idleEnergyPerTick)
                                    powerSystem.nodePool[id].requiredEnergy = powerSystem.nodePool[id].idleEnergyPerTick;
                                if ((double)powerSystem.nodePool[id].requiredEnergy < num17 - 0.01)
                                {
                                    double num11 = num17 * 0.02 + (double)powerSystem.nodePool[id].requiredEnergy * 0.98;
                                    powerSystem.nodePool[id].requiredEnergy = (int)(num11 + 0.9999);
                                }
                                else if ((double)powerSystem.nodePool[id].requiredEnergy > num17 + 0.01)
                                {
                                    double num11 = num17 * 0.2 + (double)powerSystem.nodePool[id].requiredEnergy * 0.8;
                                    powerSystem.nodePool[id].requiredEnergy = (int)num11;
                                }
                            }
                            else
                                powerSystem.nodePool[id].requiredEnergy = powerSystem.nodePool[id].idleEnergyPerTick;
                            long requiredEnergy = (long)powerSystem.nodePool[id].requiredEnergy;
                            num9 += requiredEnergy;
                            num2 += requiredEnergy;
                        }
                    }
                    long num18 = 0;
                    List<int> exchangers = powerNetwork.exchangers;
                    int count2 = exchangers.Count;
                    long num19 = 0;
                    long num20 = 0;
                    long num21 = 0;
                    long num22 = 0;
                    for (int index2 = 0; index2 < count2; ++index2)
                    {
                        int index3 = exchangers[index2];
                        powerSystem.excPool[index3].StateUpdate();
                        powerSystem.excPool[index3].BeltUpdate(powerSystem.factory);
                        bool flag3 = (double)powerSystem.excPool[index3].state >= 1.0;
                        bool flag4 = (double)powerSystem.excPool[index3].state <= -1.0;
                        if (!flag3 && !flag4)
                        {
                            powerSystem.excPool[index3].capsCurrentTick = 0L;
                            powerSystem.excPool[index3].currEnergyPerTick = 0L;
                        }
                        int entityId = powerSystem.excPool[index3].entityId;
                        float num10 = (float)(((double)powerSystem.excPool[index3].state + 1.0) * (double)entityAnimPool[entityId].working_length * 0.5);
                        if ((double)num10 >= 3.99000000953674)
                            num10 = 3.99f;
                        entityAnimPool[entityId].time = num10;
                        entityAnimPool[entityId].state = 0U;
                        entityAnimPool[entityId].power = (float)powerSystem.excPool[index3].currPoolEnergy / (float)powerSystem.excPool[index3].maxPoolEnergy;
                        if (flag4)
                        {
                            long num11 = powerSystem.excPool[index3].OutputCaps();
                            num21 += num11;
                            num18 = num21;
                            powerSystem.currentGeneratorCapacities[powerSystem.excPool[index3].subId] += num11;
                        }
                        else if (flag3)
                            num22 += powerSystem.excPool[index3].InputCaps();
                    }
                    /* Code Analysis Note: until this line
                     * num18 = num21 = ExcOutCap
                     * num9 = num2 = ConsReq
                     * num22 = ExcInpCap
                     */
                    List<int> generators = powerNetwork.generators;
                    int count3 = generators.Count;
                    for (int index2 = 0; index2 < count3; ++index2)
                    {
                        int index3 = generators[index2];
                        long num10;
                        if (powerSystem.genPool[index3].wind)
                        {
                            num10 = powerSystem.genPool[index3].EnergyCap_Wind(windStrength);
                            num18 += num10;
                            cleanEnergyGeneratorCap += num10;
                        }
                        else if (powerSystem.genPool[index3].photovoltaic)
                        {
                            if (flag2)
                            {
                                num10 = powerSystem.genPool[index3].EnergyCap_PV(normalized.x, normalized.y, normalized.z, luminosity);
                                num18 += num10;
                            }
                            else
                            {
                                num10 = powerSystem.genPool[index3].capacityCurrentTick;
                                num18 += num10;
                            }
                            cleanEnergyGeneratorCap += num10;
                        }
                        else if (powerSystem.genPool[index3].gamma)
                        {
                            num10 = powerSystem.genPool[index3].EnergyCap_Gamma(response);
                            num18 += num10;
                            cleanEnergyGeneratorCap += num10;
                        }
                        else if (powerSystem.genPool[index3].geothermal)
                        {
                            num10 = powerSystem.genPool[index3].EnergyCap_GTH();
                            num18 += num10;
                            cleanEnergyGeneratorCap += num10;
                        }
                        else
                        {
                            num10 = powerSystem.genPool[index3].EnergyCap_Fuel();
                            num18 += num10;
                            entitySignPool[powerSystem.genPool[index3].entityId].signType = num10 > 30L ? 0U : 8U;
                        }
                        powerSystem.currentGeneratorCapacities[powerSystem.genPool[index3].subId] += num10;
                    }
                    num1 += num18 - num21;
                    long num23 = num18 - num9;

                    /* Code Analysis Note: until this line
                     * num21 = ExcOutCap
                     * num18 = ExcOutCap + GenCap
                     * num1 = GenCap
                     * num9 = num2 = ConsReq
                     * num22 = ExcInpCap
                     * num23 = ExcOutCap + GenCap - ConsReq
                     */
                    long num24 = 0;
                    if (num23 > 0L && powerNetwork.exportDemandRatio > 0.0)
                    {
                        if (powerNetwork.exportDemandRatio > 1.0)
                            powerNetwork.exportDemandRatio = 1.0;
                        num24 = (long)((double)num23 * powerNetwork.exportDemandRatio + 0.5);
                        num23 -= num24;
                        num9 += num24;
                    }

                    /* Code Analysis Note: until this line
                     * NOTE: powerNetwork.exportDemandRatio = 1.0 - (1.0 - powerNetwork.exportDemandRatio) * (1.0 - planetAtField.fillDemandRatio); regarding ATFields
                     * 
                     * num21 = ExcOutCap
                     * num18 = ExcOutCap + GenCap
                     * num1 = GenCap
                     * num2 = ConsReq
                     * num22 = ExcInpCap
                     * num24 = EnergyExport = (ExcOutCap + GenCap - ConsReq) * exportDemandRatio + 0.5
                     * num23 = ExcOutCap + GenCap - ConsReq - EnergyExport => this value determine Accs should charge
                     * num9 = ConsReq + EnergyExport
                     */

                    powerNetwork.exportDemandRatio = 0.0;
                    powerNetwork.energyStored = 0L;
                    List<int> accumulators = powerNetwork.accumulators;
                    int count4 = accumulators.Count;
                    long num25 = 0;
                    long num26 = 0;
                    if (num23 >= 0L)
                    {
                        for (int index2 = 0; index2 < count4; ++index2)
                        {
                            int index3 = accumulators[index2];
                            powerSystem.accPool[index3].curPower = 0L;
                            long num10 = powerSystem.accPool[index3].InputCap();
                            if (num10 > 0L)
                            {
                                long num11 = num10 < num23 ? num10 : num23;
                                powerSystem.accPool[index3].curEnergy += num11;
                                powerSystem.accPool[index3].curPower = num11;
                                num23 -= num11;
                                num25 += num11;
                                num4 += num11;
                            }
                            powerNetwork.energyStored += powerSystem.accPool[index3].curEnergy;
                            int entityId = powerSystem.accPool[index3].entityId;
                            entityAnimPool[entityId].state = powerSystem.accPool[index3].curEnergy > 0L ? 1U : 0U;
                            entityAnimPool[entityId].power = (float)powerSystem.accPool[index3].curEnergy / (float)powerSystem.accPool[index3].maxEnergy;
                        }
                    }
                    else
                    {
                        long num10 = -num23;
                        for (int index2 = 0; index2 < count4; ++index2)
                        {
                            int index3 = accumulators[index2];
                            powerSystem.accPool[index3].curPower = 0L;
                            long num11 = powerSystem.accPool[index3].OutputCap();
                            if (num11 > 0L)
                            {
                                long num12 = num11 < num10 ? num11 : num10;
                                powerSystem.accPool[index3].curEnergy -= num12;
                                powerSystem.accPool[index3].curPower = -num12;
                                num10 -= num12;
                                num26 += num12;
                                num3 += num12;
                            }
                            powerNetwork.energyStored += powerSystem.accPool[index3].curEnergy;
                            int entityId = powerSystem.accPool[index3].entityId;
                            entityAnimPool[entityId].state = powerSystem.accPool[index3].curEnergy > 0L ? 2U : 0U;
                            entityAnimPool[entityId].power = (float)powerSystem.accPool[index3].curEnergy / (float)powerSystem.accPool[index3].maxEnergy;
                        }
                    }
                    /* Code Analysis Note: until this line
                     * NOTE: powerNetwork.exportDemandRatio = 1.0 - (1.0 - powerNetwork.exportDemandRatio) * (1.0 - planetAtField.fillDemandRatio); regarding ATFields
                     * 
                     * num21 = ExcOutCap
                     * num18 = ExcOutCap + GenCap
                     * num1 = GenCap
                     * num2 = ConsReq
                     * num22 = ExcInpCap
                     * num24 = EnergyExport = (ExcOutCap + GenCap - ConsReq) * exportDemandRatio + 0.5
                     * num9 = ConsReq + EnergyExport
                     * num4 = num25 = AccCharged
                     * num26 = num3 = AccDischarged
                     * num23 = ExcOutCap + GenCap - ConsReq - EnergyExport - AccCharged  => this value determine following Exc should charge                     * 
                     */

                    /* Patch note : */
                    //double num27 = num23 < num22 ? (double)num23 / (double)num22 : 1.0;
                    num23 -= num21; // Exclude exchanger output
                    num23 = num23 > 0 ? num23 : 0;
                    double num27 = num23 < num22 ? (double)num23 / (double)num22 : 1.0;
                    /* Patch End
                    /* Code Analysis Note: ExcChargeWorkRatio = num27 = num23/num22 limited by <=1 */
                    for (int index2 = 0; index2 < count2; ++index2)
                    {
                        int index3 = exchangers[index2];
                        if ((double)powerSystem.excPool[index3].state >= 1.0 && num27 >= 0.0)
                        {
                            long num10 = (long)(num27 * (double)powerSystem.excPool[index3].capsCurrentTick + 0.99999);
                            long remaining = num23 < num10 ? num23 : num10;
                            long num11 = powerSystem.excPool[index3].InputUpdate(remaining, entityAnimPool, productRegister, consumeRegister);
                            num23 -= num11;
                            num19 += num11;
                            num4 += num11;
                        }
                        else
                            powerSystem.excPool[index3].currEnergyPerTick = 0L;
                    }

                    /* Code Analysis Note: until this line
                     * NOTE: powerNetwork.exportDemandRatio = 1.0 - (1.0 - powerNetwork.exportDemandRatio) * (1.0 - planetAtField.fillDemandRatio); regarding ATFields
                     * 
                     * num21 = ExcOutCap
                     * num18 = ExcOutCap + GenCap
                     * num1 = GenCap
                     * num2 = ConsReq
                     * num22 = ExcInpCap
                     * num24 = EnergyExport = (ExcOutCap + GenCap - ConsReq) * exportDemandRatio + 0.5
                     * num9 = ConsReq + EnergyExport
                     * num23 = ExcOutCap + GenCap - ConsReq - EnergyExport - AccCharged - ExcCharged  => should be 0 that remain nothing
                     * num19 = ExcCharged => Exchanger has lowest charge priority
                     * num4 = AccCharged + ExcCharged
                     * num25 = AccCharged
                     * num26 = num3 = AccDischarged
                     */

                    // num28 = (ExcOutCap + GenCap) < (ConsReq + EnergyExport) + ExcCharged ? (ExcOutCap + GenCap) + AccCharged + ExcCharged :  (CosmPowerReq + ExportReq) +  AccCharged + ExcCharged; 
                    // Following loop will calculate energy actually discharged, num28 will determine energy should be discharged from exchangers
                    long num28 = num18 < num9 + num19 ? num18 + num25 + num19 : num9 + num25 + num19;
                    //Patch note: change the exchanger discharge target
                    double num29;
                    {
                        long energyToGenerate;
                        long energyExcDischarge;
                        switch (ExchangerDischargeStrategy)
                        {
                            default:
                            case EExchangerDischargeStrategy.EQUAL_AS_FUEL_GENERATOR:
                                long fuelAndDiscCap = (num18 - cleanEnergyGeneratorCap);
                                energyToGenerate = num28 - cleanEnergyGeneratorCap;
                                num29 = (fuelAndDiscCap <=  0 || energyToGenerate <= 0)  ? 0.0 : 
                                        (energyToGenerate < fuelAndDiscCap) ? ((double)energyToGenerate / (double)fuelAndDiscCap) : 1.0;
                                break;
                            case EExchangerDischargeStrategy.DISCHARGE_FIRST:
                                energyToGenerate = cleanEnergyGeneratorCap;
                                energyExcDischarge = num28 > energyToGenerate ? num28 - energyToGenerate : 0;
                                num29 = energyExcDischarge < num21 ? (double)energyExcDischarge / (double)num21 : 1.0;
                                break;
                            case EExchangerDischargeStrategy.DISCHARGE_LAST:
                                energyToGenerate = num18 - num21;
                                energyExcDischarge = num28 > energyToGenerate ? num28 - energyToGenerate : 0;
                                num29 = energyExcDischarge < num21 ? (double)energyExcDischarge / (double)num21 : 1.0;
                                break;

                        }
                    }

                    for (int index2 = 0; index2 < count2; ++index2)
                    {
                        int index3 = exchangers[index2];
                        if ((double)powerSystem.excPool[index3].state <= -1.0)
                        {
                            long num10 = (long)(num29 * (double)powerSystem.excPool[index3].capsCurrentTick + 0.99999);
                            long energyPay = num28 < num10 ? num28 : num10;
                            long num11 = powerSystem.excPool[index3].OutputUpdate(energyPay, entityAnimPool, productRegister, consumeRegister);
                            num20 += num11;
                            num3 += num11;
                            num28 -= num11;
                        }
                    }

                    /* Code Analysis Note: until this line
                     * NOTE: powerNetwork.exportDemandRatio = 1.0 - (1.0 - powerNetwork.exportDemandRatio) * (1.0 - planetAtField.fillDemandRatio); regarding ATFields
                     * 
                     * num21 = ExcOutCap
                     * num18 = ExcOutCap + GenCap
                     * num1 = GenCap
                     * num2 = ConsReq
                     * num22 = ExcInpCap
                     * num24 = EnergyExport = (ExcOutCap + GenCap - ConsReq) * exportDemandRatio + 0.5
                     * num9 = ConsReq + EnergyExport
                     * num23 = ExcOutCap + GenCap - ConsReq - EnergyExport - AccCharged - ExcCharged  => should be 0 that remain nothing
                     * num19 = ExcCharged => Exchanger has lowest charge priority
                     * num4 = AccCharged + ExcCharged = TotalCharge
                     * num25 = AccCharged
                     * num26 = AccDischarged
                     * num3 = AccDischarged + ExcDischarged = TotalDischarge
                     * num28 = (ExcOutCap + GenCap) + AccCharged + ExcCharged - ExcDischarged =>(not enough power)
                     * or    = (CosmPowerReq + EnergyExport) +  AccCharged + ExcCharged  - ExcDischarged=>(enough power)
                     * num29 = ExcDischargeWorkRatio
                     * num20 = num3 = ExcDischarged
                     */

                    powerNetwork.energyCapacity = num18 - num21;//GenCap = (ExcOutCap + GenCap) - ExcOutCap
                    powerNetwork.energyRequired = num9 - num24;//ConsReq = (CosmReq + EnergyExport) - EnergyExport
                    powerNetwork.energyExport = num24;//EnergyExport
                    powerNetwork.energyServed = num18 + num26 < num9 ? num18 + num26 : num9;// ExcOutCap + GenCap + AccDischarged < (CosmPowerReq + EnergyExport)？ ExcOutCap + GenCap + AccDischarged  ： (CosmPowerReq + ExportReq)
                    powerNetwork.energyAccumulated = num25 - num26;//AccEnergy = AccCharged - AccDischarged
                    powerNetwork.energyExchanged = num19 - num20;//ExcCharged - ExcDischarged
                    powerNetwork.energyExchangedInputTotal = num19;//ExcCharged
                    powerNetwork.energyExchangedOutputTotal = num20;//ExcDischarged
                    if (num24 > 0L)
                    {
                        PlanetATField planetAtField = powerSystem.factory.planetATField;
                        planetAtField.energy += num24;
                        planetAtField.atFieldRechargeCurrent = num24 * 60L;
                    }
                    long num30 = num18 + num26; //(ExcOutCap + GenCap) + AccDischargeCap = TotalPowerCap
                    long num31 = num9 + num25; //(CosmReq + ExportReq) + AccChargeReq = TotalPowerReq
                    num5 += num30 >= num31 ? num2 + num24 : num30; // if (TotalPowerCap > TotalPowerReq) (num5 = CosmPowerReq + energyExport) else num5 = totalPowerCap
                    long num32 = num20 - num31 > 0L ? num20 - num31 : 0L;// ExcOutputMargin = (ExcDischarged - TotalPowerReq) under limited by 0??
                    double num33 = num30 >= num31 ? 1.0 : (double)num30 / (double)num31; //ConsumerRatio = TotalPowerCap / TotalPowerReq, upper limited by 1
                    long num34 = num31 + (num19 - num32); // ? num34 = TotalPowerReq + ExcCharged - max(0, (ExcDischarged - TotalPowerReq)) almost = TotalPowerReq + ExcCharged
                    long num35 = num30 - num20;// ?????EnergyCanServeByAccAndGen = (ExcOutCap + GenCap) + AccDischarged - ExcDischarged
                    double num36 = num35 > num34 ? (double)num34 / (double)num35 : 1.0;//GenerateRatio =  EnergyReqGen/EnergyCanServeByAccAndGen upper limited by 1
                    powerNetwork.consumerRatio = num33;
                    powerNetwork.generaterRatio = num36;
                    float num37 = num35 > 0L || powerNetwork.energyStored > 0L || num20 > 0L ? (float)num33 : 0.0f;
                    float num38 = num35 > 0L || powerNetwork.energyStored > 0L || num20 > 0L ? (float)num36 : 0.0f; //If not EnergyReqGen or energyStored or ExcOutput, set 0
                    powerSystem.networkServes[index1] = num37;
                    powerSystem.networkGenerates[index1] = num38;

                    //Patch Note : Calculate clean energy generator generaterRatio, and fuel generaterRatio seperately and apply to them 
                    long totalEnergyNeedGenerate = num28;
                    long fuelEnergyCap = powerNetwork.energyCapacity - cleanEnergyGeneratorCap;
                    double generatorRatioClean = 1.0;

                    // Different from origin calculation which ignores energy input energy from exchanger => some time generator may stop
                    // this ratio will make every fuel works equally

                    double generatorRatioFuel = ((double)totalEnergyNeedGenerate - (double)cleanEnergyGeneratorCap) / ((double)fuelEnergyCap + 0.001);
                    generatorRatioFuel = generatorRatioFuel > 1.0 ? 1.0 : generatorRatioFuel;

                    if (generatorRatioFuel < 0.0)
                    {
                        generatorRatioFuel = 0.0;
                        generatorRatioClean = (double)totalEnergyNeedGenerate / ((double)cleanEnergyGeneratorCap + 0.001);
                        generatorRatioClean = generatorRatioClean > 1.0 ? 1.0 : generatorRatioClean;
                    }

                    //Patch end
                    for (int index2 = 0; index2 < count3; ++index2)
                    {
                        int index3 = generators[index2];
                        long energy = 0;
                        float _speed1 = 1f;
                        bool flag3 = !powerSystem.genPool[index3].wind && !powerSystem.genPool[index3].photovoltaic && !powerSystem.genPool[index3].gamma && !powerSystem.genPool[index3].geothermal;
                        if (flag3)
                            powerSystem.genPool[index3].currentStrength = num28 <= 0L || powerSystem.genPool[index3].capacityCurrentTick <= 0L ? 0.0f : 1f;
                        if (num28 > 0L && powerSystem.genPool[index3].productId == 0)
                        {
                            /* Patch note: if generator uses clean energy, make it output max energy */
                            double generaterRatio = flag3 ? generatorRatioFuel : generatorRatioClean;
                            long num10 = (long)(generaterRatio * (double)powerSystem.genPool[index3].capacityCurrentTick + 0.99999);

                            energy = num28 < num10 ? num28 : num10;
                            if (energy > 0L)
                            {
                                num28 -= energy;
                                if (flag3)
                                {
                                    powerSystem.genPool[index3].GenEnergyByFuel(energy, consumeRegister);
                                    _speed1 = 2f;
                                }
                            }
                        }
                        powerSystem.genPool[index3].generateCurrentTick = energy;
                        int entityId = powerSystem.genPool[index3].entityId;
                        if (powerSystem.genPool[index3].wind)
                        {
                            float _speed2 = 0.7f;
                            entityAnimPool[entityId].Step2((double)entityAnimPool[entityId].power > 0.100000001490116 || energy > 0L ? 1U : 0U, _dt, windStrength, _speed2);
                        }
                        else if (powerSystem.genPool[index3].gamma)
                        {
                            bool keyFrame = (index3 + num8) % 90 == 0;
                            powerSystem.genPool[index3].GameTick_Gamma(useIonLayer, useCata, keyFrame, powerSystem.factory, productRegister, consumeRegister);
                            entityAnimPool[entityId].time += _dt;
                            if ((double)entityAnimPool[entityId].time > 1.0)
                                --entityAnimPool[entityId].time;
                            entityAnimPool[entityId].power = (float)powerSystem.genPool[index3].capacityCurrentTick / (float)powerSystem.genPool[index3].genEnergyPerTick;
                            entityAnimPool[entityId].state = (uint)((powerSystem.genPool[index3].productId > 0 ? 2 : 0) + (powerSystem.genPool[index3].catalystPoint > 0 ? 1 : 0));
                            entityAnimPool[entityId].working_length = (float)((double)entityAnimPool[entityId].working_length * 0.990000009536743 + (powerSystem.genPool[index3].catalystPoint > 0 ? 0.00999999977648258 : 0.0));
                            if (isActive)
                                entitySignPool[entityId].signType = (double)powerSystem.genPool[index3].productCount < 20.0 ? 0U : 6U;
                        }
                        else if (powerSystem.genPool[index3].fuelMask > (short)1)
                        {
                            float _power = (float)((double)entityAnimPool[entityId].power * 0.98 + 0.02 * (energy > 0L ? 1.0 : 0.0));
                            if (energy > 0L && (double)_power < 0.0)
                                _power = 0.0f;
                            entityAnimPool[entityId].Step2((double)entityAnimPool[entityId].power > 0.100000001490116 || energy > 0L ? 1U : 0U, _dt, _power, _speed1);
                        }
                        else if (powerSystem.genPool[index3].geothermal)
                        {
                            float num10 = powerSystem.genPool[index3].warmup + powerSystem.genPool[index3].warmupSpeed;
                            powerSystem.genPool[index3].warmup = (double)num10 > 1.0 ? 1f : ((double)num10 < 0.0 ? 0.0f : num10);
                            entityAnimPool[entityId].state = energy > 0L ? 1U : 0U;
                            entityAnimPool[entityId].Step(entityAnimPool[entityId].state, _dt, 2f, 0.0f);
                            entityAnimPool[entityId].working_length = powerSystem.genPool[index3].warmup;
                            if (energy > 0L)
                            {
                                if ((double)entityAnimPool[entityId].power < 1.0)
                                    entityAnimPool[entityId].power += _dt / 6f;
                            }
                            else if ((double)entityAnimPool[entityId].power > 0.0)
                                entityAnimPool[entityId].power -= _dt / 6f;
                            entityAnimPool[entityId].prepare_length += (float)(3.14159274101257 * (double)_dt / 8.0);
                            if ((double)entityAnimPool[entityId].prepare_length > 6.28318548202515)
                                entityAnimPool[entityId].prepare_length -= 6.283185f;
                        }
                        else
                        {
                            float _power = (float)((double)entityAnimPool[entityId].power * 0.98 + 0.02 * (double)energy / (double)powerSystem.genPool[index3].genEnergyPerTick);
                            if (energy > 0L && (double)_power < 0.200000002980232)
                                _power = 0.2f;
                            entityAnimPool[entityId].Step2((double)entityAnimPool[entityId].power > 0.100000001490116 || energy > 0L ? 1U : 0U, _dt, _power, _speed1);
                        }
                    }
                }
            }
            lock (factoryProductionStat)
            {
                factoryProductionStat.powerGenRegister = num1;
                factoryProductionStat.powerConRegister = num2;
                factoryProductionStat.powerDisRegister = num3;
                factoryProductionStat.powerChaRegister = num4;
                factoryProductionStat.energyConsumption += num5;
            }
            if (isActive)
            {
                for (int index1 = 0; index1 < powerSystem.netCursor; ++index1)
                {
                    PowerNetwork powerNetwork = powerSystem.netPool[index1];
                    if (powerNetwork != null && powerNetwork.id == index1)
                    {
                        List<int> consumers = powerNetwork.consumers;
                        int count = consumers.Count;
                        if (index1 == 0)
                        {
                            for (int index2 = 0; index2 < count; ++index2)
                                entitySignPool[powerSystem.consumerPool[consumers[index2]].entityId].signType = 1U;
                        }
                        else if (powerNetwork.consumerRatio < 0.100000001490116)
                        {
                            for (int index2 = 0; index2 < count; ++index2)
                                entitySignPool[powerSystem.consumerPool[consumers[index2]].entityId].signType = 2U;
                        }
                        else if (powerNetwork.consumerRatio < 0.5)
                        {
                            for (int index2 = 0; index2 < count; ++index2)
                                entitySignPool[powerSystem.consumerPool[consumers[index2]].entityId].signType = 3U;
                        }
                        else
                        {
                            for (int index2 = 0; index2 < count; ++index2)
                                entitySignPool[powerSystem.consumerPool[consumers[index2]].entityId].signType = 0U;
                        }
                    }
                }
            }
            for (int index = 1; index < powerSystem.nodeCursor; ++index)
            {
                if (powerSystem.nodePool[index].id == index)
                {
                    int entityId = powerSystem.nodePool[index].entityId;
                    int networkId = powerSystem.nodePool[index].networkId;
                    if (powerSystem.nodePool[index].isCharger)
                    {
                        float networkServe = powerSystem.networkServes[networkId];
                        int num9 = powerSystem.nodePool[index].requiredEnergy - powerSystem.nodePool[index].idleEnergyPerTick;
                        if ((double)powerSystem.nodePool[index].coverRadius < 20.0)
                            entityAnimPool[entityId].StepPoweredClamped(networkServe, _dt, num9 > 0 ? 2U : 1U);
                        else
                            entityAnimPool[entityId].StepPoweredClamped2(networkServe, _dt, num9 > 0 ? 2U : 1U);
                        if (num9 > 0 && entityAnimPool[entityId].state == 2U)
                        {
                            lock (mainPlayer.mecha)
                            {
                                int num10 = (int)((double)num9 * (double)networkServe);
                                mainPlayer.mecha.coreEnergy += (double)num10;
                                mainPlayer.mecha.MarkEnergyChange(2, (double)num10);
                                mainPlayer.mecha.AddChargerDevice(entityId);
                                if (mainPlayer.mecha.coreEnergy > mainPlayer.mecha.coreEnergyCap)
                                    mainPlayer.mecha.coreEnergy = mainPlayer.mecha.coreEnergyCap;
                            }
                        }
                    }
                    else if (entityPool[entityId].powerGenId == 0 && entityPool[entityId].powerAccId == 0 && entityPool[entityId].powerExcId == 0)
                    {
                        float networkServe = powerSystem.networkServes[networkId];
                        entityAnimPool[entityId].Step2((double)networkServe > 0.100000001490116 ? 1U : 0U, _dt, (float)((double)entityAnimPool[entityId].power * 0.97 + 0.03 * (double)networkServe), 0.4f);
                    }
                }
            }
            ExecuteTime[powerSystem.factory.index] = ExecuteTime[powerSystem.factory.index] * 0.99 + StopWatchList[powerSystem.factory.index].duration * 0.01;
            /** Patch note, skip the origin function **/
            return false;
        }
    }
}
