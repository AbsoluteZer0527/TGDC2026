using UnityEngine;

// Kitchen difficulty from the day number: level 1 on day 1, max level 10 from day 10 on.
// Everything here is a multiplier or count; the Inspector values are the max-difficulty (day 10) values.
public static class Difficulty
{
    public const int MaxLevel = 10;
    public static int Level => Mathf.Clamp(GameManager.I ? GameManager.I.Day : 1, 1, MaxLevel);

    // Different ingredients per order: 2 on day 1, +1 every other day (day 3: 3, day 5: 4, day 7: 5), 6 from day 9.
    public static int MaxOrderSize => Mathf.Min(6, 2 + (Level - 1) / 2);

    // Veggie runners. Even days: faster with shorter pauses. Odd days: higher, more frequent hops.
    static int EvenSteps => Level / 2;          // day 2 -> 1 ... day 10 -> 5
    static int OddSteps => (Level - 1) / 2;     // day 3 -> 1 ... day 9 -> 4
    public static float RunSpeed => Mathf.Min(1f, 0.2f + 0.2f * EvenSteps);  // x0.2 -> x1 (day 8)
    public static float MaxPause => 2f - 0.2f * EvenSteps;                   // x2   -> x1 (day 10)
    public static float Hop => Mathf.Min(1f, 0.2f + 0.2f * OddSteps);        // x0.2 -> x1 (day 9), hop height and hops/sec

    // Fraction of the day-one serving window, reaching one third at maximum difficulty.
    public static float ServingWindow => Mathf.Lerp(1f, 1f / 3f, (Level - 1) / (float)(MaxLevel - 1));
}
