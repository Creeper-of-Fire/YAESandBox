using System.Globalization;

// ReSharper disable InconsistentNaming

namespace YAESandBox.Workflow.Module.ExactModule;

internal partial class LuaScriptModuleProcessor
{
    /// <summary>
    /// 日期时间模块的工厂类，暴露给 Lua 作为 'datetime'。
    /// </summary>
    private class LuaDateTimeBridge(LuaLogBridge logger)
    {
        private LuaLogBridge Logger { get; } = logger;

        public LuaDateTimeObject utcnow() => new(DateTimeOffset.UtcNow, Logger);
        public LuaDateTimeObject now() => new(DateTimeOffset.Now, Logger);
        public LuaDateTimeObject? parse(string dateString, string? format = null)
        {
            try
            {
                if (string.IsNullOrEmpty(format))
                {
                    return DateTimeOffset.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) 
                        ? new LuaDateTimeObject(result, Logger) 
                        : null;
                }
                return DateTimeOffset.TryParseExact(dateString, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var resultExact) 
                    ? new LuaDateTimeObject(resultExact, Logger) 
                    : null;
            }
            catch (Exception ex)
            {
                Logger.error($"datetime.parse 失败: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// 一个安全的 DateTimeOffset 包装器，暴露给 Lua。
    /// 这个对象是不可变的；所有 'add' 方法都返回一个新对象。
    /// </summary>
    private class LuaDateTimeObject(DateTimeOffset dateTimeOffset, LuaLogBridge logger)
    {
        private DateTimeOffset Dto { get; } = dateTimeOffset;
        private LuaLogBridge Logger { get; } = logger;

        // --- 属性 ---
        public int year => this.Dto.Year;
        public int month => this.Dto.Month;
        public int day => this.Dto.Day;
        public int hour => this.Dto.Hour;
        public int minute => this.Dto.Minute;
        public int second => this.Dto.Second;
        public int millisecond => this.Dto.Millisecond;
        public int day_of_week => (int)this.Dto.DayOfWeek; // 0=Sunday, ..., 6=Saturday
        public int day_of_year => this.Dto.DayOfYear;

        // --- 操作方法 (返回新对象) ---
        public LuaDateTimeObject add_years(int years) => new(this.Dto.AddYears(years), Logger);
        public LuaDateTimeObject add_months(int months) => new(this.Dto.AddMonths(months), Logger);
        public LuaDateTimeObject add_days(double days) => new(this.Dto.AddDays(days), Logger);
        public LuaDateTimeObject add_hours(double hours) => new(this.Dto.AddHours(hours), Logger);
        public LuaDateTimeObject add_minutes(double minutes) => new(this.Dto.AddMinutes(minutes), Logger);
        public LuaDateTimeObject add_seconds(double seconds) => new(this.Dto.AddSeconds(seconds), Logger);

        // --- 格式化 ---
        /// <summary>
        /// 根据 .NET 格式化字符串来格式化日期。
        /// </summary>
        public string format(string formatString)
        {
            try
            {
                return Dto.ToString(formatString, CultureInfo.InvariantCulture);
            }
            catch(Exception ex)
            {
                Logger.error($"datetime:format 失败: {ex.Message}");
                return Dto.ToString("o"); // 返回一个默认的安全格式
            }
        }

        /// <summary>
        /// 默认输出为 ISO 8601 格式，方便调试。
        /// </summary>
        public override string ToString() => this.Dto.ToString("o"); // Round-trip format
    }
}