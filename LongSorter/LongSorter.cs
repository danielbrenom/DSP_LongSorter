using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace LongSorter
{
    [BepInPlugin(__GUID__, __NAME__, "1.2.2")]
    public class LongSorter : BaseUnityPlugin
    {
        public const string __NAME__ = "LongSorter";
        public const string __GUID__ = "com.hetima.dsp." + __NAME__;

        new internal static ManualLogSource Logger;
        void Awake()
        {
            Logger = base.Logger;
            //Logger.LogInfo("Awake");

            new Harmony(__GUID__).PatchAll(typeof(Patch));
        }


        static class Patch
        {

            public static IEnumerable<CodeInstruction> CheckBuildConditions_Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                //if (a > b)
                //{
                //    buildPreview.condition = EBuildCondition.TooFar;
                //}
                //の b を変更する 2箇所ある
                //Psotfixでもいいけど、その場で変更しないと衝突判定がスルーされる
                List<CodeInstruction> ins = instructions.ToList();
                List<int> patchPos = new List<int>(2);

                FieldInfo f = AccessTools.Field(typeof(BuildPreview), nameof(BuildPreview.condition));
                MethodInfo m = typeof(LongSorter.Patch).GetMethod("LengthCorrection");

                for (int i = 0; i < ins.Count; i++)
                {
                    if (ins[i].opcode == OpCodes.Stfld && ins[i].operand is FieldInfo o && o == f)
                    {
                        //EBuildCondition.TooFar == 13
                        const int tooFarOperand = (int)EBuildCondition.TooFar;
                        if (ins[i - 1].opcode != OpCodes.Ldc_I4_S || !(ins[i - 1].operand is sbyte o2) || o2 != tooFarOperand
                            && (ins[i - 3].opcode == OpCodes.Ble_Un || ins[i - 3].opcode == OpCodes.Ble_Un_S))
                        {
                            patchPos.Add(i - 5);
                            if (patchPos.Count == 2)
                            {
                                break;
                            }
                        }
                    }
                }

                for (int i = 0; i < ins.Count; i++)
                {
                    if (patchPos.Contains(i))
                    {
                        // ldloc.s
                        //+1 ldloc.s 対象
                        //+2 ble.un.s
                        yield return ins[i];
                        yield return ins[i + 1];
                        yield return new CodeInstruction(OpCodes.Call, m);
                        yield return ins[i + 2];
                        i += 2;
                    }
                    else
                    {
                        yield return ins[i];
                    }
                }
            }


            [HarmonyTranspiler, HarmonyPatch(typeof(BuildTool_Inserter), "CheckBuildConditions")]
            public static IEnumerable<CodeInstruction> BuildTool_Inserter_Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return CheckBuildConditions_Transpiler(instructions);
            }

            //[HarmonyTranspiler, HarmonyPatch(typeof(BuildTool_Click), "CheckBuildConditions")]
            //public static IEnumerable<CodeInstruction> BuildTool_Click_Transpiler(IEnumerable<CodeInstruction> instructions)
            //{
            //}
            


            [HarmonyTranspiler, HarmonyPatch(typeof(BuildTool_Inserter), "DeterminePreviews")]
            public static IEnumerable<CodeInstruction> BuildTool_Inserter_DeterminePreviews_Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                MethodInfo m = typeof(System.Collections.Generic.List<BuildTool_Inserter.PosePair>).GetMethod("Add");
                MethodInfo ac11 = typeof(LongSorter.Patch).GetMethod("AngleCorrection11");
                MethodInfo ac14 = typeof(LongSorter.Patch).GetMethod("AngleCorrection14");
                MethodInfo ac40 = typeof(LongSorter.Patch).GetMethod("AngleCorrection40");
                List<CodeInstruction> ins = instructions.ToList();

                //BuildTool_Inserter.PosePair.Add() するちょっと前に比較してる
                //ldc.r4 11 or 14
                for (int i = 0; i < ins.Count; i++)
                {
                    if (ins[i].opcode == OpCodes.Callvirt && ins[i].operand is MethodInfo o && o == m)
                    {
                        for (int j = i-1; j > i - 22; j--)
                        {
                            if (ins[j].opcode == OpCodes.Ldc_R4 && ins[j].operand is float num1)
                            {
                                if (num1 == 11f)
                                {
                                    ins[j].opcode = OpCodes.Call;
                                    ins[j].operand = ac11;
                                    break;
                                }
                                else if (num1 == 14f)
                                {
                                    ins[j].opcode = OpCodes.Call;
                                    ins[j].operand = ac14;
                                    break;
                                }
                            }
                        }
                    }

                    //IL_0c44: ldloc.s num1_V_54
                    //IL_0c46: ldc.r4  40
                    if (ac40 != null && ins[i].opcode == OpCodes.Ldloc_S && i + 1 < ins.Count)
                    {
                        if (ins[i + 1].opcode == OpCodes.Ldc_R4 && ins[i + 1].operand is float num2 && num2 == 40f)
                        {
                            ins[i + 1].opcode = OpCodes.Call;
                            ins[i + 1].operand = ac40;
                            ac40 = null; //1回だけ
                        }
                    }
                }
                return ins.AsEnumerable();
            }

            [HarmonyPostfix, HarmonyPatch(typeof(BuildTool_Inserter), "CheckBuildConditions")]
            public static void BuildTool_Inserter_CheckBuildConditions_Postfix(BuildTool_Inserter __instance, ref bool __result)
            {
                if (!__result && LongMode() && __instance.buildPreviews.Count == 1)
                {
                    BuildPreview buildPreview = __instance.buildPreviews[0];
                    if (buildPreview.condition == EBuildCondition.Collide)
                    {
                        //ベルトの衝突は無視
                        if (canIgnoreCollide(__instance, buildPreview))
                        {
                            __result = true;
                        }
                    }
                    else if (buildPreview.condition == EBuildCondition.TooClose)
                    {
                        //真上に接続 (ついでに近すぎる建物間の接続判定も緩くなる)
                        float distance = Vector3.Distance(buildPreview.lpos, buildPreview.lpos2);
                        if (distance > PlanetGrid.kAltGrid - 0.2)
                        {
                            __result = true;
                        }
                    }

                    if (__result)
                    {
                        buildPreview.condition = EBuildCondition.Ok;
                        __instance.actionBuild.model.cursorText = buildPreview.conditionText;
                        __instance.actionBuild.model.cursorState = 0;
                        if (!VFInput.onGUI)
                        {
                            UICursor.SetCursor(ECursor.Default);
                        }
                    }
                }
            }

            public static bool canIgnoreCollide(BuildTool_Inserter tool, BuildPreview buildPreview)
            {
                if (buildPreview.desc.hasBuildCollider)
                {
                    for (int i = 0; i < BuildToolAccess.TmpColsLength(); i++)
                    {
                        Collider collider = BuildToolAccess.TmpCols()[i];
                        ColliderData colliderData;
                        if (tool.planet.physics.GetColliderData(collider, out colliderData) && colliderData.objType == EObjectType.Entity)
                        {
                            int eid = colliderData.objId;
                            EntityData e = tool.planet.factory.entityPool[eid];
                            if (e.beltId <= 0)
                            {
                                return false;
                            }
                        }
                    }
                    return true;
                }
                return false;
            }

            public static bool LongMode()
            {
                return VFInput.control;
            }

            public static float LengthCorrection(float val)
            {
                return LongMode() ? val * 5f : val;
            }

            //角度なので90とか180とか返しとけば足りそう
            public static float AngleCorrection11()
            {
                return LongMode() ? 1000f : 11f;
            }
            public static float AngleCorrection14()
            {
                return LongMode() ? 1000f : 14f;
            }
            public static float AngleCorrection40()
            {
                //この角度が大きいと斜めに置く候補が出るので真上に接続しにくい
                //shiftも押すと真上に繋げやすくする
                return LongMode() ? (VFInput.shift ? 6f : 1000f) : 40f;
            }

            // since 0.9.27.15466
            // For now, this method only checks the distance, so simply do not execute it
            [HarmonyPrefix, HarmonyPatch(typeof(PlanetFactory), "OnInserterBuilt")]
            public static bool PlanetFactory_OnInserterBuilt_Prefix()
            {
                return false;
            }
        }

        public class BuildToolAccess : BuildTool
        {
            public static int TmpColsLength()
            {
                int result = 0;
                for (int i = 0; i < _tmp_cols.Length; i++)
                {
                    if (_tmp_cols[i] != null)
                    {
                        result++;
                    }
                    else
                    {
                        break;
                    }
                }
                return result;
            }
            public static Collider[] TmpCols()
            {
                return _tmp_cols;
            }

        }

    }
}
