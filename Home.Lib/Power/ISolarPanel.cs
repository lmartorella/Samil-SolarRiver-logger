namespace Lucky.Home.Power
{
    interface ISolarPanel
    {
        double ImmediatePower { get; }
        Counter CurrentDayData { get; }
        Counter LastDayData { get; }
        Counter LastWeekData { get; }
        Counter LastMonthData { get; }
    }
}
