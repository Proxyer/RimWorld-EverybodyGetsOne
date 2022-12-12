﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using HarmonyLib;
using UnityEngine;
using TD_Find_Lib;

namespace Everybody_Gets_One
{
	public class PersonCountMapComp : MapComponent
	{
		//cache these counts each update
		public Dictionary<Bill_Production, QuerySearch> billPersonCounters = new();

		public PersonCountMapComp(Map map) : base(map) { }

		private List<Bill_Production> scribeBills;
		private List<QuerySearch> scribeSearches;
		public override void ExposeData()
		{
			Scribe_Collections.Look(ref billPersonCounters, "billPersonCounters", LookMode.Reference, LookMode.Deep, ref scribeBills, ref scribeSearches);
			if (billPersonCounters == null)
				billPersonCounters = new();
		}

		public bool HasPersonCounter(Bill_Production bill)
		{
			return billPersonCounters.ContainsKey(bill);
		}

		public void SetPersonCounter(Bill_Production bill, QuerySearch search)
		{
			billPersonCounters[bill] = search;

		}

		public QuerySearch GetPersonCounter(Bill_Production bill)
		{
			QuerySearch personCounter;
			if (!billPersonCounters.TryGetValue(bill, out personCounter))
			{
				personCounter = MakePersonCounter(bill);
				billPersonCounters[bill] = personCounter;
			}
			return personCounter;
		}

		public void RemovePersonCounter(Bill_Production bill)
		{
			billPersonCounters.Remove(bill);
		}


		public int CountFor(Bill_Production bill)
		{
			QuerySearch personCounter = GetPersonCounter(bill);

			personCounter.RemakeList();

			return personCounter.result.allThings.Sum(t => t.stackCount);
		}

		public QuerySearch MakePersonCounter(Bill_Production bill)
		{
			QuerySearch search = new(map);
			search.SetListType(SearchListType.Everyone, false);

			ThingQueryBasicProperty queryColonist = ThingQueryMaker.MakeQuery<ThingQueryBasicProperty>();
			queryColonist.sel = QueryPawnProperty.IsColonist;
			search.Children.Add(queryColonist, remake: false);

			ThingQueryBasicProperty querySlave = ThingQueryMaker.MakeQuery<ThingQueryBasicProperty>();
			querySlave.sel = QueryPawnProperty.IsSlaveOfColony;
			querySlave.include = false;
			search.Children.Add(querySlave, remake: false);

			ThingQueryQuest queryQuestLodger = ThingQueryMaker.MakeQuery<ThingQueryQuest>();
			//Default is Quest Lodger
			queryQuestLodger.include = false;
			search.Children.Add(queryQuestLodger, remake: false);

			search.name = "TD.PeopleForBill".Translate() + bill.LabelCap;

			// Maybe don't immediately open it.
			// Find.WindowStack.Add(new PersonCounterEditor(search));

			return search;
		}

		public void OpenPersonCounter(Bill_Production bill)
		{
			Find.WindowStack.Add(new PersonCounterEditor(GetPersonCounter(bill)));
		}
	}

	public class PersonCounterEditor : SearchEditorRevertableWindow, ISearchReceiver
	{
		public PersonCounterEditor(QuerySearch search) : base(search, TransferTag)
		{
			//Same as the Dialog_BillConfig
			forcePause = true;
			doCloseX = true;
			absorbInputAroundWindow = true;
			closeOnClickedOutside = true;

			//Overrides from SearchEditorWindow
			onlyOneOfTypeAllowed = true;
			//preventCameraMotion = false;
			draggable = false;
			resizeable = false;
			//above //doCloseX = true;
		}

		public override Vector2 InitialSize => new Vector2(750, 320);

		public override void SetInitialSizeAndPosition()
		{
			base.SetInitialSizeAndPosition();
			windowRect.x = (UI.screenWidth - windowRect.width) / 2;
			windowRect.y = 0;
		}


		// ISearchReceiver stuff
		public static string TransferTag = "TD.EGO";
		public string Source => TransferTag;
		public string ReceiveName => "TD.UseAsPersonCounter".Translate();
		public QuerySearch.CloneArgs CloneArgs => QuerySearch.CloneArgs.use;
		
		public bool CanReceive() => true;
		public void Receive(QuerySearch search)
		{
			Import(search);
		}

		public override void PostOpen()
		{
			base.PostOpen();

			SearchTransfer.Register(this);
		}

		public override void PreClose()
		{
			base.PreClose();

			SearchTransfer.Deregister(this);
		}
	}

	public static class MapCompExtensions
	{
		public static int CurrentPersonCount(this Map map, Bill_Production bill) =>
			map.GetComponent<PersonCountMapComp>().CountFor(bill);

		public static void OpenPersonCounter(this Map map, Bill_Production bill) =>
			map.GetComponent<PersonCountMapComp>().OpenPersonCounter(bill);

		public static bool HasPersonCounter(this Map map, Bill_Production bill) =>
			map.GetComponent<PersonCountMapComp>().HasPersonCounter(bill);

		public static QuerySearch GetPersonCounter(this Map map, Bill_Production bill) =>
			map.GetComponent<PersonCountMapComp>().GetPersonCounter(bill);

		public static QuerySearch GetPersonCounter(this Bill_Production bill) =>
			bill.Map.GetComponent<PersonCountMapComp>().GetPersonCounter(bill);

		public static void RemovePersonCounter(this Map map, Bill_Production bill) =>
			map.GetComponent<PersonCountMapComp>().RemovePersonCounter(bill);

		public static void SetPersonCounter(this Map map, Bill_Production bill, QuerySearch search) =>
			map.GetComponent<PersonCountMapComp>().SetPersonCounter(bill, search);
	}


	[HarmonyPatch(typeof(Building_WorkTable), nameof(Building_WorkTable.Notify_BillDeleted))]
	public static class DeleteBillCounter
	{
		public static void Postfix(Building_WorkTable __instance, Bill bill)
		{
			if(bill is Bill_Production billP)
			{
				__instance.MapHeld.RemovePersonCounter(billP);
			}
		}
	}

	[HarmonyPatch(typeof(Building), nameof(Building.Destroy))]
	public class Building_Destroy_Detour
	{
		public static void Prefix(Building __instance)
		{
			//Because Building_WorkTable doesn't override Destroy
			if (__instance is Building_WorkTable workTable)
				foreach (var bill in workTable.BillStack.Bills)
					if (bill is Bill_Production billP)
						__instance.MapHeld.RemovePersonCounter(billP);
		}
	}
}
