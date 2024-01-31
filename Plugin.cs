using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx;
using GameData.Item;
using HarmonyLib;
using HarmonyLib.Tools;
using TMPro;
using UnityEngine;


namespace ProdPanel;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        // Plugin startup logic
        HarmonyFileLog.Enabled = true;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        Harmony harmony = new(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();
    }
}

[HarmonyPatch]
static class ItemTrackingLocationSortingPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemTrackingLocationSorting), "Start")]
    static void Start(ItemTrackingLocationSorting __instance)
    {
        // new text
        GameObject prodText = UnityEngine.Object.Instantiate(GameObject.Find("SellersText"), __instance.transform.GetChild(1));
        prodText.transform.SetSiblingIndex(7);
        prodText.name = "ProducersText";
        prodText.GetComponent<TextMeshProUGUI>().text = "Prod";
        // text in the middle of the panel
        prodText.transform.localPosition = new Vector3(0f, prodText.transform.localPosition.y, prodText.transform.localPosition.z);

        // old pagingrows
        GameObject sellPagingRow = GameObject.Find("SellerContentGroup");
        GameObject buyPagingRow = GameObject.Find("BuyersContentGroup");

        // new pagingrow
        GameObject prodPagingRow = UnityEngine.Object.Instantiate(GameObject.Find("SellerContentGroup"), __instance.transform.GetChild(1));
        prodPagingRow.transform.SetSiblingIndex(18);
        prodPagingRow.name = "ProducterContentGroup";

        // resizing prodPagingRow
        prodPagingRow.transform.localPosition = new Vector3(0f, prodPagingRow.transform.localPosition.y, prodPagingRow.transform.localPosition.z);
        prodPagingRow.transform.localScale = new Vector3(0.7f, 1f, 1f);
        
        // resizing sellPagingRow
        sellPagingRow.transform.localPosition = new Vector3(-1150f, sellPagingRow.transform.localPosition.y, sellPagingRow.transform.localPosition.z);
        sellPagingRow.transform.localScale = new Vector3(0.7f, 1f, 1f);

        // resizing buyPagingRow
        buyPagingRow.transform.localPosition = new Vector3(1150f, buyPagingRow.transform.localPosition.y, buyPagingRow.transform.localPosition.z);
        buyPagingRow.transform.localScale = new Vector3(0.7f, 1f, 1f);

        // add setupOnUpdate 
        prodPagingRow.transform.GetChild(1).GetComponent<PagingRow>().SetupOnUpdatePage(new OnUpdatePage(__instance.PopulateProducterContent));
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ItemTrackingLocationSorting), "OnOpenPopup")]
    static bool OnOpenPopup()
    {
        GameObject.Find("ProducterContentGroup").transform.GetChild(1).GetComponent<PagingRow>().ResetCurrentPage();
        return true;
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemTrackingLocationSorting), "OnClosePopup")]
    static void OnClosePopup()
    {
        ItemTrackingLocationTab[] prodLocations = GameObject.Find("ProducterContentGroup").transform.GetComponentsInChildren<ItemTrackingLocationTab>(true);
		for (int i = 0; i < prodLocations.Length; i++)
		{
			prodLocations[i].OnClosePopup();
		}
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemTrackingLocationSorting), "SetupPageNumOnOpenPopup")]
    static void SetupPageNumOnOpenPopup(ItemTrackingLocationSorting __instance)
    {
        PagingRow prodPagingRow = GameObject.Find("ProducterContentGroup").transform.GetChild(1).GetComponent<PagingRow>();
		ItemTrackingLocationTab[] prodLocations = GameObject.Find("ProducterContentGroup").transform.GetComponentsInChildren<ItemTrackingLocationTab>(true);

        // reset page
		prodPagingRow.ResetCurrentPage();

        // some reflexion since these are private
        ItemData showingItem = AccessTools.Field(typeof(ItemTrackingLocationSorting), "showingItem").GetValue(__instance) as ItemData;
        List<ItemTrackingData> trackingDataList = AccessTools.Field(typeof(ItemTrackingLocationSorting), "trackingDataList").GetValue(__instance) as List<ItemTrackingData>;

        // since we dont have a currentProductingDict (no more field with Harmony)
        // recompute it each time
        int currentProductingCount = trackingDataList.Where(x => x.IsStoreOpened &&
                !x.MerchantTags.Any() && 
                (x.MerchantOffer.StoreOffer.Contains("RC_" + showingItem.Id + "_0") || 
                x.MerchantOffer.StoreOffer.Contains("RC_" + showingItem.Id + "_1") || 
                x.MerchantOffer.StoreOffer.Contains("RC_" + showingItem.Id + "_2") || 
                x.MerchantOffer.StoreOffer.Contains("RC_" + showingItem.Id + "_3") || 
                x.MerchantOffer.StoreOffer.Contains("RC_" + showingItem.Id + "_4") || 
                x.MerchantOffer.StoreOffer.Contains("RC_" + showingItem.Id + "_5")))
                .GroupBy( x => x.PortName )
                .Count();
        
        prodPagingRow.ReloadMaxpage(currentProductingCount / prodLocations.Length, true);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemTrackingLocationSorting), "OnSortDistance")]
    static void OnSortDistance(ItemTrackingLocationSorting __instance)
    {
		__instance.PopulateProducterContent();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemTrackingLocationSorting), "OnSortProfit")]
    static void OnSortProfit(ItemTrackingLocationSorting __instance)
    {
		__instance.PopulateProducterContent();
    }

    [HarmonyDebug]
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(ItemTrackingLocationSorting), "ProcessTrackingPortMerchant")]
    static IEnumerable<CodeInstruction> ProcessTrackingPortMerchant(IEnumerable<CodeInstruction> instructions, ILGenerator il)
    {
        // new labels
        Label nextIf = il.DefineLabel();
        Label notContainsNewItem = il.DefineLabel();
        // new vars
        LocalBuilder newVarInt = il.DeclareLocal(typeof(int));
        LocalBuilder newVarInt2 = il.DeclareLocal(typeof(int));
        LocalBuilder newVarInt3 = il.DeclareLocal(typeof(int));
        LocalBuilder newVarString = il.DeclareLocal(typeof(string));
        LocalBuilder newVarString2 = il.DeclareLocal(typeof(string));
        LocalBuilder newVarItemTrackingData = il.DeclareLocal(typeof(ItemTrackingData));

        int status = 0;

        foreach (CodeInstruction instruction in instructions)
        {
            // condition: FacilityData
            if((instruction.Is(OpCodes.Callvirt, AccessTools.Method(typeof(Facilities.PortFacilities), "GetFacilityDataByIndex")) && status == 0)
                || (instruction.opcode == OpCodes.Stloc_S && status == 1) // skip 1 line 
            )
            {
                status++;
                yield return instruction;
            }
            // loc facilityData found
            else if (instruction.opcode == OpCodes.Ldloc_S && status == 2)
            {
                status++;
                // is null ?
                yield return new CodeInstruction(OpCodes.Ldloc_S, 7);
                yield return new CodeInstruction(OpCodes.Brfalse, nextIf);

                // FacilityData.Arrivalconnectiontypes
                yield return new CodeInstruction(OpCodes.Ldloc_S, 7);
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(FacilityData), "get_Arrivalconnectiontypes"));
                // is contains(4) ? (4 = Factory)
                yield return new CodeInstruction(OpCodes.Ldc_I4_4);
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(List<>).MakeGenericType(typeof(Facilities.FacilityConnectionTypes)), "Contains", new Type[] {typeof(Facilities.FacilityConnectionTypes)}));
                yield return new CodeInstruction(OpCodes.Brfalse, nextIf);

                // FacilityData.Arrivalconnectiontypes
                yield return new CodeInstruction(OpCodes.Ldloc_S, 7);
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(FacilityData), "get_Arrivalconnectiontypes"));
                // indexof(4) (4 = Factory)
                yield return new CodeInstruction(OpCodes.Ldc_I4_4);
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(List<>).MakeGenericType(typeof(Facilities.FacilityConnectionTypes)), "IndexOf", new Type[] {typeof(Facilities.FacilityConnectionTypes)}));
                yield return new CodeInstruction(OpCodes.Stloc_S, newVarInt);

                // is num >= 0 ?
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarInt);
                yield return new CodeInstruction(OpCodes.Ldc_I4_0);
                yield return new CodeInstruction(OpCodes.Blt, nextIf);

                // facilityDataByIndex.Arrivalconnectionids[num]
                yield return new CodeInstruction(OpCodes.Ldloc_S, 7);
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(FacilityData), "get_Arrivalconnectionids"));
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarInt);
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(List<>).MakeGenericType(typeof(string)), "get_Item"));
                yield return new CodeInstruction(OpCodes.Stloc_S, newVarString);
                // factoryID
                yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(ProductionFacilityDataManager), "Inst"));
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarString);
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ProductionFacilityDataManager), "GetFactoryIDByID"));
                yield return new CodeInstruction(OpCodes.Stloc_S, newVarString2);

                // no CHEAT
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarString2);
                yield return new CodeInstruction(OpCodes.Ldstr, "CHEAT");
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(string), "Contains", new Type[] {typeof(string)}));
                yield return new CodeInstruction(OpCodes.Brtrue, nextIf);

                // new ItemTrackingData
                yield return new CodeInstruction(OpCodes.Newobj, AccessTools.Constructor(typeof(ItemTrackingData)));
                yield return new CodeInstruction(OpCodes.Stloc_S, newVarItemTrackingData);

                // port name
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(LocalizationManager), "Inst"));
                yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(PortsTravelDataManager), "Inst"));
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(AbstractLocationPoint), "get_BaseId"));
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(PortsTravelDataManager), "GetShortNameByID"));
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(LocalizationManager), "GetLocalizedValue"));
                yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(ItemTrackingData), "PortName"));
                
                // port id
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(AbstractLocationPoint), "get_BaseId"));
                yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(ItemTrackingData), "PortID"));

                // facility index
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarInt);
                yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(ItemTrackingData), "FacilityIndex"));

                // MerchantID
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarString);
                yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(ItemTrackingData), "MerchantID"));

                // MerchantTags
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Newobj, AccessTools.Constructor(typeof(List<>).MakeGenericType(typeof(MerchantTag))));
                yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(ItemTrackingData), "MerchantTags"));

                // StoreID
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarString2);
                yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(ItemTrackingData), "StoreID"));

                // WharehouseSpace
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Ldloc_0);
                yield return new CodeInstruction(OpCodes.Ldloca_S, newVarInt2);
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ItemTrackingLocationSorting), "GetWarehouseOccupiedSlot"));
                yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(ItemTrackingData), "WarehouseSpace"));

                // WarehouseMax
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarInt2);
                yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(ItemTrackingData), "WarehouseMax"));

                // HangarSpace
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Ldloc_0);
                yield return new CodeInstruction(OpCodes.Ldloca_S, newVarInt3);
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ItemTrackingLocationSorting), "GetHangarSpace"));
                yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(ItemTrackingData), "HangarSpace"));

                // HangarMax
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarInt3);
                yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(ItemTrackingData), "HangarMax"));

                // Distance
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(AbstractLocationPoint), "get_PlayerDistance"));
                yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(ItemTrackingData), "Distance"));

                // PortIconSprite
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(IconSpriteManager), "Inst"));
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(IconSpriteManager), "GetMapIcon"));
                yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(ItemTrackingData), "PortIconSprite"));

                // Arrivalitemconditions
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Ldloc_S, 7);
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(FacilityData), "get_Arrivalitemconditions"));
                yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(ItemTrackingData), "Arrivalitemconditions"));

                // Departureitemconditions
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Ldloc_S, 7);
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(FacilityData), "get_Departureitemconditions"));
                yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(ItemTrackingData), "Departureitemconditions"));

                // TrackingMerchantOffer::StoreOffer
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(ItemTrackingData), "MerchantOffer"));
                yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(ProductionFacilityDataManager), "Inst"));
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ItemTrackingData), "StoreID"));
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ProductionFacilityDataManager), "GetRecipeTableByID"));
                yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(TrackingMerchantOffer), "StoreOffer"));

                // contains ItemTrackingLocationSorting
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ItemTrackingLocationSorting), "trackingDataList"));
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(List<>).MakeGenericType(typeof(ItemTrackingData)), "Contains"));
                yield return new CodeInstruction(OpCodes.Brfalse_S, notContainsNewItem);

                // already in ItemTrackingLocationSorting
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ItemTrackingLocationSorting), "trackingDataList"));
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ItemTrackingLocationSorting), "trackingDataList"));
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(List<>).MakeGenericType(typeof(ItemTrackingData)), "IndexOf", new Type[] {typeof(ItemTrackingData)}));
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(List<>).MakeGenericType(typeof(ItemTrackingData)), "set_Item"));
                yield return new CodeInstruction(OpCodes.Br_S, nextIf);

                // new in ItemTrackingLocationSorting
                CodeInstruction addingItem = new(OpCodes.Ldarg_0);
                addingItem.labels.Add(notContainsNewItem);
                yield return addingItem;
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ItemTrackingLocationSorting), "trackingDataList"));
                yield return new CodeInstruction(OpCodes.Ldloc_S, newVarItemTrackingData);
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(List<>).MakeGenericType(typeof(ItemTrackingData)), "Add"));

                // reach next if
                instruction.labels.Add(nextIf);
                yield return instruction;
            }
            else 
            {
                yield return instruction;
            }
        }
    }
}

public static class ItemTrackingLocationSortingUtils
{
    public static void PopulateProducterContent(this ItemTrackingLocationSorting itemTrackingLocationSorting)
	{
		PagingRow component = GameObject.Find("ProducterContentGroup").transform.GetChild(1).GetComponent<PagingRow>();
		List<ItemTrackingLocationTab> prodLocations = new(GameObject.Find("ProducterContentGroup").transform.GetComponentsInChildren<ItemTrackingLocationTab>(true));
		int num = 0;
		num += component.CurrentPage * prodLocations.Count;

        // some reflexion to retrieve some elements
        Dictionary<string, ItemTrackingData> currentProductingDict = new();
        List<KeyValuePair<string, ItemTrackingData>> currentProducterList;
        ItemData showingItem = AccessTools.Field(typeof(ItemTrackingLocationSorting), "showingItem").GetValue(itemTrackingLocationSorting) as ItemData;
        List<ItemTrackingData> trackingDataList = AccessTools.Field(typeof(ItemTrackingLocationSorting), "trackingDataList").GetValue(itemTrackingLocationSorting) as List<ItemTrackingData>;

        // compute the list and dict, no new field with Harmony
        foreach (ItemTrackingData itemTrackingData in trackingDataList)
		{
            if (itemTrackingData.IsStoreOpened &&
                !itemTrackingData.MerchantTags.Any() && 
                (itemTrackingData.MerchantOffer.StoreOffer.Contains("RC_" + showingItem.Id + "_0") || 
                itemTrackingData.MerchantOffer.StoreOffer.Contains("RC_" + showingItem.Id + "_1") || 
                itemTrackingData.MerchantOffer.StoreOffer.Contains("RC_" + showingItem.Id + "_2") || 
                itemTrackingData.MerchantOffer.StoreOffer.Contains("RC_" + showingItem.Id + "_3") || 
                itemTrackingData.MerchantOffer.StoreOffer.Contains("RC_" + showingItem.Id + "_4") || 
                itemTrackingData.MerchantOffer.StoreOffer.Contains("RC_" + showingItem.Id + "_5")))
            {
                if (!currentProductingDict.ContainsKey(itemTrackingData.PortName))
                {
                    currentProductingDict.Add(itemTrackingData.PortName, itemTrackingData);
                }
                else if (currentProductingDict[itemTrackingData.PortName].MerchantOffer.MerchantSellingItemOffset > itemTrackingData.MerchantOffer.MerchantSellingItemOffset)
                {
                    currentProductingDict[itemTrackingData.PortName] = itemTrackingData;
                }
            }
        }
        currentProducterList = currentProductingDict.ToList();
        currentProducterList.Sort((KeyValuePair<string, ItemTrackingData> p1, KeyValuePair<string, ItemTrackingData> p2) => p1.Value.Distance.CompareTo(p2.Value.Distance));

		for (int i = 0; i < prodLocations.Count; i++)
		{
			prodLocations[i].OnClosePopup();
		}
		for (int j = 0; j < prodLocations.Count; j++)
		{
			if (num > currentProducterList.Count - 1 || currentProducterList[num].Value == null)
			{
				prodLocations[j].OnClosePopup();
			}
			else
			{
				Color borderColor = Color.green;
				string percent = "-";
				string warehouseSpaceTextFormat = currentProducterList[num].Value.GetWarehouseSpaceTextFormat();
				string hangarSpaceTextFormat = currentProducterList[num].Value.GetHangarSpaceTextFormat();
				string distanceTextFormat = currentProducterList[num].Value.GetDistanceTextFormat();
				if (currentProducterList[num].Value.PurchaseInterest.MerchantBuyingPriceOffset < 0f)
				{
					borderColor = Color.red;
					if (currentProducterList[num].Value.PurchaseInterest.MerchantBuyingPriceOffset % 1f == 0f)
					{
						percent = currentProducterList[num].Value.PurchaseInterest.MerchantBuyingPriceOffset.ToString() + "%";
					}
					else
					{
						percent = currentProducterList[num].Value.PurchaseInterest.MerchantBuyingPriceOffset.ToString("0.#") + "%";
					}
				}
				if (currentProducterList[num].Value.PurchaseInterest.MerchantBuyingPriceOffset > 0f)
				{
					if (currentProducterList[num].Value.PurchaseInterest.MerchantBuyingPriceOffset > 0.1f)
					{
						if (currentProducterList[num].Value.PurchaseInterest.MerchantBuyingPriceOffset % 1f == 0f)
						{
							percent = "+" + currentProducterList[num].Value.PurchaseInterest.MerchantBuyingPriceOffset.ToString() + "%";
						}
						else
						{
							percent = "+" + currentProducterList[num].Value.PurchaseInterest.MerchantBuyingPriceOffset.ToString("0.#") + "%";
						}
					}
					else
					{
						currentProducterList[num].Value.PurchaseInterest.MerchantBuyingPriceOffset = 0f;
						borderColor = Color.yellow;
					}
				}
				if (currentProducterList[num].Value.PurchaseInterest.MerchantBuyingPriceOffset == 0f)
				{
					borderColor = Color.yellow;
				}
				prodLocations[j].SetupLocationDisplay(
                    currentProducterList[num].Value.PortID, 
                    currentProducterList[num].Value.PortName, 
                    currentProducterList[num].Value.PortIconSprite, 
                    warehouseSpaceTextFormat, 
                    hangarSpaceTextFormat, 
                    distanceTextFormat, 
                    currentProducterList[num].Value.GetDistanceUnit(), 
                    percent, 
                    borderColor);
				num++;
			}
		}
    }
}
