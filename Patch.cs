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

        public static void SetExchangerDischargePriority(EExchangerDischargeStrategy strategy)
        {
            ExchangerDischargeStrategy = strategy;
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
            float _dt = 0.0166666675f;
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
            Vector3 zero = Vector3.zero;
            Vector3 vector3_1 = Vector3.zero;
            float num6 = 0.0f;
            bool flag1;
            if (mainPlayer.mecha.coreEnergyCap - mainPlayer.mecha.coreEnergy > 0.0 && mainPlayer.isAlive && mainPlayer.planetId == planet.id)
            {
                float num7 = powerSystem.factory.planet.realRadius + 0.2f;
                Vector3 vector3_2 = isMultithreadMode ? powerSystem.multithreadPlayerPos : mainPlayer.position;
                float magnitude = vector3_2.magnitude;
                if ((double)magnitude > 0.0)
                    vector3_1 = vector3_2 * (num7 / magnitude);
                flag1 = (double)magnitude > (double)num7 - 30.0 && (double)magnitude < (double)num7 + 50.0;
            }
            else
                flag1 = false;
            lock (mainPlayer.mecha)
                num6 = Mathf.Pow(Mathf.Clamp01((float)(1.0 - mainPlayer.mecha.coreEnergy / mainPlayer.mecha.coreEnergyCap) * 10f), 0.75f);
            /** Patch note, replace private field dysonSphere with traversed local variable **/
            float response = dysonSphere != null ? dysonSphere.energyRespCoef : 0.0f;
            int num8 = (int)(Math.Min(Math.Abs(powerSystem.factory.planet.rotationPeriod), Math.Abs(powerSystem.factory.planet.orbitalPeriod)) * 60.0 / 2160.0);
            if (num8 < 1)
                num8 = 1;
            else if (num8 > 60)
                num8 = 60;
            if (powerSystem.factory.planet.singularity == EPlanetSingularity.TidalLocked)
                num8 = 60;
            bool flag2 = time % (long)num8 == 0L || GameMain.onceGameTick <= 2L;
            int num9 = (int)(time % 90L);
            EntityData[] entityPool = powerSystem.factory.entityPool;
            for (int index1 = 1; index1 < powerSystem.netCursor; ++index1)
            {
                PowerNetwork powerNetwork = powerSystem.netPool[index1];
                if (powerNetwork != null && powerNetwork.id == index1)
                {
                    List<int> consumers = powerNetwork.consumers;
                    int count1 = consumers.Count;
                    long num10 = 0;
                    for (int index2 = 0; index2 < count1; ++index2)
                    {
                        long requiredEnergy = powerSystem.consumerPool[consumers[index2]].requiredEnergy;
                        num10 += requiredEnergy;
                        num2 += requiredEnergy;
                    }
                    foreach (PowerNetworkStructures.Node node in powerNetwork.nodes)
                    {
                        int id = node.id;
                        if (powerSystem.nodePool[id].id == id && powerSystem.nodePool[id].isCharger)
                        {
                            if ((double)powerSystem.nodePool[id].coverRadius <= 20.0)
                            {
                                double num11 = 0.0;
                                if (flag1)
                                {
                                    double num12 = (double)powerSystem.nodePool[id].powerPoint.x * 0.98799997568130493 - (double)vector3_1.x;
                                    float num13 = powerSystem.nodePool[id].powerPoint.y * 0.988f - vector3_1.y;
                                    float num14 = powerSystem.nodePool[id].powerPoint.z * 0.988f - vector3_1.z;
                                    float coverRadius = powerSystem.nodePool[id].coverRadius;
                                    if ((double)coverRadius < 9.0)
                                        coverRadius += 2.01f;
                                    else if ((double)coverRadius > 20.0)
                                        coverRadius += 0.5f;
                                    float num15 = (float)(num12 * num12 + (double)num13 * (double)num13 + (double)num14 * (double)num14);
                                    float num16 = coverRadius * coverRadius;
                                    if ((double)num15 <= (double)num16)
                                    {
                                        double consumerRatio = powerNetwork.consumerRatio;
                                        float num17 = (float)(((double)num16 - (double)num15) / (3.0 * (double)coverRadius));
                                        if ((double)num17 > 1.0)
                                            num17 = 1f;
                                        num11 = (double)num6 * consumerRatio * consumerRatio * (double)num17;
                                    }
                                }
                                double num18 = (double)powerSystem.nodePool[id].idleEnergyPerTick * (1.0 - num11) + (double)powerSystem.nodePool[id].workEnergyPerTick * num11;
                                if (powerSystem.nodePool[id].requiredEnergy < powerSystem.nodePool[id].idleEnergyPerTick)
                                    powerSystem.nodePool[id].requiredEnergy = powerSystem.nodePool[id].idleEnergyPerTick;
                                if ((double)powerSystem.nodePool[id].requiredEnergy < num18 - 0.01)
                                {
                                    double num19 = num18 * 0.02 + (double)powerSystem.nodePool[id].requiredEnergy * 0.98;
                                    powerSystem.nodePool[id].requiredEnergy = (int)(num19 + 0.9999);
                                }
                                else if ((double)powerSystem.nodePool[id].requiredEnergy > num18 + 0.01)
                                {
                                    double num20 = num18 * 0.2 + (double)powerSystem.nodePool[id].requiredEnergy * 0.8;
                                    powerSystem.nodePool[id].requiredEnergy = (int)num20;
                                }
                            }
                            else
                                powerSystem.nodePool[id].requiredEnergy = powerSystem.nodePool[id].idleEnergyPerTick;
                            long requiredEnergy = (long)powerSystem.nodePool[id].requiredEnergy;
                            num10 += requiredEnergy;
                            num2 += requiredEnergy;
                        }
                    }
                    long num21 = 0;
                    List<int> exchangers = powerNetwork.exchangers;
                    int count2 = exchangers.Count;
                    long num22 = 0;
                    long num23 = 0;
                    long num24 = 0;
                    long num25 = 0;
                    for (int index3 = 0; index3 < count2; ++index3)
                    {
                        int index4 = exchangers[index3];
                        powerSystem.excPool[index4].StateUpdate();
                        powerSystem.excPool[index4].BeltUpdate(powerSystem.factory);
                        bool flag3 = (double)powerSystem.excPool[index4].state >= 1.0;
                        bool flag4 = (double)powerSystem.excPool[index4].state <= -1.0;
                        if (!flag3 && !flag4)
                        {
                            powerSystem.excPool[index4].capsCurrentTick = 0L;
                            powerSystem.excPool[index4].currEnergyPerTick = 0L;
                        }
                        int entityId = powerSystem.excPool[index4].entityId;
                        float num26 = (float)(((double)powerSystem.excPool[index4].state + 1.0) * (double)entityAnimPool[entityId].working_length * 0.5);
                        if ((double)num26 >= 3.9900000095367432)
                            num26 = 3.99f;
                        entityAnimPool[entityId].time = num26;
                        entityAnimPool[entityId].state = 0U;
                        entityAnimPool[entityId].power = (float)powerSystem.excPool[index4].currPoolEnergy / (float)powerSystem.excPool[index4].maxPoolEnergy;
                        if (flag4)
                        {
                            long num27 = powerSystem.excPool[index4].OutputCaps();
                            num24 += num27;
                            num21 = num24;
                            powerSystem.currentGeneratorCapacities[powerSystem.excPool[index4].subId] += num27;
                        }
                        else if (flag3)
                            num25 += powerSystem.excPool[index4].InputCaps();
                    }
                    /* Code Analysis Note: until this line
                     * num21 = num24 = ExcOutCap
                     * num10 = num2 = ConsReq
                     * num25 = ExcInpCap
                     */
                    /* Patch note: calculate clean energy capacity */
                    long cleanEnergyGeneratorCap = 0;

                    List<int> generators = powerNetwork.generators;
                    int count3 = generators.Count;
                    for (int index5 = 0; index5 < count3; ++index5)
                    {
                        int index6 = generators[index5];
                        long num28;
                        if (powerSystem.genPool[index6].wind)
                        {
                            num28 = powerSystem.genPool[index6].EnergyCap_Wind(windStrength);
                            num21 += num28;
                            cleanEnergyGeneratorCap += num28;   //Line Patch
                        }
                        else if (powerSystem.genPool[index6].photovoltaic)
                        {
                            if (flag2)
                            {
                                num28 = powerSystem.genPool[index6].EnergyCap_PV(normalized.x, normalized.y, normalized.z, luminosity);
                                num21 += num28;
                            }
                            else
                            {
                                num28 = powerSystem.genPool[index6].capacityCurrentTick;
                                num21 += num28;
                            }
                            cleanEnergyGeneratorCap += num28;  // Line Patch
                        }
                        else if (powerSystem.genPool[index6].gamma)
                        {
                            num28 = powerSystem.genPool[index6].EnergyCap_Gamma(response);
                            num21 += num28;
                            cleanEnergyGeneratorCap += num28;  // Line Patch
                        }
                        else if (powerSystem.genPool[index6].geothermal)
                        {
                            num28 = powerSystem.genPool[index6].EnergyCap_GTH();
                            num21 += num28;
                            cleanEnergyGeneratorCap += num28;  // Line Patch
                        }
                        else
                        {
                            num28 = powerSystem.genPool[index6].EnergyCap_Fuel();
                            num21 += num28;
                            entitySignPool[powerSystem.genPool[index6].entityId].signType = num28 > 30L ? 0U : 8U;
                        }
                        powerSystem.currentGeneratorCapacities[powerSystem.genPool[index6].subId] += num28;
                    }
                    num1 += num21 - num24;
                    long num29 = num21 - num10;
                    long num30 = 0;
                    /* Code Analysis Note: until this line
                     * num24 = ExcOutCap
                     * num21 = ExcOutCap + GenCap
                     * num1 = GenCap
                     * num10 = num2 = ConsReq
                     * num25 = ExcInpCap
                     * num29 = ExcOutCap + GenCap - ConsReq
                     */

                    if (num29 > 0L && powerNetwork.exportDemandRatio > 0.0)
                    {
                        if (powerNetwork.exportDemandRatio > 1.0)
                            powerNetwork.exportDemandRatio = 1.0;
                        num30 = (long)((double)num29 * powerNetwork.exportDemandRatio + 0.5);
                        num29 -= num30;
                        num10 += num30;
                    }

                    /* Code Analysis Note: until this line
                     * NOTE: powerNetwork.exportDemandRatio = 1.0 - (1.0 - powerNetwork.exportDemandRatio) * (1.0 - planetAtField.fillDemandRatio); regarding ATFields
                     * 
                     * num24 = ExcOutCap
                     * num21 = ExcOutCap + GenCap
                     * num1 = GenCap
                     * num2 = ConsReq
                     * num25 = ExcInpCap
                     * num30 = EnergyExport = (ExcOutCap + GenCap - ConsReq) * exportDemandRatio + 0.5
                     * num29 = ExcOutCap + GenCap - ConsReq - EnergyExport => this value determine Accs should charge
                     * num10 = ConsReq + EnergyExport
                     */

                    powerNetwork.exportDemandRatio = 0.0;
                    powerNetwork.energyStored = 0L;
                    List<int> accumulators = powerNetwork.accumulators;
                    int count4 = accumulators.Count;
                    long num31 = 0;
                    long num32 = 0;
                    if (num29 >= 0L)
                    {
                        for (int index7 = 0; index7 < count4; ++index7)
                        {
                            int index8 = accumulators[index7];
                            powerSystem.accPool[index8].curPower = 0L;
                            long num33 = powerSystem.accPool[index8].InputCap();
                            if (num33 > 0L)
                            {
                                long num34 = num33 < num29 ? num33 : num29;
                                powerSystem.accPool[index8].curEnergy += num34;
                                powerSystem.accPool[index8].curPower = num34;
                                num29 -= num34;
                                num31 += num34;
                                num4 += num34;
                            }
                            powerNetwork.energyStored += powerSystem.accPool[index8].curEnergy;
                            int entityId = powerSystem.accPool[index8].entityId;
                            entityAnimPool[entityId].state = powerSystem.accPool[index8].curEnergy > 0L ? 1U : 0U;
                            entityAnimPool[entityId].power = (float)powerSystem.accPool[index8].curEnergy / (float)powerSystem.accPool[index8].maxEnergy;
                        }
                    }
                    else
                    {
                        long num35 = -num29;
                        for (int index9 = 0; index9 < count4; ++index9)
                        {
                            int index10 = accumulators[index9];
                            powerSystem.accPool[index10].curPower = 0L;
                            long num36 = powerSystem.accPool[index10].OutputCap();
                            if (num36 > 0L)
                            {
                                long num37 = num36 < num35 ? num36 : num35;
                                powerSystem.accPool[index10].curEnergy -= num37;
                                powerSystem.accPool[index10].curPower = -num37;
                                num35 -= num37;
                                num32 += num37;
                                num3 += num37;
                            }
                            powerNetwork.energyStored += powerSystem.accPool[index10].curEnergy;
                            int entityId = powerSystem.accPool[index10].entityId;
                            entityAnimPool[entityId].state = powerSystem.accPool[index10].curEnergy > 0L ? 2U : 0U;
                            entityAnimPool[entityId].power = (float)powerSystem.accPool[index10].curEnergy / (float)powerSystem.accPool[index10].maxEnergy;
                        }
                    }
                    /* Code Analysis Note: until this line
                     * NOTE: powerNetwork.exportDemandRatio = 1.0 - (1.0 - powerNetwork.exportDemandRatio) * (1.0 - planetAtField.fillDemandRatio); regarding ATFields
                     * 
                     * num24 = ExcOutCap
                     * num21 = ExcOutCap + GenCap
                     * num1 = GenCap
                     * num2 = ConsReq
                     * num25 = ExcInpCap
                     * num30 = EnergyExport = (ExcOutCap + GenCap - ConsReq) * exportDemandRatio + 0.5
                     * num10 = ConsReq + EnergyExport
                     * num4 = num31 = AccCharged
                     * num32 = num3 = AccDischarged
                     * num29 = ExcOutCap + GenCap - ConsReq - EnergyExport - AccCharged  => this value determine following Exc should charge                     * 
                     */

                    /* Patch note : */
                    //double num38 = num29 < num25 ? (double) num29 / (double) num25 : 1.0;
                    num29 -= num24; // Exclude exchanger output
                    num29 = num29 > 0 ? num29 : 0;
                    double num38 = num29 < num25 ? (double)num29 / (double)num25 : 1.0;
                    /* Patch End
                    /* Code Analysis Note: ExcChargeWorkRatio = num38 = num29/num25 limited by <=1 */
                    for (int index11 = 0; index11 < count2; ++index11)
                    {
                        int index12 = exchangers[index11];
                        if ((double)powerSystem.excPool[index12].state >= 1.0 && num38 >= 0.0)
                        {
                            long num39 = (long)(num38 * (double)powerSystem.excPool[index12].capsCurrentTick + 0.99999);
                            long remaining = num29 < num39 ? num29 : num39;
                            long num40 = powerSystem.excPool[index12].InputUpdate(remaining, entityAnimPool, productRegister, consumeRegister);
                            num29 -= num40;
                            num22 += num40;
                            num4 += num40;
                        }
                        else
                            powerSystem.excPool[index12].currEnergyPerTick = 0L;
                    }

                    /* Code Analysis Note: until this line
                     * NOTE: powerNetwork.exportDemandRatio = 1.0 - (1.0 - powerNetwork.exportDemandRatio) * (1.0 - planetAtField.fillDemandRatio); regarding ATFields
                     * 
                     * num24 = ExcOutCap
                     * num21 = ExcOutCap + GenCap
                     * num1 = GenCap
                     * num2 = ConsReq
                     * num25 = ExcInpCap
                     * num30 = EnergyExport = (ExcOutCap + GenCap - ConsReq) * exportDemandRatio + 0.5
                     * num10 = ConsReq + EnergyExport
                     * num29 = ExcOutCap + GenCap - ConsReq - EnergyExport - AccCharged - ExcCharged  => should be 0 that remain nothing
                     * num22 = ExcCharged => Exchanger has lowest charge priority
                     * num4 = AccCharged + ExcCharged
                     * num31 = AccCharged
                     * num32 = num3 = AccDischarged
                     */

                    // num41 = (ExcOutCap + GenCap) < (ConsReq + EnergyExport) + ExcCharged ? (ExcOutCap + GenCap) + AccCharged + ExcCharged :  (CosmPowerReq + ExportReq) +  AccCharged + ExcCharged; 
                    // Following loop will calculate energy actually discharged, num41 will determine energy should be discharged from exchangers
                    long num41 = num21 < num10 + num22 ? num21 + num31 + num22 : num10 + num31 + num22;
                    //Patch note: change the exchanger discharge target
                    double num42;
                    {
                        long energyToGenerate;
                        long energyExcDischarge;
                        switch (ExchangerDischargeStrategy)
                        {
                            default:
                            case EExchangerDischargeStrategy.EQUAL_AS_FUEL_GENERATOR:
                                long fuelAndDiscCap = (num21 - cleanEnergyGeneratorCap);
                                energyToGenerate = num41 - cleanEnergyGeneratorCap;
                                num42 = (fuelAndDiscCap <= 0 || energyToGenerate <= 0) ? 0.0 :
                                        (energyToGenerate < fuelAndDiscCap) ? ((double)energyToGenerate / (double)fuelAndDiscCap) : 1.0;
                                break;
                            case EExchangerDischargeStrategy.DISCHARGE_FIRST:
                                energyToGenerate = cleanEnergyGeneratorCap;
                                energyExcDischarge = num41 > energyToGenerate ? num41 - energyToGenerate : 0;
                                num42 = energyExcDischarge < num24 ? (double)energyExcDischarge / (double)num24 : 1.0;
                                break;
                            case EExchangerDischargeStrategy.DISCHARGE_LAST:
                                energyToGenerate = num21 - num24;
                                energyExcDischarge = num41 > energyToGenerate ? num41 - energyToGenerate : 0;
                                num42 = energyExcDischarge < num24 ? (double)energyExcDischarge / (double)num24 : 1.0;
                                break;

                        }
                    }

                    for (int index13 = 0; index13 < count2; ++index13)
                    {
                        int index14 = exchangers[index13];
                        if ((double)powerSystem.excPool[index14].state <= -1.0)
                        {
                            long num43 = (long)(num42 * (double)powerSystem.excPool[index14].capsCurrentTick + 0.99999);
                            long energyPay = num41 < num43 ? num41 : num43;
                            long num44 = powerSystem.excPool[index14].OutputUpdate(energyPay, entityAnimPool, productRegister, consumeRegister);
                            num23 += num44;
                            num3 += num44;
                            num41 -= num44;
                        }
                    }

                    /* Code Analysis Note: until this line
                     * NOTE: powerNetwork.exportDemandRatio = 1.0 - (1.0 - powerNetwork.exportDemandRatio) * (1.0 - planetAtField.fillDemandRatio); regarding ATFields
                     * 
                     * num24 = ExcOutCap
                     * num21 = ExcOutCap + GenCap
                     * num1 = GenCap
                     * num2 = ConsReq
                     * num25 = ExcInpCap
                     * num30 = EnergyExport = (ExcOutCap + GenCap - ConsReq) * exportDemandRatio + 0.5
                     * num10 = ConsReq + EnergyExport
                     * num29 = ExcOutCap + GenCap - ConsReq - EnergyExport - AccCharged - ExcCharged  => should be 0 that remain nothing
                     * num22 = ExcCharged => Exchanger has lowest charge priority
                     * num4 = AccCharged + ExcCharged = TotalCharge
                     * num31 = AccCharged
                     * num32 = AccDischarged
                     * num3 = AccDischarged + ExcDischarged = TotalDischarge
                     * num41 = (ExcOutCap + GenCap) + AccCharged + ExcCharged - ExcDischarged =>(not enough power)
                     * or    = (CosmPowerReq + EnergyExport) +  AccCharged + ExcCharged  - ExcDischarged=>(enough power)
                     * num42 = ExcDischargeWorkRatio
                     * num23 = num3 = ExcDischarged
                     */

                    powerNetwork.energyCapacity = num21 - num24;//GenCap = (ExcOutCap + GenCap) - ExcOutCap
                    powerNetwork.energyRequired = num10 - num30;//ConsReq = (CosmReq + EnergyExport) - EnergyExport
                    powerNetwork.energyExport = num30;//EnergyExport
                    powerNetwork.energyServed = num21 + num32 < num10 ? num21 + num32 : num10;// ExcOutCap + GenCap + AccDischarged < (CosmPowerReq + EnergyExport)？ ExcOutCap + GenCap + AccDischarged  ： (CosmPowerReq + ExportReq)
                    powerNetwork.energyAccumulated = num31 - num32;//AccEnergy = AccCharged - AccDischarged
                    powerNetwork.energyExchanged = num22 - num23;//ExcCharged - ExcDischarged
                    powerNetwork.energyExchangedInputTotal = num22;//ExcCharged
                    powerNetwork.energyExchangedOutputTotal = num23;//ExcDischarged
                    if (num30 > 0L)
                    {
                        PlanetATField planetAtField = powerSystem.factory.planetATField;
                        planetAtField.energy += num30;
                        planetAtField.atFieldRechargeCurrent = num30 * 60L;
                    }
                    //(ExcOutCap + GenCap) + AccDischargeCap = TotalPowerCap
                    long num45 = num21 + num32;
                    //(CosmReq + ExportReq) + AccChargeReq = TotalPowerReq
                    long num46 = num10 + num31;
                    // if (TotalPowerCap > TotalPowerReq) (num5 = CosmPowerReq + energyExport) else num5 = totalPowerCap
                    num5 += num45 >= num46 ? num2 + num30 : num45;
                    // ExcOutputMargin = (ExcDischarged - TotalPowerReq) under limited by 0??
                    long num47 = num23 - num46 > 0L ? num23 - num46 : 0L;
                    //ConsumerRatio = TotalPowerCap / TotalPowerReq, upper limited by 1
                    double num48 = num45 >= num46 ? 1.0 : (double)num45 / (double)num46;
                    // ? num49 = TotalPowerReq + ExcCharged - max(0, (ExcDischarged - TotalPowerReq)) almost = TotalPowerReq + ExcCharged
                    long num49 = num46 + (num22 - num47);
                    // ?????EnergyCanServeByAccAndGen = (ExcOutCap + GenCap) + AccDischarged - ExcDischarged
                    long num50 = num45 - num23;
                    //GenerateRatio =  EnergyReqGen/EnergyCanServeByAccAndGen upper limited by 1
                    double num51 = num50 > num49 ? (double)num49 / (double)num50 : 1.0;
                    powerNetwork.consumerRatio = num48;
                    powerNetwork.generaterRatio = num51;
                    float num52 = num50 > 0L || powerNetwork.energyStored > 0L || num23 > 0L ? (float)num48 : 0.0f;
                    //If not EnergyReqGen or energyStored or ExcOutput, set 0
                    float num53 = num50 > 0L || powerNetwork.energyStored > 0L || num23 > 0L ? (float)num51 : 0.0f;
                    powerSystem.networkServes[index1] = num52;
                    powerSystem.networkGenerates[index1] = num53;

                    //Patch Note : Calculate clean energy generator generaterRatio, and fuel generaterRatio seperately and apply to them 
                    long totalEnergyNeedGenerate = num41;
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
                    for (int index15 = 0; index15 < count3; ++index15)
                    {
                        int index16 = generators[index15];
                        long energy = 0;
                        float _speed1 = 1f;
                        bool flag5 = !powerSystem.genPool[index16].wind && !powerSystem.genPool[index16].photovoltaic && !powerSystem.genPool[index16].gamma && !powerSystem.genPool[index16].geothermal;
                        if (flag5)
                            powerSystem.genPool[index16].currentStrength = num41 <= 0L || powerSystem.genPool[index16].capacityCurrentTick <= 0L ? 0.0f : 1f;
                        if (num41 > 0L && powerSystem.genPool[index16].productId == 0)
                        {
                            /* Patch note: if generator uses clean energy, make it output max energy */
                            double generaterRatio = flag5 ? generatorRatioFuel : generatorRatioClean;
                            long num54 = (long)(generaterRatio * (double)powerSystem.genPool[index16].capacityCurrentTick + 0.99999);

                            energy = num41 < num54 ? num41 : num54;
                            if (energy > 0L)
                            {
                                num41 -= energy;
                                if (flag5)
                                {
                                    powerSystem.genPool[index16].GenEnergyByFuel(energy, consumeRegister);
                                    _speed1 = 2f;
                                }
                            }
                        }
                        powerSystem.genPool[index16].generateCurrentTick = energy;
                        int entityId = powerSystem.genPool[index16].entityId;
                        if (powerSystem.genPool[index16].wind)
                        {
                            float _speed2 = 0.7f;
                            entityAnimPool[entityId].Step2((double)entityAnimPool[entityId].power > 0.10000000149011612 || energy > 0L ? 1U : 0U, _dt, windStrength, _speed2);
                        }
                        else if (powerSystem.genPool[index16].gamma)
                        {
                            bool keyFrame = (index16 + num9) % 90 == 0;
                            powerSystem.genPool[index16].GameTick_Gamma(useIonLayer, useCata, keyFrame, powerSystem.factory, productRegister, consumeRegister);
                            entityAnimPool[entityId].time += _dt;
                            if ((double)entityAnimPool[entityId].time > 1.0)
                                --entityAnimPool[entityId].time;
                            entityAnimPool[entityId].power = (float)powerSystem.genPool[index16].capacityCurrentTick / (float)powerSystem.genPool[index16].genEnergyPerTick;
                            entityAnimPool[entityId].state = (uint)((powerSystem.genPool[index16].productId > 0 ? 2 : 0) + (powerSystem.genPool[index16].catalystPoint > 0 ? 1 : 0));
                            entityAnimPool[entityId].working_length = (float)((double)entityAnimPool[entityId].working_length * 0.99000000953674316 + (powerSystem.genPool[index16].catalystPoint > 0 ? 0.0099999997764825821 : 0.0));
                            if (isActive)
                                entitySignPool[entityId].signType = (double)powerSystem.genPool[index16].productCount < 20.0 ? 0U : 6U;
                        }
                        else if (powerSystem.genPool[index16].fuelMask > (short)1)
                        {
                            float _power = (float)((double)entityAnimPool[entityId].power * 0.98 + 0.02 * (energy > 0L ? 1.0 : 0.0));
                            if (energy > 0L && (double)_power < 0.0)
                                _power = 0.0f;
                            entityAnimPool[entityId].Step2((double)entityAnimPool[entityId].power > 0.10000000149011612 || energy > 0L ? 1U : 0U, _dt, _power, _speed1);
                        }
                        else if (powerSystem.genPool[index16].geothermal)
                        {
                            float num55 = powerSystem.genPool[index16].warmup + powerSystem.genPool[index16].warmupSpeed;
                            powerSystem.genPool[index16].warmup = (double)num55 > 1.0 ? 1f : ((double)num55 < 0.0 ? 0.0f : num55);
                            entityAnimPool[entityId].state = energy > 0L ? 1U : 0U;
                            entityAnimPool[entityId].Step(entityAnimPool[entityId].state, _dt, 2f, 0.0f);
                            entityAnimPool[entityId].working_length = powerSystem.genPool[index16].warmup;
                            if (energy > 0L)
                            {
                                if ((double)entityAnimPool[entityId].power < 1.0)
                                    entityAnimPool[entityId].power += _dt / 6f;
                            }
                            else if ((double)entityAnimPool[entityId].power > 0.0)
                                entityAnimPool[entityId].power -= _dt / 6f;
                            entityAnimPool[entityId].prepare_length += (float)(3.1415927410125732 * (double)_dt / 8.0);
                            if ((double)entityAnimPool[entityId].prepare_length > 6.2831854820251465)
                                entityAnimPool[entityId].prepare_length -= 6.28318548f;
                        }
                        else
                        {
                            float _power = (float)((double)entityAnimPool[entityId].power * 0.98 + 0.02 * (double)energy / (double)powerSystem.genPool[index16].genEnergyPerTick);
                            if (energy > 0L && (double)_power < 0.20000000298023224)
                                _power = 0.2f;
                            entityAnimPool[entityId].Step2((double)entityAnimPool[entityId].power > 0.10000000149011612 || energy > 0L ? 1U : 0U, _dt, _power, _speed1);
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
                for (int index17 = 0; index17 < powerSystem.netCursor; ++index17)
                {
                    PowerNetwork powerNetwork = powerSystem.netPool[index17];
                    if (powerNetwork != null && powerNetwork.id == index17)
                    {
                        List<int> consumers = powerNetwork.consumers;
                        int count = consumers.Count;
                        if (index17 == 0)
                        {
                            for (int index18 = 0; index18 < count; ++index18)
                                entitySignPool[powerSystem.consumerPool[consumers[index18]].entityId].signType = 1U;
                        }
                        else if (powerNetwork.consumerRatio < 0.10000000149011612)
                        {
                            for (int index19 = 0; index19 < count; ++index19)
                                entitySignPool[powerSystem.consumerPool[consumers[index19]].entityId].signType = 2U;
                        }
                        else if (powerNetwork.consumerRatio < 0.5)
                        {
                            for (int index20 = 0; index20 < count; ++index20)
                                entitySignPool[powerSystem.consumerPool[consumers[index20]].entityId].signType = 3U;
                        }
                        else
                        {
                            for (int index21 = 0; index21 < count; ++index21)
                                entitySignPool[powerSystem.consumerPool[consumers[index21]].entityId].signType = 0U;
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
                        int num56 = powerSystem.nodePool[index].requiredEnergy - powerSystem.nodePool[index].idleEnergyPerTick;
                        if ((double)powerSystem.nodePool[index].coverRadius < 20.0)
                            entityAnimPool[entityId].StepPoweredClamped(networkServe, _dt, num56 > 0 ? 2U : 1U);
                        else
                            entityAnimPool[entityId].StepPoweredClamped2(networkServe, _dt, num56 > 0 ? 2U : 1U);
                        if (num56 > 0 && entityAnimPool[entityId].state == 2U)
                        {
                            lock (mainPlayer.mecha)
                            {
                                int change = (int)((double)num56 * (double)networkServe);
                                mainPlayer.mecha.coreEnergy += (double)change;
                                mainPlayer.mecha.MarkEnergyChange(2, (double)change);
                                mainPlayer.mecha.AddChargerDevice(entityId);
                                if (mainPlayer.mecha.coreEnergy > mainPlayer.mecha.coreEnergyCap)
                                    mainPlayer.mecha.coreEnergy = mainPlayer.mecha.coreEnergyCap;
                            }
                        }
                    }
                    else if (entityPool[entityId].powerGenId == 0 && entityPool[entityId].powerAccId == 0 && entityPool[entityId].powerExcId == 0)
                    {
                        float networkServe = powerSystem.networkServes[networkId];
                        entityAnimPool[entityId].Step2((double)networkServe > 0.10000000149011612 ? 1U : 0U, _dt, (float)((double)entityAnimPool[entityId].power * 0.97 + 0.03 * (double)networkServe), 0.4f);
                    }
                }
            }
            ExecuteTime[powerSystem.factory.index] = ExecuteTime[powerSystem.factory.index] * 0.99 + StopWatchList[powerSystem.factory.index].duration * 0.01;
            /** Patch note, skip the origin function **/
            return false;
        }
    }
}
