using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LongSorter
{
    [BepInPlugin(Guid, Name, "1.3.2")]
    public class LongSorter : BaseUnityPlugin
    {
        private const string Name = "LongSorter";
        private const string Guid = "com.sylf.dsp." + Name;

        private static ManualLogSource _logger;

        private void Awake()
        {
            _logger = Logger;
            new Harmony(Guid).PatchAll(typeof(Patch));
        }


        internal static class Patch
        {
            private static IEnumerable<CodeInstruction> CheckBuildConditions_Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                //if (a > b)
                //{
                //    buildPreview.condition = EBuildCondition.TooFar;
                //}
                //の b を変更する 2箇所ある
                //Psotfixでもいいけど、その場で変更しないと衝突判定がスルーされる
                var ins = instructions.ToList();
                var patchPos = new List<int>(2);
                var f = AccessTools.Field(typeof(BuildPreview), nameof(BuildPreview.condition));
                var m = typeof(Patch).GetMethod("LengthCorrection");
                for (var i = 0; i < ins.Count; i++)
                {
                    if (ins[i].opcode != OpCodes.Stfld || !(ins[i].operand is FieldInfo o) || o != f)
                        continue;
                    //EBuildCondition.TooFar == 14
                    const int tooFarOperand = (int)EBuildCondition.TooFar;
                    if (ins[i - 1].opcode != OpCodes.Ldc_I4_S || !(ins[i - 1].operand is sbyte o2) || o2 != tooFarOperand
                        || (ins[i - 3].opcode != OpCodes.Ble_Un && ins[i - 3].opcode != OpCodes.Ble_Un_S))
                        continue;
                    patchPos.Add(i - 5);
                    if (patchPos.Count == 2)
                    {
                        break;
                    }
                }

                for (var i = 0; i < ins.Count; i++)
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
            public static IEnumerable<CodeInstruction> BuildTool_Inserter_Transpiler(IEnumerable<CodeInstruction> instructions) => CheckBuildConditions_Transpiler(instructions);

            [HarmonyTranspiler, HarmonyPatch(typeof(BuildTool_Inserter), "DeterminePreviews")]
            public static IEnumerable<CodeInstruction> BuildTool_Inserter_DeterminePreviews_Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var m = typeof(List<BuildTool_Inserter.PosePair>).GetMethod("Add");
                var ac11 = typeof(Patch).GetMethod("AngleCorrection11");
                var ac14 = typeof(Patch).GetMethod("AngleCorrection14");
                var ac40 = typeof(Patch).GetMethod("AngleCorrection40");
                var ins = instructions.ToList();

                //BuildTool_Inserter.PosePair.Add() するちょっと前に比較してる
                //ldc.r4 11 or 14
                for (var i = 0; i < ins.Count; i++)
                {
                    if (ins[i].opcode == OpCodes.Callvirt && ins[i].operand is MethodInfo o && o == m)
                    {
                        for (var j = i - 1; j > i - 22; j--)
                        {
                            if (ins[j].opcode != OpCodes.Ldc_R4 || !(ins[j].operand is float num1))
                                continue;
                            if (Mathf.Approximately(num1, 11f))
                            {
                                ins[j].opcode = OpCodes.Call;
                                ins[j].operand = ac11;
                                break;
                            }

                            if (!Mathf.Approximately(num1, 14f))
                                continue;
                            ins[j].opcode = OpCodes.Call;
                            ins[j].operand = ac14;
                            break;
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
                if (__result || !LongMode() || __instance.buildPreviews.Count != 1)
                    return;
                var buildPreview = __instance.buildPreviews[0];
                switch (buildPreview.condition)
                {
                    case EBuildCondition.Collide:
                    {
                        //ベルトの衝突は無視
                        if (CanIgnoreCollide(__instance, buildPreview))
                        {
                            __result = true;
                        }

                        break;
                    }
                    case EBuildCondition.TooClose:
                    {
                        //真上に接続 (ついでに近すぎる建物間の接続判定も緩くなる)
                        var distance = Vector3.Distance(buildPreview.lpos, buildPreview.lpos2);
                        if (distance > PlanetGrid.kAltGrid - 0.2)
                        {
                            __result = true;
                        }

                        break;
                    }
                }

                if (!__result)
                    return;
                buildPreview.condition = EBuildCondition.Ok;
                __instance.actionBuild.model.cursorText = buildPreview.conditionText;
                __instance.actionBuild.model.cursorState = 0;
                if (!VFInput.onGUI)
                {
                    UICursor.SetCursor(ECursor.Default);
                }
            }

            private static bool CanIgnoreCollide(BuildTool_Inserter tool, BuildPreview buildPreview)
            {
                if (!buildPreview.desc.hasBuildCollider)
                    return false;
                for (var i = 0; i < BuildToolAccess.TmpColsLength(); i++)
                {
                    var collider = BuildToolAccess.TmpCols()[i];
                    if (!tool.planet.physics.GetColliderData(collider, out var colliderData) || colliderData.objType != EObjectType.Entity)
                        continue;
                    var eid = colliderData.objId;
                    var e = tool.planet.factory.entityPool[eid];
                    if (e.beltId <= 0)
                    {
                        return false;
                    }
                }

                return true;
            }

            private static bool LongMode() => VFInput.control;

            public static float LengthCorrection(float val) => LongMode() ? val * 5f : val;

            //角度なので90とか180とか返しとけば足りそう
            public static float AngleCorrection11() => LongMode() ? 1000f : 11f;

            public static float AngleCorrection14() => LongMode() ? 1000f : 14f;

            public static float AngleCorrection40() =>
                //この角度が大きいと斜めに置く候補が出るので真上に接続しにくい
                //shiftも押すと真上に繋げやすくする
                LongMode() ? (VFInput.shift ? 6f : 1000f) : 40f;

            // since 0.9.27.15466
            // For now, this method only checks the distance, so simply do not execute it
            [HarmonyPrefix, HarmonyPatch(typeof(PlanetFactory), "OnInserterBuilt")]
            public static bool PlanetFactory_OnInserterBuilt_Prefix() => false;
        }

        private class BuildToolAccess : BuildTool
        {
            public static int TmpColsLength()
            {
                var result = 0;
                foreach (var collider in _tmp_cols)
                {
                    if (collider != null)
                    {
                        result++;
                        continue;
                    }

                    break;
                }

                return result;
            }

            public static Collider[] TmpCols() => _tmp_cols;
        }
    }
}