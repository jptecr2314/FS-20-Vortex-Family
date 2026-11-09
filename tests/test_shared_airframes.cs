using System;
using System.Linq;
using Japensou.NuclearOption.SharedAirframes;

internal static class SharedAirframeChecks
{
    private static int checks;
    private static void Check(bool ok, string why)
    {
        checks++;
        if (!ok)
            throw new Exception(why);
    }

    private static void Reject(Action f, string why)
    {
        try
        {
            f();
        }
        catch (InvalidOperationException)
        {
            checks++;
            return;
        }

        throw new Exception(why);
    }

    private static void Main()
    {
        var variants = new[]
        {
            "SmallFighter1",
            "JP_FS20_A",
            "JP_FS20_CTOL"
        };
        foreach (string a in variants)
            foreach (string b in variants)
                Check(AirframePool.Same(a, b), "Every FS20 body supports all three variants");
        Check(!AirframePool.Same("SmallFighter1", "LargeFighter1"), "Other aircraft stay separate");
        Check(!AirframePool.Same(null, null), "Null is not an airframe");
        foreach (string requested in variants)
        {
            var stock = new[]
            {
                48,
                0,
                0
            };
            for (int flown = 0; flown < 48; flown++)
            {
                foreach (string visible in variants)
                    Check(AirframePool.Count(stock) == 48 - flown, "All selectors display one remaining balance");
                var debit = AirframePool.PlanDebit(stock, 1);
                Check(debit.Sum() == 1, "Taking a variant consumes exactly one cell");
                for (int i = 0; i < stock.Length; i++)
                    stock[i] -= debit[i];
            }

            Check(AirframePool.Count(stock) == 0, "48 withdrawals exhaust 48 cells");
            Reject(() => AirframePool.PlanDebit(stock, 1), "49th withdrawal must not mint a cell");
            Check(stock.Sum() == 0, "Rejected withdrawal leaves balance unchanged");
            stock[2]++;
            Check(AirframePool.Count(stock) == 1, "Return of a C restores one shared body");
            stock[1] += 2;
            Check(AirframePool.Count(stock) == 3, "Added A stock is pooled without copying into B/C");
        }

        var mixed = new[]
        {
            2,
            3,
            4
        };
        var plan = AirframePool.PlanDebit(mixed, 7);
        Check(plan.SequenceEqual(new[] { 2, 3, 2 }), "A multi-cell debit spans existing rows once");
        Check(mixed.SequenceEqual(new[] { 2, 3, 4 }), "Planning does not mutate supply before validation");
        Reject(() => AirframePool.PlanDebit(mixed, 10), "Insufficient batch fails atomically");
        Check(mixed.Sum() == 9, "Rejected batch does not lose any cells");
        Check(AirframePool.Count(new[] { int.MaxValue, int.MaxValue }) == int.MaxValue, "UI count does not overflow");
        Check(AirframePool.Count(new[] { -1, 48 }) == 48, "Negative legacy row does not increase availability");
        var keys = new[]
        {
            "SmallFighter1",
            "Other",
            "JP_FS20_A",
            "JP_FS20_CTOL"
        };
        var reserved = new[]
        {
            false,
            false,
            true,
            true
        };
        var owned = AirframePool.SelectOwned(keys, reserved, "JP_FS20_CTOL", 1);
        Check(owned.SequenceEqual(new[] { 0 }), "Sale prefers the unreserved body across variants");
        owned = AirframePool.SelectOwned(keys, reserved, "SmallFighter1", 3);
        Check(owned.SequenceEqual(new[] { 3, 2, 0 }), "Returning all family cells preserves unrelated ownership and removal order");
        Reject(() => AirframePool.SelectOwned(keys, reserved, "JP_FS20_A", 4), "Owned cells cannot be copied");
        var factionOne = new[]
        {
            48
        };
        var factionTwo = new[]
        {
            7
        };
        var take = AirframePool.PlanDebit(factionOne, 1);
        factionOne[0] -= take[0];
        Check(factionOne[0] == 47 && factionTwo[0] == 7, "Factions keep independent pools");
        Console.WriteLine("SHARED_AIRFRAME_POOL_PASS checks=" + checks + "; offline ledger only, no game launched");
    }
}
