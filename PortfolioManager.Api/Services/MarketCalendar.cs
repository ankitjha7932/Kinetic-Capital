using System.Runtime.InteropServices;

namespace PortfolioManager.Api.Services;

/// <summary>
/// NSE market calendar — knows trading hours, weekends, and official NSE holidays.
/// Single source of truth for "is the market open right now?" across the whole app.
/// </summary>
public static class MarketCalendar
{
    // ── Trading session window (IST) ──────────────────────────────────────────
    public static readonly TimeSpan MarketOpen = new(9, 15, 0);
    public static readonly TimeSpan MarketClose = new(15, 30, 0);

    // ── NSE Holidays 2026 ─────────────────────────────────────────────────────
    // Source: NSE circular — equity segment holidays
    private static readonly HashSet<DateOnly> NseHolidays2026 = new()
    {
        new DateOnly(2026, 1, 15), // Municipal Corporation Election - Maharashtra
        new DateOnly(2026, 1, 26), // Republic Day
        new DateOnly(2026, 3, 3), // Holi
        new DateOnly(2026, 3, 26), // Shri Ram Navami
        new DateOnly(2026, 3, 31), // Shri Mahavir Jayanti
        new DateOnly(2026, 4, 3), // Good Friday
        new DateOnly(2026, 4, 14), // Dr. Baba Saheb Ambedkar Jayanti
        new DateOnly(2026, 5, 1), // Maharashtra Day
        new DateOnly(2026, 5, 28), // Bakri Id
        new DateOnly(2026, 6, 26), // Muharram
        new DateOnly(2026, 9, 14), // Ganesh Chaturthi
        new DateOnly(2026, 10, 2), // Mahatma Gandhi Jayanti
        new DateOnly(2026, 10, 20), // Dussehra
        new DateOnly(2026, 11, 10), // Diwali - Balipratipada
        new DateOnly(2026, 11, 24), // Prakash Gurpurb Sri Guru Nanak Dev
        new DateOnly(2026, 12, 25), // Christmas
    };

    // ── NSE Holidays 2025 (keep for backward-compat / year boundary) ──────────
    private static readonly HashSet<DateOnly> NseHolidays2025 = new()
    {
        new DateOnly(2025, 1, 26), // Republic Day
        new DateOnly(2025, 2, 26), // Mahashivratri
        new DateOnly(2025, 3, 14), // Holi
        new DateOnly(2025, 3, 31), // Id-Ul-Fitr (Ramzan Id)
        new DateOnly(2025, 4, 10), // Shri Mahavir Jayanti
        new DateOnly(2025, 4, 14), // Dr. Baba Saheb Ambedkar Jayanti
        new DateOnly(2025, 4, 18), // Good Friday
        new DateOnly(2025, 5, 1), // Maharashtra Day
        new DateOnly(2025, 8, 15), // Independence Day
        new DateOnly(2025, 10, 2), // Mahatma Gandhi Jayanti / Dussehra
        new DateOnly(2025, 10, 21), // Diwali Laxmi Puja (Muhurat Trading)
        new DateOnly(2025, 10, 22), // Diwali - Balipratipada
        new DateOnly(2025, 11, 5), // Prakash Gurpurb Sri Guru Nanak Dev
        new DateOnly(2025, 12, 25), // Christmas
    };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns the current IST DateTime.</summary>
    public static DateTime NowIST()
    {
        var tzId = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "India Standard Time"
            : "Asia/Kolkata";
        return TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById(tzId)
        );
    }

    /// <summary>Is the NSE equity market currently open (right now)?</summary>
    public static bool IsMarketOpenNow() => IsMarketOpen(NowIST());

    /// <summary>Is the NSE equity market open at a given IST DateTime?</summary>
    public static bool IsMarketOpen(DateTime istDateTime)
    {
        if (IsWeekend(istDateTime))
            return false;
        if (IsNseHoliday(istDateTime))
            return false;
        return istDateTime.TimeOfDay >= MarketOpen && istDateTime.TimeOfDay <= MarketClose;
    }

    /// <summary>
    /// Returns a <see cref="MarketStatus"/> describing the current market state.
    /// </summary>
    public static MarketStatus GetCurrentStatus()
    {
        var now = NowIST();
        return GetStatusAt(now);
    }

    /// <summary>
    /// Returns the market status at a given IST DateTime.
    /// Includes whether data is live or from previous close,
    /// and the date of the last actual trading session.
    /// </summary>
    public static MarketStatus GetStatusAt(DateTime istDateTime)
    {
        bool isWeekend = IsWeekend(istDateTime);
        bool isHoliday = IsNseHoliday(istDateTime);
        bool isTradingDay = !isWeekend && !isHoliday;

        bool isOpen =
            isTradingDay
            && istDateTime.TimeOfDay >= MarketOpen
            && istDateTime.TimeOfDay <= MarketClose;

        bool isPreMarket = isTradingDay && istDateTime.TimeOfDay < MarketOpen;
        bool isPostMarket = isTradingDay && istDateTime.TimeOfDay > MarketClose;

        var sessionType =
            isOpen ? "OPEN"
            : isPreMarket ? "PRE_MARKET"
            : isPostMarket ? "POST_MARKET"
            : isHoliday ? "HOLIDAY"
            : "WEEKEND";

        // The previous trading session's close date
        var prevTradingDate = GetPreviousTradingDay(istDateTime, isOpen || isPostMarket);

        // Human-readable reason for closure
        string? closedReason = null;
        if (!isOpen)
        {
            if (isHoliday)
                closedReason = GetHolidayName(istDateTime);
            else if (isWeekend)
                closedReason = istDateTime.DayOfWeek.ToString();
            else if (isPreMarket)
                closedReason = "Pre-market";
            else if (isPostMarket)
                closedReason = "Post-market";
        }

        return new MarketStatus(
            IsOpen: isOpen,
            SessionType: sessionType,
            IsLiveData: isOpen,
            PreviousSessionDate: prevTradingDate,
            CurrentIST: istDateTime,
            ClosedReason: closedReason
        );
    }

    /// <summary>
    /// Returns the most recent completed trading day.
    /// If the market is currently open, returns today (data is live).
    /// If today is a holiday/weekend/pre-market, returns the previous trading day.
    /// </summary>
    public static DateOnly GetPreviousTradingDay(
        DateTime istDateTime,
        bool includeTodayIfTrading = false
    )
    {
        var date = DateOnly.FromDateTime(istDateTime);

        // If we're in an open session and caller wants to include today → return today
        if (includeTodayIfTrading && !IsWeekend(istDateTime) && !IsNseHoliday(istDateTime))
            return date;

        // Walk backward until we find a trading day
        var check = date.AddDays(-1);
        int safetyCounter = 0;
        while (safetyCounter++ < 30)
        {
            var dt = check.ToDateTime(TimeOnly.MinValue);
            if (!IsWeekend(dt) && !IsNseHoliday(dt))
                return check;
            check = check.AddDays(-1);
        }
        return date; // fallback (should never reach here)
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public static bool IsWeekend(DateTime dt) =>
        dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday;

    public static bool IsNseHoliday(DateTime dt)
    {
        var d = DateOnly.FromDateTime(dt);
        return NseHolidays2026.Contains(d) || NseHolidays2025.Contains(d);
    }

    public static bool IsNseHoliday(DateOnly d) =>
        NseHolidays2026.Contains(d) || NseHolidays2025.Contains(d);

    /// <summary>Returns the holiday name for a given date, or null if not a holiday.</summary>
    public static string? GetHolidayName(DateTime dt)
    {
        var d = DateOnly.FromDateTime(dt);
        return HolidayNames.TryGetValue(d, out var name) ? name : null;
    }

    // ── Holiday name lookup ───────────────────────────────────────────────────
    private static readonly Dictionary<DateOnly, string> HolidayNames = new()
    {
        // 2026
        [new DateOnly(2026, 1, 15)] = "Municipal Corporation Election - Maharashtra",
        [new DateOnly(2026, 1, 26)] = "Republic Day",
        [new DateOnly(2026, 3, 3)] = "Holi",
        [new DateOnly(2026, 3, 26)] = "Shri Ram Navami",
        [new DateOnly(2026, 3, 31)] = "Shri Mahavir Jayanti",
        [new DateOnly(2026, 4, 3)] = "Good Friday",
        [new DateOnly(2026, 4, 14)] = "Dr. Baba Saheb Ambedkar Jayanti",
        [new DateOnly(2026, 5, 1)] = "Maharashtra Day",
        [new DateOnly(2026, 5, 28)] = "Bakri Id",
        [new DateOnly(2026, 6, 26)] = "Muharram",
        [new DateOnly(2026, 9, 14)] = "Ganesh Chaturthi",
        [new DateOnly(2026, 10, 2)] = "Mahatma Gandhi Jayanti",
        [new DateOnly(2026, 10, 20)] = "Dussehra",
        [new DateOnly(2026, 11, 10)] = "Diwali - Balipratipada",
        [new DateOnly(2026, 11, 24)] = "Prakash Gurpurb Sri Guru Nanak Dev",
        [new DateOnly(2026, 12, 25)] = "Christmas",
        // 2025
        [new DateOnly(2025, 1, 26)] = "Republic Day",
        [new DateOnly(2025, 2, 26)] = "Mahashivratri",
        [new DateOnly(2025, 3, 14)] = "Holi",
        [new DateOnly(2025, 3, 31)] = "Id-Ul-Fitr (Ramzan Id)",
        [new DateOnly(2025, 4, 10)] = "Shri Mahavir Jayanti",
        [new DateOnly(2025, 4, 14)] = "Dr. Baba Saheb Ambedkar Jayanti",
        [new DateOnly(2025, 4, 18)] = "Good Friday",
        [new DateOnly(2025, 5, 1)] = "Maharashtra Day",
        [new DateOnly(2025, 8, 15)] = "Independence Day",
        [new DateOnly(2025, 10, 2)] = "Mahatma Gandhi Jayanti / Dussehra",
        [new DateOnly(2025, 10, 21)] = "Diwali Laxmi Puja",
        [new DateOnly(2025, 10, 22)] = "Diwali - Balipratipada",
        [new DateOnly(2025, 11, 5)] = "Prakash Gurpurb Sri Guru Nanak Dev",
        [new DateOnly(2025, 12, 25)] = "Christmas",
    };
}

/// <summary>
/// Snapshot of the current NSE market status.
/// Serialises cleanly to JSON for API responses.
/// </summary>
public record MarketStatus(
    bool IsOpen,
    string SessionType, // OPEN | PRE_MARKET | POST_MARKET | HOLIDAY | WEEKEND
    bool IsLiveData, // true only when market is currently open
    DateOnly PreviousSessionDate,
    DateTime CurrentIST,
    string? ClosedReason // holiday name / "Saturday" / "Pre-market" / etc.
);
