using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MSSV_Gacha_Random
{
    public class WeightedOutput
    {
        public ThingDef thingDef;
        public float weight = 1f;
        public int count = 1;
        public bool isNothing = false;
        public int tier = 0; // 0 = "normal loot", 1 = ggood, 2 = great
    }
    public class RandomOutputExtension : DefModExtension
    {
        public List<WeightedOutput> outputs = new List<WeightedOutput>();
        public int nothingDescCount = 1;
        public int genericDescCount = 1;
        public int goodDescCount = 1;
        public int greatDescCount = 1;
    }
    public class Building_GachaMachine : Building_WorkTableAutonomous
    {
        private int pendingIndex = -1;
        private bool readyToDispense = false;
        private List<WeightedOutput> LootTable =>
            this.def.GetModExtension<RandomOutputExtension>()?.outputs;
        public override void Notify_FormingCompleted()
        {
            var table = LootTable;
            if (table.NullOrEmpty())
            {
                base.Notify_FormingCompleted();
                return;
            }
            base.Notify_FormingCompleted();
           pendingIndex = PickWeightedIndex(table);
            readyToDispense = true;
        }
        protected override void Tick()
        {
            base.Tick();

            if (readyToDispense && activeBill == null)
            {
                SpawnProduct();
                ResetState();
            }
        }
        private void SpawnProduct()
        {
            var table = LootTable;
            if (table.NullOrEmpty() || pendingIndex < 0 || pendingIndex >= table.Count)
                return;
            var output = table[pendingIndex];
            SendLetter(output);
            if (output.isNothing || output.thingDef == null)
                return;
            Thing product = ThingMaker.MakeThing(
                output.thingDef,
                output.thingDef.MadeFromStuff
                    ? GenStuff.DefaultStuffFor(output.thingDef)
                    : null
            );
            product.stackCount = output.count;
            CompQuality compQuality = product.TryGetComp<CompQuality>();
            if (compQuality != null)
                compQuality.SetQuality(
                    QualityUtility.GenerateQualityBaseGen(),
                    ArtGenerationContext.Colony
                );
            GenPlace.TryPlaceThing(
                product,
                this.InteractionCell,
                this.Map,
                ThingPlaceMode.Near
            );
        }
        private void SendLetter(WeightedOutput output)
        {
            var ext = this.def.GetModExtension<RandomOutputExtension>();
            string desc;
            if (output.isNothing)
            {
                int roll = Rand.RangeInclusive(1, ext?.nothingDescCount ?? 1);
                desc = ("Gacha_NothingDesc_" + roll).Translate();
            }
            else if (output.tier >= 2)
            {
                int roll = Rand.RangeInclusive(1, ext?.greatDescCount ?? 1);
                desc = ("Gacha_GreatDesc_" + roll).Translate(output.thingDef.LabelCap);
            }
            else if (output.tier == 1)
            {
                int roll = Rand.RangeInclusive(1, ext?.goodDescCount ?? 1);
                desc = ("Gacha_GoodDesc_" + roll).Translate(output.thingDef.LabelCap);
            }
            else
            {
                int roll = Rand.RangeInclusive(1, ext?.genericDescCount ?? 1);
                desc = ("Gacha_GenericDesc_" + roll).Translate(output.thingDef.LabelCap);
            }

            Find.LetterStack.ReceiveLetter(
                "Gacha cycle complete",
                desc,
                LetterDefOf.PositiveEvent,
                new LookTargets(this)
            );
        }
        private void ResetState()
        {
            pendingIndex = -1;
            readyToDispense = false;
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pendingIndex, "pendingIndex", -1);
            Scribe_Values.Look(ref readyToDispense, "readyToDispense", false);
        }
        private int PickWeightedIndex(List<WeightedOutput> outputs)
        {
            float total = 0f;
            foreach (var o in outputs)
                if (o.weight > 0f) total += o.weight;
            if (total <= 0f) return -1;
            float roll = Rand.Value * total;
            float cumulative = 0f;
            for (int i = 0; i < outputs.Count; i++)
            {
                if (outputs[i].weight <= 0f) continue;
                cumulative += outputs[i].weight;
                if (roll <= cumulative) return i;
            }
            return outputs.Count - 1;
        }
    }
}